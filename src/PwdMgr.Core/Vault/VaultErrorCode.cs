namespace PwdMgr.Core.Vault;

/// <summary>
/// 密码库操作失败的原因。数值顺序与文档中的错误码一致，界面层可据此给出固定提示。
/// </summary>
public enum VaultErrorCode
{
    /// <summary>E100：文件不是本软件创建的密码库。</summary>
    NotAVaultFile = 0,

    /// <summary>E101：文件版本高于当前软件支持的版本。</summary>
    UnsupportedVersion = 1,

    /// <summary>E102：文件被截断、长度异常或结构损坏。</summary>
    FileCorrupted = 2,

    /// <summary>E104：目标文件已存在，拒绝覆盖。</summary>
    TargetExists = 3,

    /// <summary>E200：钥匙文件内容不合法。</summary>
    InvalidKeyFile = 4,

    /// <summary>E201：钥匙与密码库不匹配。</summary>
    KeyMismatch = 5,

    /// <summary>E202：无法用该钥匙解开数据密钥。</summary>
    KeyUnwrapFailed = 6,

    /// <summary>E203：解密校验失败，文件可能被修改。</summary>
    DecryptionFailed = 7,

    /// <summary>E204：解密后的内容不是有效的密码库数据。</summary>
    InvalidPayload = 8,

    /// <summary>E205：找不到钥匙文件。</summary>
    KeyFileNotFound = 9,

    /// <summary>E300：保存失败。</summary>
    SaveFailed = 10,

    /// <summary>E301：备份旧文件失败，为避免丢数据未继续保存。</summary>
    BackupFailed = 11,

    /// <summary>E400：密码库正被另一个实例使用。</summary>
    VaultBusy = 12,

    /// <summary>E103：找不到密码库文件。</summary>
    VaultFileNotFound = 13,
}
