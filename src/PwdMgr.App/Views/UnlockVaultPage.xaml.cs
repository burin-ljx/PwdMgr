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
    }

    public UnlockVaultViewModel ViewModel { get; } = new();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string vaultPath && !string.IsNullOrWhiteSpace(vaultPath))
        {
            ViewModel.VaultPath = vaultPath;

            var suggestedKeyPath = FilePickerService.SuggestKeyPath(vaultPath);
            if (File.Exists(suggestedKeyPath))
            {
                ViewModel.KeyPath = suggestedKeyPath;
            }
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
