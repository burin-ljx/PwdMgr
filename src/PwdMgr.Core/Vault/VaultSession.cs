using System.Security.Cryptography;
using System.Text.Json;
using PwdMgr.Core.Crypto;
using PwdMgr.Core.Models;
using PwdMgr.Core.Serialization;
using PwdMgr.Core.Storage;

namespace PwdMgr.Core.Vault;

/// <summary>
/// 一次已解锁的密码库会话。它持有文件锁、钥匙、数据密钥与内存中的文档；
/// 释放时会清空敏感数据并删除锁文件。
/// </summary>
public sealed class VaultSession : IAsyncDisposable
{
    private const string LockFileSuffix = ".lock";

    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly FileStream _lockStream;
    private readonly VaultKeyPair _keyPair;
    private readonly byte[] _wrappedKey;
    private readonly DateTimeOffset _createdUtc;

    private byte[]? _dataKey;
    private byte[]? _lastNonce;
    private bool _disposed;

    private VaultSession(
        string vaultPath,
        string keyFilePath,
        FileStream lockStream,
        VaultKeyPair keyPair,
        byte[] wrappedKey,
        byte[] dataKey,
        byte[] nonce,
        DateTimeOffset createdUtc,
        VaultDocument document)
    {
        VaultPath = vaultPath;
        KeyFilePath = keyFilePath;
        _lockStream = lockStream;
        _keyPair = keyPair;
        _wrappedKey = wrappedKey;
        _dataKey = dataKey;
        _lastNonce = nonce;
        _createdUtc = createdUtc;
        Document = document;
    }

    /// <summary>密码库文件的完整路径。</summary>
    public string VaultPath { get; }

    /// <summary>钥匙文件的完整路径。</summary>
    public string KeyFilePath { get; }

    /// <summary>解密后的内存文档。</summary>
    public VaultDocument Document { get; }

    /// <summary>密码库唯一标识。</summary>
    public Guid VaultId => Document.VaultId;

    /// <summary>当前钥匙的公钥指纹。</summary>
    public string KeyId => _keyPair.KeyId;

