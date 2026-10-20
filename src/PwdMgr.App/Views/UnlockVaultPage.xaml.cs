using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PwdMgr.App.Services;
using PwdMgr.App.ViewModels;
using PwdMgr.Core.Vault;

namespace PwdMgr.App.Views;

public sealed partial class UnlockVaultPage : Page
{
    public UnlockVaultPage()
    {
        InitializeComponent();

        // 输入框等子元素可能把拖放事件标记为已处理，这里连已处理的事件也接收。
        DropSurface.AddHandler(DragOverEvent, new DragEventHandler(OnDragOver), handledEventsToo: true);
        DropSurface.AddHandler(DropEvent, new DragEventHandler(OnDrop), handledEventsToo: true);
    }

    public UnlockVaultViewModel ViewModel { get; } = new();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is not UnlockNavigationArgs args || string.IsNullOrWhiteSpace(args.VaultPath))
        {
            return;
        }

        ViewModel.VaultPath = args.VaultPath;

        // 从“上锁”回来时不带钥匙路径，必须重新选择钥匙文件。
        if (!args.SuggestKey)
        {
            return;
        }

        var suggestedKeyPath = FilePickerService.SuggestKeyPath(args.VaultPath);
        if (File.Exists(suggestedKeyPath))
        {
            ViewModel.KeyPath = suggestedKeyPath;
        }
    }

    private void OnPickVaultClick(object sender, RoutedEventArgs e)
    {
        var path = FilePickerService.PickVaultToOpen(App.ShellWindow);
        if (path is not null)
        {
            ViewModel.VaultPath = path;
            ViewModel.ErrorMessage = null;
        }
    }

    private void OnPickKeyClick(object sender, RoutedEventArgs e)
    {
        var path = FilePickerService.PickKeyToOpen(App.ShellWindow);
        if (path is not null)
        {
            ViewModel.KeyPath = path;
            ViewModel.ErrorMessage = null;
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => App.Shell.NavigateToWelcome();

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (FileDropHelper.CanAccept(e))
        {
            FileDropHelper.Accept(e, "拖入密码库或钥匙文件");
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!FileDropHelper.CanAccept(e))
        {
            return;
        }

        e.Handled = true;
        var result = await FileDropHelper.ReadAsync(e);

        if (result.Error is not null)
        {
            ViewModel.ErrorMessage = result.Error;
            return;
        }

        if (result.VaultPath is { } vaultPath)
        {
            ViewModel.VaultPath = vaultPath;
            ViewModel.ErrorMessage = null;

            // 同目录下有同名钥匙文件时顺手填上。
            if (string.IsNullOrWhiteSpace(ViewModel.KeyPath))
            {
                var suggested = FilePickerService.SuggestKeyPath(vaultPath);
                if (File.Exists(suggested))
                {
                    ViewModel.KeyPath = suggested;
                }
            }
        }

        if (result.KeyPath is { } keyPath)
        {
            if (string.IsNullOrWhiteSpace(ViewModel.VaultPath))
            {
                ViewModel.ErrorMessage = FileDropHelper.KeyFirstMessage;
                return;
            }

            ViewModel.KeyPath = keyPath;
            ViewModel.ErrorMessage = null;
        }
    }

    /// <summary>去掉首尾空白与粘贴时可能带上的引号。</summary>
    private static string CleanPath(string path) => path.Trim().Trim('"').Trim();

    private async void OnOpenClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ErrorMessage = null;
        ViewModel.IsBusy = true;

        VaultSession? session = null;
        try
        {
            var vaultPath = CleanPath(ViewModel.VaultPath);
            var keyPath = CleanPath(ViewModel.KeyPath);

            session = await VaultSession.OpenAsync(vaultPath, keyPath);
            await App.Shell.EnterVaultAsync(session);
            session = null;
        }
        catch (VaultException ex)
        {
            ViewModel.ErrorMessage = ex.Message;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ViewModel.ErrorMessage = $"打开失败：{ex.Message}";
        }
        finally
        {
            if (session is not null)
            {
                await session.DisposeAsync();
            }

            ViewModel.IsBusy = false;
        }
    }
}
