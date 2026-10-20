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

    private async void OnPickVaultClick(object sender, RoutedEventArgs e)
    {
        var path = await FilePickerService.PickVaultToCreateAsync(App.ShellWindow, "我的密码库");
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

    private async void OnPickKeyClick(object sender, RoutedEventArgs e)
    {
        var path = await FilePickerService.PickKeyToCreateAsync(App.ShellWindow, "我的钥匙");
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
            ViewModel.ErrorMessage = ex.Message;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ViewModel.ErrorMessage = $"创建失败：{ex.Message}";
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
