using System.Security.Cryptography;

namespace PwdMgr.Core.Crypto;

/// <summary>数据加密、随机数与密钥的底层操作。</summary>
public static class VaultCrypto
{
    /// <summary>数据密钥长度：AES-256 需要 32 字节。</summary>
    public const int DataKeySizeBytes = 32;

    /// <summary>AES-GCM 随机数长度。</summary>
    public const int NonceSizeBytes = 12;

    /// <summary>AES-GCM 认证标签长度。</summary>
    public const int TagSizeBytes = 16;

    /// <summary>生成一条随机的数据密钥。</summary>
    public static byte[] CreateDataKey()
    {
        var key = new byte[DataKeySizeBytes];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    /// <summary>生成一个随机 nonce，并保证不与上一次重复。</summary>
    public static byte[] CreateNonce(ReadOnlySpan<byte> previousNonce)
    {
        var nonce = new byte[NonceSizeBytes];
        RandomNumberGenerator.Fill(nonce);

        if (!previousNonce.IsEmpty && nonce.AsSpan().SequenceEqual(previousNonce))
        {
            RandomNumberGenerator.Fill(nonce);
        }

        return nonce;
    }

    /// <summary>加密明文，返回密文与认证标签。</summary>
    public static (byte[] Ciphertext, byte[] Tag) Encrypt(
        ReadOnlySpan<byte> plaintext,
        byte[] dataKey,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> associatedData)
    {
        ArgumentNullException.ThrowIfNull(dataKey);
        ValidateKeyAndNonce(dataKey, nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(dataKey, TagSizeBytes);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
        return (ciphertext, tag);
    }

    /// <summary>解密并校验标签；校验失败返回 <see langword="false"/>，不抛出异常。</summary>
    public static bool TryDecrypt(
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> tag,
        byte[] dataKey,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> associatedData,
        out byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(dataKey);
        ValidateKeyAndNonce(dataKey, nonce);

        if (tag.Length != TagSizeBytes)
        {
            plaintext = [];
            return false;
        }

        var buffer = new byte[ciphertext.Length];

        using var aes = new AesGcm(dataKey, TagSizeBytes);
        try
        {
            aes.Decrypt(nonce, ciphertext, tag, buffer, associatedData);
            plaintext = buffer;
            return true;
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(buffer);
            plaintext = [];
            return false;
        }
    }

    private static void ValidateKeyAndNonce(ReadOnlySpan<byte> dataKey, ReadOnlySpan<byte> nonce)
    {
        if (dataKey.Length != DataKeySizeBytes)
        {
            throw new ArgumentException($"数据密钥必须为 {DataKeySizeBytes} 字节。", nameof(dataKey));
        }

        if (nonce.Length != NonceSizeBytes)
        {
            throw new ArgumentException($"nonce 必须为 {NonceSizeBytes} 字节。", nameof(nonce));
        }
    }
}
