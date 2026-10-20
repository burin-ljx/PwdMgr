using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace PwdMgr.App.Views;

public sealed partial class WelcomePage : Page
{
    public WelcomePage()
    {
        InitializeComponent();
    }

    private void OnCreateClick(object sender, RoutedEventArgs e) => App.Shell.NavigateToCreate();

    private void OnOpenClick(object sender, RoutedEventArgs e) => App.Shell.NavigateToUnlock();
}