    /// <summary>创建一个新的空密码库，并生成配套的钥匙文件。</summary>
    /// <exception cref="VaultException">目标文件已存在，或写入校验失败。</exception>
    public static async Task<VaultSession> CreateAsync(
        string vaultPath,
        string keyFilePath,
        CancellationToken cancellationToken = default)
    {
        var vaultFullPath = Path.GetFullPath(vaultPath);
        var keyFullPath = Path.GetFullPath(keyFilePath);

        EnsureDistinctPaths(vaultFullPath, keyFullPath);
        EnsureDirectory(vaultFullPath);
        EnsureDirectory(keyFullPath);
        EnsureDirectoryIsWritable(vaultFullPath);
        EnsureDirectoryIsWritable(keyFullPath);

        // 文件保存对话框会预建 0 字节文件：这种占位文件直接覆盖写，不删除重建。
        var vaultIsPlaceholder = EnsureWritableTarget(vaultFullPath, "密码库文件");
        var keyIsPlaceholder = EnsureWritableTarget(keyFullPath, "钥匙文件");

        var lockStream = AcquireLock(vaultFullPath);
        VaultKeyPair? keyPair = null;
        byte[]? dataKey = null;
        var keyFileCreated = false;
        var vaultFileCreated = false;

        try
        {
            keyPair = VaultKeyPair.Create();
            dataKey = VaultCrypto.CreateDataKey();
            var wrappedKey = keyPair.WrapDataKey(dataKey);
            var document = VaultDocument.CreateNew();

            try
            {
                ExecuteWithRetry(() => KeyFileStore.Save(keyFullPath, keyPair, writeInPlace: keyIsPlaceholder));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new VaultException(
                    VaultErrorCode.SaveFailed,
                    $"无法写入钥匙文件：{keyFullPath}{Environment.NewLine}{ex.Message}",
                    ex);
            }

            keyFileCreated = true;
            var header = new VaultHeader
            {
                CreatedUtc = document.CreatedUtc,
                KeyId = keyPair.KeyId,
                KeyBits = keyPair.KeySizeBits,
                WrappedKey = wrappedKey,
            };

            var (fileBytes, nonce) = BuildFileBytes(header, document.ToPayload(), dataKey, []);

            try
            {
                if (vaultIsPlaceholder)
                {
                    await ExecuteWithRetryAsync(
                            () => AtomicFile.WriteInPlaceAsync(vaultFullPath, fileBytes, cancellationToken))
                        .ConfigureAwait(false);
                }
                else
                {
                    await ExecuteWithRetryAsync(
                            () => AtomicFile.WriteAsync(vaultFullPath, fileBytes, cancellationToken))
                        .ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new VaultException(
                    VaultErrorCode.SaveFailed,
                    $"无法写入密码库文件：{vaultFullPath}{Environment.NewLine}{ex.Message}",
                    ex);
            }

            vaultFileCreated = true;

            // 新建后立刻读回校验，确保用户拿到的一定是能打开的库。
            VerifyFile(vaultFullPath, dataKey, document.VaultId);

            return new VaultSession(
                vaultFullPath,
                keyFullPath,
                lockStream,
                keyPair,
                wrappedKey,
                dataKey,
                nonce,
                document.CreatedUtc,
                document);
        }
        catch (Exception ex)
        {
            if (vaultFileCreated)
            {
                TryDelete(vaultFullPath);
            }

            if (keyFileCreated)
            {
                TryDelete(keyFullPath);
            }

            if (dataKey is not null)
            {
                CryptographicOperations.ZeroMemory(dataKey);
            }

            keyPair?.Dispose();
            lockStream.Dispose();

            if (ex is IOException or UnauthorizedAccessException)
            {
                throw new VaultException(
                    VaultErrorCode.SaveFailed,
                    $"写入密码库失败：{ex.Message}",
                    ex);
            }

            throw;
        }
    }

    /// <summary>用密码库文件与钥匙文件打开已有密码库。</summary>
    /// <exception cref="VaultException">文件不存在、钥匙不匹配、文件损坏等。</exception>
    public static async Task<VaultSession> OpenAsync(
        string vaultPath,
        string keyFilePath,
        CancellationToken cancellationToken = default)
    {
        var vaultFullPath = Path.GetFullPath(vaultPath);
        var keyFullPath = Path.GetFullPath(keyFilePath);

        EnsureDistinctPaths(vaultFullPath, keyFullPath);

        if (!File.Exists(vaultFullPath))
        {
            throw new VaultException(VaultErrorCode.VaultFileNotFound, $"找不到密码库文件：{vaultFullPath}");
        }

        if (!File.Exists(keyFullPath))
        {
            throw new VaultException(VaultErrorCode.KeyFileNotFound, $"找不到钥匙文件：{keyFullPath}");
        }

        var lockStream = AcquireLock(vaultFullPath);
        VaultKeyPair? keyPair = null;
        byte[]? dataKey = null;

        try
        {
            var fileBytes = await File.ReadAllBytesAsync(vaultFullPath, cancellationToken).ConfigureAwait(false);
            keyPair = KeyFileStore.Load(keyFullPath);
            var opened = DecryptVault(fileBytes, keyPair);
            dataKey = opened.DataKey;

            return new VaultSession(
                vaultFullPath,
                keyFullPath,
                lockStream,
                keyPair,
                opened.WrappedKey,
                opened.DataKey,
                opened.Nonce,
                opened.CreatedUtc,
                opened.Document);
        }
        catch
        {
            if (dataKey is not null)
            {
                CryptographicOperations.ZeroMemory(dataKey);
            }

            keyPair?.Dispose();
            lockStream.Dispose();
            throw;
        }
    }

    /// <summary>把内存文档加密写回密码库文件。写入后立即读回校验。</summary>
    /// <exception cref="VaultException">保存或校验失败。</exception>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dataKey = _dataKey ?? throw new ObjectDisposedException(nameof(VaultSession));
            var header = new VaultHeader
            {
                CreatedUtc = _createdUtc,
                KeyId = _keyPair.KeyId,
                KeyBits = _keyPair.KeySizeBits,
                WrappedKey = _wrappedKey,
            };

            var previousNonce = _lastNonce ?? [];
            var (fileBytes, nonce) = BuildFileBytes(header, Document.ToPayload(), dataKey, previousNonce);

            try
            {
                await AtomicFile.WriteAsync(VaultPath, fileBytes, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new VaultException(VaultErrorCode.SaveFailed, null, ex);
            }

            try
            {
                VerifyFile(VaultPath, dataKey, Document.VaultId);
            }
            catch (Exception ex)
            {
                RestoreBackup(VaultPath);
                throw new VaultException(VaultErrorCode.SaveFailed, "保存后校验失败，已尝试用备份恢复。", ex);
            }

            _lastNonce = nonce;
            Document.MarkSaved();
        }
        finally
        {
            _saveGate.Release();
        }
    }

