using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PwdMgr.Core.Serialization;
using PwdMgr.Core.Storage;
using PwdMgr.Core.Vault;

namespace PwdMgr.Core.Crypto;

/// <summary>钥匙文件的读写。文件内容为 UTF-8 JSON，私钥以 Base64 存放。</summary>
public static class KeyFileStore
{
    /// <summary>当前钥匙文件格式版本。</summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>钥匙类型标识。</summary>
    public const string KeyType = "RSAPrivateKey";

    /// <summary>把密钥对写入钥匙文件，采用“临时文件 + 替换”方式，避免写到一半留下坏文件。</summary>
    /// <summary>
    /// 把密钥对写入钥匙文件。钥匙文件只在创建时写入，直接覆盖目标（可能是文件对话框预建的
    /// 0 字节占位文件），不做临时文件替换，避免依赖删除与改名权限。
    /// </summary>
    public static void Save(string path, VaultKeyPair keyPair)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(keyPair);

        var model = new KeyFileModel
        {
            KeyId = keyPair.KeyId,
            Algorithm = $"RSA-{keyPair.KeySizeBits}",
            CreatedUtc = DateTimeOffset.UtcNow,
            PublicKeySpki = Convert.ToBase64String(keyPair.ExportPublicKeySpki()),
            PrivateKeyPkcs8 = Convert.ToBase64String(keyPair.ExportPrivateKeyPkcs8()),
        };

        var json = JsonSerializer.Serialize(model, VaultJsonContext.Default.KeyFileModel);
        AtomicFile.WriteInPlace(path, Encoding.UTF8.GetBytes(json));
    }

    /// <summary>读取并校验钥匙文件。任何不一致都会抛出 <see cref="VaultException"/>。</summary>
    public static VaultKeyPair Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new VaultException(VaultErrorCode.KeyFileNotFound, $"找不到钥匙文件：{path}");
        }

        byte[] json;
        try
        {
            json = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new VaultException(VaultErrorCode.InvalidKeyFile, $"无法读取钥匙文件：{path}", ex);
        }

        KeyFileModel? model;
        try
        {
            model = JsonSerializer.Deserialize(json, VaultJsonContext.Default.KeyFileModel);
        }
        catch (JsonException ex)
        {
            throw new VaultException(VaultErrorCode.InvalidKeyFile, "钥匙文件不是有效的 JSON。", ex);
        }

        if (model is null
            || model.Format != CurrentFormatVersion
            || !string.Equals(model.Type, KeyType, StringComparison.Ordinal))
        {
            throw new VaultException(VaultErrorCode.InvalidKeyFile);
        }

        byte[] pkcs8;
        try
        {
            pkcs8 = Convert.FromBase64String(model.PrivateKeyPkcs8);
        }
        catch (FormatException ex)
        {
            throw new VaultException(VaultErrorCode.InvalidKeyFile, "钥匙文件中的私钥不是有效的 Base64。", ex);
        }

        VaultKeyPair keyPair;
        try
        {
            keyPair = VaultKeyPair.FromPkcs8(pkcs8);
        }
        catch (CryptographicException ex)
        {
            throw new VaultException(VaultErrorCode.InvalidKeyFile, "钥匙文件中的私钥无法解析。", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pkcs8);
        }

        try
        {
            if (!string.Equals(keyPair.KeyId, model.KeyId, StringComparison.Ordinal))
            {
                throw new VaultException(VaultErrorCode.InvalidKeyFile, "钥匙文件内部指纹不一致，文件可能被修改过。");
            }

            return keyPair;
        }
        catch
        {
            keyPair.Dispose();
            throw;
        }
    }
}
