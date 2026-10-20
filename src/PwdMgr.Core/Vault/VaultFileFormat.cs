using System.Buffers.Binary;
using System.Text.Json;
using PwdMgr.Core.Crypto;
using PwdMgr.Core.Serialization;

namespace PwdMgr.Core.Vault;

/// <summary>
/// 密码库二进制容器：魔数 + 格式版本 + 头部 JSON + 密文长度 + nonce + 密文 + 认证标签。
/// 头部及长度字段整体作为 AES-GCM 的附加认证数据，改动任意一字节都会导致解密失败。
/// </summary>
internal static class VaultFileFormat
{
    public const int CurrentFormatVersion = 1;

    public const string AppTag = "pwdmgr";
    public const string CipherAlgorithm = "AES-256-GCM";
    public const string WrapAlgorithm = "RSA-OAEP-SHA256";

    public const int MagicSize = 4;
    public const int VersionSize = 2;
    public const int LengthFieldSize = 4;
    public const int MaxHeaderSize = 64 * 1024;
    public const int MaxPayloadSize = 256 * 1024 * 1024;

    private const int MaxWrappedKeySize = 1024;

    private static ReadOnlySpan<byte> Magic => "PWDB"u8;

    /// <summary>
    /// 构造“附加认证数据”前缀，即密文之前的全部字节。
    /// 加密前必须先有它，因此它与 <see cref="Assemble"/> 分成两步。
    /// </summary>
    public static byte[] BuildPrefix(VaultHeader header, int ciphertextLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ciphertextLength);

        var headerJson = JsonSerializer.SerializeToUtf8Bytes(header, VaultJsonContext.Default.VaultHeader);
        if (headerJson.Length > MaxHeaderSize)
        {
            throw new VaultException(VaultErrorCode.InvalidPayload, "文件头超出允许的长度。");
        }

        var prefix = new byte[MagicSize + VersionSize + LengthFieldSize + headerJson.Length + LengthFieldSize];
        var span = prefix.AsSpan();

        Magic.CopyTo(span);
        BinaryPrimitives.WriteUInt16LittleEndian(span[4..], CurrentFormatVersion);
        BinaryPrimitives.WriteInt32LittleEndian(span[6..], headerJson.Length);
        headerJson.CopyTo(span[10..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[(10 + headerJson.Length)..], ciphertextLength);

        return prefix;
    }

    /// <summary>把前缀、nonce、密文与标签拼成完整文件内容。</summary>
    public static byte[] Assemble(
        ReadOnlySpan<byte> prefix,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> tag)
    {
        if (nonce.Length != VaultCrypto.NonceSizeBytes)
        {
            throw new ArgumentException($"nonce 必须为 {VaultCrypto.NonceSizeBytes} 字节。", nameof(nonce));
        }

        if (tag.Length != VaultCrypto.TagSizeBytes)
        {
            throw new ArgumentException($"标签必须为 {VaultCrypto.TagSizeBytes} 字节。", nameof(tag));
        }

        var buffer = new byte[prefix.Length + nonce.Length + ciphertext.Length + tag.Length];
        var span = buffer.AsSpan();

        prefix.CopyTo(span);
        nonce.CopyTo(span[prefix.Length..]);
        ciphertext.CopyTo(span[(prefix.Length + nonce.Length)..]);
        tag.CopyTo(span[(prefix.Length + nonce.Length + ciphertext.Length)..]);

        return buffer;
    }

