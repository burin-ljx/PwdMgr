using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PwdMgr.App.Services;
using PwdMgr.App.ViewModels;
using PwdMgr.Core.Vault;

namespace PwdMgr.App.Views;

public sealed partial class CreateVaultPage : Page
{
    public CreateVaultPage()
    {
        InitializeComponent();
    }

    public CreateVaultViewModel ViewModel { get; } = new();

    private void OnPickVaultClick(object sender, RoutedEventArgs e)
    {
        var path = FilePickerService.PickVaultToCreate(App.ShellWindow, "我的密码库");
        if (path is null)
        {
            return;
        }

        ViewModel.VaultPath = path;

        if (string.IsNullOrWhiteSpace(ViewModel.KeyPath))
        {
            ViewModel.KeyPath = FilePickerService.SuggestKeyPath(path);
        }
    }

    private void OnPickKeyClick(object sender, RoutedEventArgs e)
    {
        var path = FilePickerService.PickKeyToCreate(App.ShellWindow, "我的钥匙");
        if (path is not null)
        {
            ViewModel.KeyPath = path;
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => App.Shell.NavigateToWelcome();

    private async void OnCreateClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ErrorMessage = null;
        ViewModel.IsBusy = true;

        VaultSession? session = null;
        try
        {
            session = await VaultSession.CreateAsync(ViewModel.VaultPath, ViewModel.KeyPath);
            await ShowCreatedDialogAsync(session);

            App.Shell.EnterVault(session);
            session = null;
        }
        catch (VaultException ex)
        {
            StartupLog.Write("创建密码库失败", ex);
            ViewModel.ErrorMessage = ex.Message;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StartupLog.Write("创建密码库失败", ex);
            LogTargetState(ViewModel.VaultPath, "密码库");
            LogTargetState(ViewModel.KeyPath, "钥匙");

            ViewModel.ErrorMessage = ex is UnauthorizedAccessException
                ? $"创建失败：无法写入该位置。可能是文件夹权限不足，或 Windows「受控文件夹访问」拦截。请换一个文件夹（例如 D:\\ 下普通目录）或把本程序加入允许列表。\n原始错误：{ex.Message}"
                : $"创建失败：{ex.Message}";
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

    /// <summary>把目标文件与目录的状态写进日志，便于定位权限类失败。</summary>
    private static void LogTargetState(string path, string description)
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            var exists = File.Exists(path);
            var attributes = exists ? File.GetAttributes(path).ToString() : "不存在";
            var length = exists ? new FileInfo(path).Length : 0;

            StartupLog.Write(
                $"{description}目标：{path} 存在={exists} 长度={length} 属性={attributes} " +
                $"目录={directory} 目录存在={Directory.Exists(directory)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StartupLog.Write($"{description}目标状态读取失败：{path} {ex.Message}");
        }
    }

    private async Task ShowCreatedDialogAsync(VaultSession session)
    {
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock
        {
            Text = $"钥匙指纹：{session.KeyId}",
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
        });
        content.Children.Add(new TextBlock
        {
            Text = "请现在把钥匙文件复制一份到别处保管。钥匙丢失后，密码库将无法打开，也没有找回途径。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "密码库已创建",
            Content = content,
            CloseButtonText = "进入密码库",
            DefaultButton = ContentDialogButton.Close,
        };

        await dialog.ShowAsync();
    }
}
