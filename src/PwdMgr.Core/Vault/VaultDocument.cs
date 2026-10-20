using PwdMgr.Core.Models;

namespace PwdMgr.Core.Vault;

/// <summary>
/// 解密后的密码库内存模型。所有增删改都发生在内存里，由 <see cref="VaultSession"/> 负责落盘。
/// <para>本类不是线程安全的：界面层应只在 UI 线程上修改它。</para>
/// </summary>
public sealed class VaultDocument
{
    private const int MaxTitleLength = 200;
    private const int MaxUsernameLength = 200;
    private const int MaxPasswordLength = 1000;
    private const int MaxUrlLength = 2000;
    private const int MaxNotesLength = 8000;
    private const int MaxTagCount = 20;
    private const int MaxTagLength = 30;

    private readonly List<PasswordItem> _items;

    internal VaultDocument(
        Guid vaultId,
        DateTimeOffset createdUtc,
        DateTimeOffset updatedUtc,
        IEnumerable<PasswordItem>? items = null)
    {
        VaultId = vaultId;
        CreatedUtc = createdUtc;
        UpdatedUtc = updatedUtc;
        _items = items is null ? [] : [.. items];
    }

    /// <summary>密码库唯一标识。</summary>
    public Guid VaultId { get; }

    /// <summary>创建时间（UTC）。</summary>
    public DateTimeOffset CreatedUtc { get; }

    /// <summary>最后修改时间（UTC）。</summary>
    public DateTimeOffset UpdatedUtc { get; private set; }

    /// <summary>是否存在尚未保存的修改。</summary>
    public bool IsDirty { get; private set; }

    /// <summary>新建一个空文档。</summary>
    internal static VaultDocument CreateNew()
    {
        var now = DateTimeOffset.UtcNow;
        return new VaultDocument(Guid.NewGuid(), now, now);
    }

    /// <summary>从落盘结构恢复文档。</summary>
    internal static VaultDocument FromPayload(VaultPayload payload) =>
        new(payload.VaultId, payload.CreatedUtc, payload.UpdatedUtc, payload.Items);

    /// <summary>回收站之外的条目，按标题排序。</summary>
    public IReadOnlyList<PasswordItem> GetActiveItems() =>
        _items.Where(item => !item.IsDeleted)
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>回收站中的条目，最近删除的在前。</summary>
    public IReadOnlyList<PasswordItem> GetDeletedItems() =>
        _items.Where(item => item.IsDeleted)
            .OrderByDescending(item => item.DeletedUtc)
            .ToList();

    /// <summary>按 Id 查找条目，包括回收站中的条目。</summary>
    public PasswordItem? Find(Guid id) => _items.Find(item => item.Id == id);

