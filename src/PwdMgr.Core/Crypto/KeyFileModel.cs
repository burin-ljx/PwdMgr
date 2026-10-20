namespace PwdMgr.Core.Crypto;

/// <summary>
/// 钥匙文件的磁盘结构。私钥按已确认的取舍以明文 Base64 保存，不使用口令加密。
/// </summary>
internal sealed class KeyFileModel
{
    public int Format { get; set; } = KeyFileStore.CurrentFormatVersion;

    public string Type { get; set; } = KeyFileStore.KeyType;

    public string KeyId { get; set; } = string.Empty;

    public string Algorithm { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }

    public string PublicKeySpki { get; set; } = string.Empty;

    public string PrivateKeyPkcs8 { get; set; } = string.Empty;
}
