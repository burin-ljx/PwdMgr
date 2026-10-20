namespace PwdMgr.Core.Models;

/// <summary>
/// 密码库中的一条记录。属性可变是为了直接绑定编辑界面；落盘时整体序列化为 JSON。
/// </summary>
public sealed class PasswordItem
{
    /// <summary>条目唯一标识，创建后不再改变。</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>显示名称，例如“GitHub”。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>登录用户名或邮箱。</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>密码明文。整个库落盘前会统一加密。</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>网址，仅作为文本保存与复制，软件不会打开它。</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>备注。</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>标签，用于分类与搜索。</summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>创建时间（UTC）。</summary>
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>最后修改时间（UTC）。</summary>
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>非空表示条目已进回收站，值为删除时间（UTC）。</summary>
    public DateTimeOffset? DeletedUtc { get; set; }

    /// <summary>该条目是否在回收站中。</summary>
    public bool IsDeleted => DeletedUtc is not null;
}
