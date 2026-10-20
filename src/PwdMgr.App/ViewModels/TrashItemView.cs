using PwdMgr.Core.Models;

namespace PwdMgr.App.ViewModels;

/// <summary>回收站列表项的展示数据。</summary>
public sealed class TrashItemView(PasswordItem item)
{
    public Guid Id { get; } = item.Id;

    public string Title { get; } = item.Title;

    public string Username { get; } = item.Username;

    public string DeletedText { get; } =
        item.DeletedUtc is { } deleted ? $"删除于 {deleted.ToLocalTime():yyyy-MM-dd HH:mm}" : string.Empty;

    public bool IsDeleted { get; } = item.IsDeleted;
}
