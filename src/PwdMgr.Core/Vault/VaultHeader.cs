namespace PwdMgr.Core.Vault;

/// <summary>
/// 密码库文件的明文头部。只包含解密所需的信息，不含标题、账号等任何内容。
/// </summary>
internal sealed class VaultHeader
{
    public int Format { get; set; } = VaultFileFormat.CurrentFormatVersion;

    public string App { get; set; } = VaultFileFormat.AppTag;

    public DateTimeOffset CreatedUtc { get; set; }

    public string Cipher { get; set; } = VaultFileFormat.CipherAlgorithm;

    public string Wrap { get; set; } = VaultFileFormat.WrapAlgorithm;

    /// <summary>公钥指纹，用来快速判断钥匙与库是否配对。</summary>
    public string KeyId { get; set; } = string.Empty;

    public int KeyBits { get; set; }

    /// <summary>被非对称密钥包裹的数据密钥。</summary>
    public byte[] WrappedKey { get; set; } = [];

    public int PayloadSchema { get; set; } = PwdMgr.Core.Models.VaultPayload.CurrentSchemaVersion;
}
