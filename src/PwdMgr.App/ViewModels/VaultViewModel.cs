using System.Collections.ObjectModel;
using PwdMgr.App.Common;
using PwdMgr.Core.Models;
using PwdMgr.Core.Vault;

namespace PwdMgr.App.ViewModels;

/// <summary>主界面状态：当前会话、可见条目列表与搜索。</summary>
public sealed class VaultViewModel : ObservableObject
{
    private int _visibleCount;

    public VaultViewModel(VaultSession session)
    {
        Session = session;
        Items = new ObservableCollection<PasswordItem>(session.Document.GetActiveItems());
        VaultFileName = Path.GetFileNameWithoutExtension(session.VaultPath);
        VaultPath = session.VaultPath;
        KeyFingerprint = session.KeyId;
        _visibleCount = Items.Count;
    }

    public VaultSession Session { get; }

    public ObservableCollection<PasswordItem> Items { get; }

    public string VaultFileName { get; }

    public string VaultPath { get; }

    public string KeyFingerprint { get; }

    public int VisibleCount
    {
        get => _visibleCount;
        private set
        {
            if (SetProperty(ref _visibleCount, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string MetaText => $"{VaultPath}   ·   钥匙指纹 {KeyFingerprint}";

    public string StatusText => VisibleCount == 0
        ? "没有条目。下一阶段会在这里新增和编辑条目。"
        : $"共 {VisibleCount} 个条目";

    /// <summary>按关键字过滤条目；关键字为空时显示全部。</summary>
    public void ApplySearch(string? keyword)
    {
        var results = Session.Document.Search(keyword);

        Items.Clear();
        foreach (var item in results)
        {
            Items.Add(item);
        }

        VisibleCount = Items.Count;
    }
}
