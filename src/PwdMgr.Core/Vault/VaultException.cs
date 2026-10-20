namespace PwdMgr.Core.Vault;

/// <summary>
/// 密码库操作失败时抛出的统一异常。界面层只需捕获这一种异常，并按 <see cref="Code"/> 给提示。
/// </summary>
public sealed class VaultException : Exception
{
    public VaultException(VaultErrorCode code, string? message = null, Exception? innerException = null)
        : base(message ?? Describe(code), innerException)
    {
        Code = code;
    }

    /// <summary>失败原因。</summary>
    public VaultErrorCode Code { get; }

    /// <summary>按错误码给出面向用户的中文提示。</summary>
    public static string Describe(VaultErrorCode code) => code switch
    {
        VaultErrorCode.NotAVaultFile => "这不是一个有效的密码库文件。",
        VaultErrorCode.UnsupportedVersion => "这个密码库由更新版本的软件创建，请升级后再打开。",
        VaultErrorCode.FileCorrupted => "密码库文件不完整或已损坏。",
        VaultErrorCode.TargetExists => "目标文件已存在，请换一个位置或改名。",
        VaultErrorCode.InvalidKeyFile => "这个文件不是本软件的钥匙文件，或内容已损坏。",
        VaultErrorCode.KeyMismatch => "这把钥匙不属于这个密码库。",
        VaultErrorCode.KeyUnwrapFailed => "无法用这把钥匙解开密码库，钥匙可能已损坏。",
        VaultErrorCode.DecryptionFailed => "密码库校验失败，文件可能被修改过。可尝试用 .bak 备份恢复。",
        VaultErrorCode.InvalidPayload => "密码库内容格式不正确。",
        VaultErrorCode.KeyFileNotFound => "找不到钥匙文件。",
        VaultErrorCode.SaveFailed => "保存失败。请检查磁盘空间与文件占用情况。",
        VaultErrorCode.BackupFailed => "备份旧文件失败，为避免覆盖数据已放弃本次保存。",
        VaultErrorCode.VaultBusy => "这个密码库正在被另一个窗口使用。",
        VaultErrorCode.VaultFileNotFound => "找不到密码库文件。",
        _ => "操作失败。",
    };
}