    /// <summary>清空敏感数据、释放钥匙与文件锁。</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_dataKey is not null)
        {
            CryptographicOperations.ZeroMemory(_dataKey);
            _dataKey = null;
        }

        if (_lastNonce is not null)
        {
            CryptographicOperations.ZeroMemory(_lastNonce);
            _lastNonce = null;
        }

        Document.ClearSensitive();
        _keyPair.Dispose();
        await _lockStream.DisposeAsync().ConfigureAwait(false);
        _saveGate.Dispose();
        GC.SuppressFinalize(this);
    }

    private static OpenedVault DecryptVault(byte[] fileBytes, VaultKeyPair keyPair)
    {
        var parsed = VaultFileFormat.Parse(fileBytes);

        if (!string.Equals(parsed.Header.KeyId, keyPair.KeyId, StringComparison.Ordinal))
        {
            throw new VaultException(VaultErrorCode.KeyMismatch);
        }

        byte[] dataKey;
        try
        {
            dataKey = keyPair.UnwrapDataKey(parsed.Header.WrappedKey);
        }
        catch (CryptographicException ex)
        {
            throw new VaultException(VaultErrorCode.KeyUnwrapFailed, null, ex);
        }

        if (!VaultCrypto.TryDecrypt(parsed.Ciphertext, parsed.Tag, dataKey, parsed.Nonce, parsed.Aad, out var plaintext))
        {
            CryptographicOperations.ZeroMemory(dataKey);
            throw new VaultException(VaultErrorCode.DecryptionFailed);
        }

        try
        {
            var payload = JsonSerializer.Deserialize(plaintext, VaultJsonContext.Default.VaultPayload);
            if (payload is null)
            {
                CryptographicOperations.ZeroMemory(dataKey);
                throw new VaultException(VaultErrorCode.InvalidPayload);
            }

            if (payload.SchemaVersion > VaultPayload.CurrentSchemaVersion)
            {
                CryptographicOperations.ZeroMemory(dataKey);
                throw new VaultException(VaultErrorCode.UnsupportedVersion, "密码库由更新版本的软件写入。");
            }

            return new OpenedVault(
                parsed.Header.WrappedKey,
                dataKey,
                parsed.Nonce.ToArray(),
                parsed.Header.CreatedUtc,
                VaultDocument.FromPayload(payload));
        }
        catch (JsonException ex)
        {
            CryptographicOperations.ZeroMemory(dataKey);
            throw new VaultException(VaultErrorCode.InvalidPayload, "密码库内容无法解析。", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static (byte[] FileBytes, byte[] Nonce) BuildFileBytes(
        VaultHeader header,
        VaultPayload payload,
        byte[] dataKey,
        ReadOnlySpan<byte> previousNonce)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload, VaultJsonContext.Default.VaultPayload);
        try
        {
            var nonce = VaultCrypto.CreateNonce(previousNonce);
            var prefix = VaultFileFormat.BuildPrefix(header, plaintext.Length);
            var (ciphertext, tag) = VaultCrypto.Encrypt(plaintext, dataKey, nonce, prefix);
            return (VaultFileFormat.Assemble(prefix, nonce, ciphertext, tag), nonce);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static void VerifyFile(string path, byte[] dataKey, Guid expectedVaultId)
    {
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new VaultException(VaultErrorCode.SaveFailed, "保存后无法读回落盘文件。", ex);
        }

        var parsed = VaultFileFormat.Parse(bytes);
        if (!VaultCrypto.TryDecrypt(parsed.Ciphertext, parsed.Tag, dataKey, parsed.Nonce, parsed.Aad, out var plaintext))
        {
            throw new VaultException(VaultErrorCode.SaveFailed, "保存后校验失败：写回的内容无法解密。");
        }

        try
        {
            var payload = JsonSerializer.Deserialize(plaintext, VaultJsonContext.Default.VaultPayload);
            if (payload is null || payload.VaultId != expectedVaultId)
            {
                throw new VaultException(VaultErrorCode.SaveFailed, "保存后校验失败：写回的内容与内存不一致。");
            }
        }
        catch (JsonException ex)
        {
            throw new VaultException(VaultErrorCode.SaveFailed, "保存后校验失败：写回的内容无法解析。", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    /// <summary>
    /// 反复重试短暂失败的文件操作。杀毒软件扫描刚创建的文件时，会短时间占住它并返回
    /// “拒绝访问”，重试几次通常就能通过。
    /// </summary>
    private static void ExecuteWithRetry(Action action, int attempts = 5)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex) when (attempt < attempts && ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(150);
            }
        }
    }

    private static async Task ExecuteWithRetryAsync(Func<Task> action, int attempts = 5)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await action().ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (attempt < attempts && ex is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(150).ConfigureAwait(false);
            }
        }
    }

    private static FileStream AcquireLock(string vaultPath)
    {
        var lockPath = vaultPath + LockFileSuffix;

        try
        {
            return new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                1,
                FileOptions.DeleteOnClose);
        }
        catch (IOException ex)
        {
            throw new VaultException(VaultErrorCode.VaultBusy, null, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new VaultException(
                VaultErrorCode.SaveFailed,
                $"无法创建锁文件：{lockPath}{Environment.NewLine}{ex.Message}",
                ex);
        }
    }

    private static void RestoreBackup(string vaultPath)
    {
        var backupPath = vaultPath + ".bak";
        if (File.Exists(backupPath))
        {
            File.Copy(backupPath, vaultPath, overwrite: true);
        }
    }

    private static void EnsureDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// 写入前先在目标目录建一个临时文件再删掉，确认目录本身可写。
    /// 这样权限或安全软件拦截会在真正写入前暴露，错误也能明确指向文件夹。
    /// </summary>
    private static void EnsureDirectoryIsWritable(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        var probePath = Path.Combine(directory, $".pwdmgr-probe-{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(
                       probePath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       1,
                       FileOptions.DeleteOnClose))
            {
                stream.WriteByte(0);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new VaultException(
                VaultErrorCode.SaveFailed,
                $"无法在这个文件夹里创建文件：{directory}。请换一个有写入权限的文件夹，或检查安全软件的拦截。",
                ex);
        }
    }

    private static void EnsureDistinctPaths(string vaultPath, string keyPath)
    {
        if (string.Equals(vaultPath, keyPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("密码库与钥匙文件不能使用同一个路径。", nameof(keyPath));
        }
    }

    /// <summary>
    /// 检查写入目标。返回 true 表示目标是一个 0 字节占位文件（文件保存对话框预建），
    /// 调用方应直接覆盖写；返回 false 表示目标不存在；非空文件一律拒绝，避免误覆盖已有数据。
    /// </summary>
    private static bool EnsureWritableTarget(string path, string description)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        long length;
        try
        {
            length = new FileInfo(path).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new VaultException(
                VaultErrorCode.TargetExists,
                $"{description}已存在，但无法读取它的状态：{path}",
                ex);
        }

        if (length == 0)
        {
            return true;
        }

        throw new VaultException(
            VaultErrorCode.TargetExists,
            $"{description}已存在且不是空文件，请换一个文件名或先移除它：{path}");
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 回滚时的清理失败不覆盖原始异常。
        }
    }
}

/// <summary>解密结果，只在打开流程内部使用。</summary>
internal sealed record OpenedVault(
    byte[] WrappedKey,
    byte[] DataKey,
    byte[] Nonce,
    DateTimeOffset CreatedUtc,
    VaultDocument Document);
