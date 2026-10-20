using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PwdMgr.App.Services;

namespace PwdMgr.App.Views;

public sealed partial class WelcomePage : Page
{
    public WelcomePage()
    {
        InitializeComponent();

        // 子元素（例如文本块）可能把拖放事件标记为已处理，这里连已处理的事件也接收。
        DropSurface.AddHandler(DragOverEvent, new DragEventHandler(OnDragOver), handledEventsToo: true);
        DropSurface.AddHandler(DropEvent, new DragEventHandler(OnDrop), handledEventsToo: true);
    }

    private void OnCreateClick(object sender, RoutedEventArgs e) => App.Shell.NavigateToCreate();

    private void OnOpenClick(object sender, RoutedEventArgs e) => App.Shell.NavigateToUnlock();

    private void OnThemeClick(object sender, RoutedEventArgs e) => ThemeMenu.Show((FrameworkElement)sender);

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (FileDropHelper.CanAccept(e))
        {
            FileDropHelper.Accept(e, "拖入以打开密码库");
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
            ShowDropError(result.Error);
            return;
        }

        if (result.VaultPath is { } vaultPath)
        {
            DropErrorBar.IsOpen = false;
            App.Shell.NavigateToUnlock(vaultPath, suggestKey: true);
            return;
        }

        // 只拖了钥匙文件：没有密码库就无从打开。
        ShowDropError(FileDropHelper.KeyFirstMessage);
    }

    private void ShowDropError(string message)
    {
        DropErrorBar.Message = message;
        DropErrorBar.IsOpen = true;
    }
}
