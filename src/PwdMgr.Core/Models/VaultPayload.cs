namespace PwdMgr.Core.Models;

/// <summary>
/// 加密后写入密码库文件的明文结构。字段增删必须同时提升 <see cref="SchemaVersion"/>，
/// 并由读取端忽略未知字段，以保证旧版本仍能打开新版本写入的库。
/// </summary>
internal sealed class VaultPayload
{
    /// <summary>当前明文结构版本。</summary>
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public Guid VaultId { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<PasswordItem> Items { get; set; } = [];
}
