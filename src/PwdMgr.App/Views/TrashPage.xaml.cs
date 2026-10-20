using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PwdMgr.App.Services;
using PwdMgr.App.ViewModels;
using PwdMgr.Core.Vault;

namespace PwdMgr.App.Views;

public sealed partial class TrashPage : Page
{
    private readonly ObservableCollection<TrashItemView> _items = [];
    private VaultSession? _session;

    public TrashPage()
    {
        InitializeComponent();
        TrashList.ItemsSource = _items;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        _session = App.Shell.Session;
        if (_session is null)
        {
            App.Shell.NavigateToWelcome();
            return;
        }

        Reload();
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => App.Shell.NavigateBackToVault();

    private async void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        if (_session is null || sender is not FrameworkElement { DataContext: TrashItemView item })
        {
            return;
        }

        if (!_session.Document.Restore(item.Id))
        {
            Reload();
            return;
        }

        await SaveOrReportAsync();
        Reload();
    }

    private async void OnPurgeClick(object sender, RoutedEventArgs e)
    {
        if (_session is null || sender is not FrameworkElement { DataContext: TrashItemView item })
        {
            return;
        }

        var confirmed = await ConfirmAsync(
            "彻底删除这条记录？",
            $"“{item.Title}”将被永久删除，无法恢复。",
            "彻底删除");

        if (!confirmed)
        {
            return;
        }

        _session.Document.Purge(item.Id);
        await SaveOrReportAsync();
        Reload();
    }

    private async void OnEmptyTrashClick(object sender, RoutedEventArgs e)
    {
        if (_session is null || _items.Count == 0)
        {
            return;
        }

        var confirmed = await ConfirmAsync(
            "清空回收站？",
            $"回收站里的 {_items.Count} 条记录将被永久删除，无法恢复。",
            "清空");

        if (!confirmed)
        {
            return;
        }

        foreach (var item in _items.ToList())
        {
            _session.Document.Purge(item.Id);
        }

        await SaveOrReportAsync();
        Reload();
    }

    private async Task SaveOrReportAsync()
    {
        if (_session is null)
        {
            return;
        }

        try
        {
            await _session.SaveAsync();
        }
        catch (VaultException ex)
        {
            ErrorBar.Message = ex.Message;
            ErrorBar.IsOpen = true;
        }
    }

    /// <summary>二次确认。确认按钮用红色，取消用默认（次要）样式，且不设默认按钮。</summary>
    private async Task<bool> ConfirmAsync(string title, string message, string primaryText)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = primaryText,
            PrimaryButtonStyle = (Style)Application.Current.Resources["DestructiveButtonStyle"],
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.None,
        };

        ThemeService.ApplyToDialog(dialog);

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void Reload()
    {
        if (_session is null)
        {
            return;
        }

        _items.Clear();
        foreach (var item in _session.Document.GetDeletedItems())
        {
            _items.Add(new TrashItemView(item));
        }

        StatusText.Text = _items.Count == 0 ? "回收站是空的" : $"共 {_items.Count} 条记录";
        EmptyPanel.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyTrashButton.IsEnabled = _items.Count > 0;
    }
}