    /// <summary>解析文件结构，返回头部与各段位置。任何结构异常都抛出 <see cref="VaultException"/>。</summary>
    public static ParsedVaultFile Parse(ReadOnlySpan<byte> file)
    {
        const int fixedPrefix = MagicSize + VersionSize + LengthFieldSize;
        if (file.Length < fixedPrefix + LengthFieldSize + VaultCrypto.NonceSizeBytes + VaultCrypto.TagSizeBytes)
        {
            throw new VaultException(VaultErrorCode.FileCorrupted, "文件太短，不是完整的密码库。");
        }

        if (!file[..MagicSize].SequenceEqual(Magic))
        {
            throw new VaultException(VaultErrorCode.NotAVaultFile);
        }

        var version = BinaryPrimitives.ReadUInt16LittleEndian(file[MagicSize..]);
        if (version != CurrentFormatVersion)
        {
            throw new VaultException(VaultErrorCode.UnsupportedVersion);
        }

        var headerLength = BinaryPrimitives.ReadInt32LittleEndian(file[(MagicSize + VersionSize)..]);
        if (headerLength <= 0 || headerLength > MaxHeaderSize)
        {
            throw new VaultException(VaultErrorCode.FileCorrupted, "文件头长度异常。");
        }

        var prefixLength = fixedPrefix + headerLength + LengthFieldSize;
        var minimumLength = prefixLength + VaultCrypto.NonceSizeBytes + VaultCrypto.TagSizeBytes;
        if (file.Length < minimumLength)
        {
            throw new VaultException(VaultErrorCode.FileCorrupted, "文件被截断。");
        }

        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(file[(prefixLength - LengthFieldSize)..]);
        if (payloadLength < 0 || payloadLength > MaxPayloadSize)
        {
            throw new VaultException(VaultErrorCode.FileCorrupted, "密文长度异常。");
        }

        var expectedLength = minimumLength + payloadLength;
        if (file.Length != expectedLength)
        {
            throw new VaultException(VaultErrorCode.FileCorrupted, "文件长度与声明不一致。");
        }

        var header = DeserializeHeader(file.Slice(fixedPrefix, headerLength));
        ValidateHeader(header);

        var nonceOffset = prefixLength;
        var ciphertextOffset = nonceOffset + VaultCrypto.NonceSizeBytes;

        return new ParsedVaultFile(
            header,
            file[..prefixLength],
            file.Slice(nonceOffset, VaultCrypto.NonceSizeBytes),
            file.Slice(ciphertextOffset, payloadLength),
            file.Slice(ciphertextOffset + payloadLength, VaultCrypto.TagSizeBytes));
    }

    private static VaultHeader DeserializeHeader(ReadOnlySpan<byte> headerJson)
    {
        VaultHeader? header;
        try
        {
            header = JsonSerializer.Deserialize(headerJson, VaultJsonContext.Default.VaultHeader);
        }
        catch (JsonException ex)
        {
            throw new VaultException(VaultErrorCode.FileCorrupted, "文件头不是有效的 JSON。", ex);
        }

        if (header is null)
        {
            throw new VaultException(VaultErrorCode.FileCorrupted, "文件头为空。");
        }

        return header;
    }

    private static void ValidateHeader(VaultHeader header)
    {
        if (header.Format != CurrentFormatVersion)
        {
            throw new VaultException(VaultErrorCode.UnsupportedVersion);
        }

        if (!string.Equals(header.Cipher, CipherAlgorithm, StringComparison.Ordinal)
            || !string.Equals(header.Wrap, WrapAlgorithm, StringComparison.Ordinal))
        {
            throw new VaultException(VaultErrorCode.UnsupportedVersion, "密码库使用了不支持的加密算法。");
        }

        if (string.IsNullOrEmpty(header.KeyId)
            || header.WrappedKey.Length is <= 0 or > MaxWrappedKeySize)
        {
            throw new VaultException(VaultErrorCode.FileCorrupted, "文件头中的密钥信息不完整。");
        }
    }
}

/// <summary>已解析的密码库文件各段，指向原始缓冲区，不复制数据。</summary>
internal readonly ref struct ParsedVaultFile(
    VaultHeader header,
    ReadOnlySpan<byte> aad,
    ReadOnlySpan<byte> nonce,
    ReadOnlySpan<byte> ciphertext,
    ReadOnlySpan<byte> tag)
{
    public VaultHeader Header { get; } = header;

    public ReadOnlySpan<byte> Aad { get; } = aad;

    public ReadOnlySpan<byte> Nonce { get; } = nonce;

    public ReadOnlySpan<byte> Ciphertext { get; } = ciphertext;

    public ReadOnlySpan<byte> Tag { get; } = tag;
}