    /// <summary>新增条目并返回它。标题必填。</summary>
    public PasswordItem Add(
        string title,
        string username = "",
        string password = "",
        string url = "",
        string notes = "",
        IEnumerable<string>? tags = null)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("标题不能为空。", nameof(title));
        }

        var now = DateTimeOffset.UtcNow;
        var item = new PasswordItem
        {
            Title = Clamp(title, MaxTitleLength),
            Username = Clamp(username, MaxUsernameLength),
            Password = Clamp(password, MaxPasswordLength),
            Url = Clamp(url, MaxUrlLength),
            Notes = Clamp(notes, MaxNotesLength),
            Tags = NormalizeTags(tags),
            CreatedUtc = now,
            UpdatedUtc = now,
        };

        _items.Add(item);
        Touch();
        return item;
    }

    /// <summary>用同 Id 条目的内容覆盖已有条目。条目不存在时返回 <see langword="false"/>。</summary>
    public bool Update(PasswordItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var existing = Find(item.Id);
        if (existing is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.Title))
        {
            throw new ArgumentException("标题不能为空。", nameof(item));
        }

        existing.Title = Clamp(item.Title, MaxTitleLength);
        existing.Username = Clamp(item.Username, MaxUsernameLength);
        existing.Password = Clamp(item.Password, MaxPasswordLength);
        existing.Url = Clamp(item.Url, MaxUrlLength);
        existing.Notes = Clamp(item.Notes, MaxNotesLength);
        existing.Tags = NormalizeTags(item.Tags);
        existing.UpdatedUtc = DateTimeOffset.UtcNow;

        Touch();
        return true;
    }

    /// <summary>把条目移入回收站。</summary>
    public bool MoveToTrash(Guid id, DateTimeOffset? deletedUtc = null)
    {
        var item = Find(id);
        if (item is null || item.IsDeleted)
        {
            return false;
        }

        item.DeletedUtc = deletedUtc ?? DateTimeOffset.UtcNow;
        Touch();
        return true;
    }

    /// <summary>从回收站恢复条目。</summary>
    public bool Restore(Guid id)
    {
        var item = Find(id);
        if (item is null || !item.IsDeleted)
        {
            return false;
        }

        item.DeletedUtc = null;
        item.UpdatedUtc = DateTimeOffset.UtcNow;
        Touch();
        return true;
    }

    /// <summary>彻底删除条目，不可恢复。</summary>
    public bool Purge(Guid id)
    {
        var item = Find(id);
        if (item is null)
        {
            return false;
        }

        _items.Remove(item);
        Touch();
        return true;
    }

    /// <summary>清理回收站中超过保留期的条目，返回清除数量。</summary>
    public int PurgeExpiredTrash(TimeSpan retention, DateTimeOffset? now = null)
    {
        var cutoff = (now ?? DateTimeOffset.UtcNow) - retention;
        var removed = _items.RemoveAll(item => item.IsDeleted && item.DeletedUtc < cutoff);

        if (removed > 0)
        {
            Touch();
        }

        return removed;
    }

    /// <summary>在前台条目中搜索；关键字为空时返回全部前台条目。</summary>
    public IReadOnlyList<PasswordItem> Search(string? keyword)
    {
        IEnumerable<PasswordItem> query = _items.Where(item => !item.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(item => Matches(item, term));
        }

        return query.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>生成用于落盘的快照。复制条目，避免序列化期间被界面线程修改。</summary>
    internal VaultPayload ToPayload() => new()
    {
        SchemaVersion = VaultPayload.CurrentSchemaVersion,
        VaultId = VaultId,
        CreatedUtc = CreatedUtc,
        UpdatedUtc = UpdatedUtc,
        Items = _items.Select(Clone).ToList(),
    };

    /// <summary>保存成功后清除“未保存”标记。</summary>
    internal void MarkSaved()
    {
        IsDirty = false;
        UpdatedUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>上锁时丢弃内存中的全部条目。</summary>
    internal void ClearSensitive()
    {
        foreach (var item in _items)
        {
            item.Password = string.Empty;
        }

        _items.Clear();
        IsDirty = false;
    }

    private void Touch()
    {
        UpdatedUtc = DateTimeOffset.UtcNow;
        IsDirty = true;
    }

    private static bool Matches(PasswordItem item, string term) =>
        item.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
        || item.Username.Contains(term, StringComparison.OrdinalIgnoreCase)
        || item.Url.Contains(term, StringComparison.OrdinalIgnoreCase)
        || item.Notes.Contains(term, StringComparison.OrdinalIgnoreCase)
        || item.Tags.Any(tag => tag.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static List<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        return tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => Clamp(tag, MaxTagLength))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxTagCount)
            .ToList();
    }

    private static string Clamp(string? value, int maxLength)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length <= maxLength)
        {
            return text;
        }

        var length = maxLength;
        if (char.IsHighSurrogate(text[length - 1]))
        {
            length--;
        }

        return text[..length];
    }

    private static PasswordItem Clone(PasswordItem source) => new()
    {
        Id = source.Id,
        Title = source.Title,
        Username = source.Username,
        Password = source.Password,
        Url = source.Url,
        Notes = source.Notes,
        Tags = [.. source.Tags],
        CreatedUtc = source.CreatedUtc,
        UpdatedUtc = source.UpdatedUtc,
        DeletedUtc = source.DeletedUtc,
    };
}
