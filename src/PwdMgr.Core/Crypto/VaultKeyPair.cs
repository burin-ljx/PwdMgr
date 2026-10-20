using System.Security.Cryptography;

namespace PwdMgr.Core.Crypto;

/// <summary>
/// 密码库的非对称密钥对。私钥只存在于内存与用户自己保管的钥匙文件中，软件不做任何托管。
/// </summary>
public sealed class VaultKeyPair : IDisposable
{
    /// <summary>默认密钥长度（位）。</summary>
    public const int DefaultKeySizeBits = 3072;

    /// <summary>指纹截取的字节数，指纹以 16 位十六进制字符串呈现。</summary>
    private const int FingerprintSizeBytes = 8;

    private readonly RSA _rsa;

    private VaultKeyPair(RSA rsa)
    {
        _rsa = rsa;
        KeySizeBits = rsa.KeySize;
        var publicKeyHash = SHA256.HashData(rsa.ExportSubjectPublicKeyInfo());
        KeyId = Convert.ToHexString(publicKeyHash.AsSpan(0, FingerprintSizeBytes)).ToLowerInvariant();
    }

    /// <summary>公钥指纹，用于快速判断钥匙与密码库是否配对。</summary>
    public string KeyId { get; }

    /// <summary>密钥长度（位）。</summary>
    public int KeySizeBits { get; }

    /// <summary>生成一对新的非对称密钥。</summary>
    public static VaultKeyPair Create(int keySizeBits = DefaultKeySizeBits)
    {
        if (keySizeBits < 2048)
        {
            throw new ArgumentOutOfRangeException(nameof(keySizeBits), keySizeBits, "密钥长度至少为 2048 位。");
        }

        return new VaultKeyPair(RSA.Create(keySizeBits));
    }

    /// <summary>从 PKCS#8 私钥导入密钥对。</summary>
    public static VaultKeyPair FromPkcs8(ReadOnlySpan<byte> pkcs8)
    {
        var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(pkcs8, out _);
        return new VaultKeyPair(rsa);
    }

    /// <summary>导出 PKCS#8 私钥字节。</summary>
    public byte[] ExportPrivateKeyPkcs8() => _rsa.ExportPkcs8PrivateKey();

    /// <summary>导出 SubjectPublicKeyInfo 公钥字节。</summary>
    public byte[] ExportPublicKeySpki() => _rsa.ExportSubjectPublicKeyInfo();

    /// <summary>用公钥包裹数据密钥。</summary>
    public byte[] WrapDataKey(ReadOnlySpan<byte> dataKey) =>
        _rsa.Encrypt(dataKey.ToArray(), RSAEncryptionPadding.OaepSHA256);

    /// <summary>用私钥解开数据密钥。钥匙不匹配时抛出 <see cref="CryptographicException"/>。</summary>
    public byte[] UnwrapDataKey(ReadOnlySpan<byte> wrappedKey) =>
        _rsa.Decrypt(wrappedKey.ToArray(), RSAEncryptionPadding.OaepSHA256);

    public void Dispose() => _rsa.Dispose();
}
