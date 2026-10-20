using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PwdMgr.App.ViewModels;
using PwdMgr.Core.Models;

namespace PwdMgr.App.Views;

public sealed partial class VaultPage : Page
{
    private const string MaskedPassword = "••••••••••";

    private VaultViewModel? _viewModel;
    private PasswordItem? _selectedItem;

    public VaultPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        var session = App.Shell.Session;
        if (session is null)
        {
            App.Shell.NavigateToWelcome();
            return;
        }

        _viewModel = new VaultViewModel(session);
        VaultNameText.Text = _viewModel.VaultFileName;
        VaultMetaText.Text = _viewModel.MetaText;
        ItemList.ItemsSource = _viewModel.Items;
        StatusText.Text = _viewModel.StatusText;

        UpdateEmptyState();
        ShowDetail(null);
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.ApplySearch(SearchBox.Text);
        StatusText.Text = _viewModel.StatusText;
        UpdateEmptyState();
        ShowDetail(null);
    }

    private void OnItemSelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ShowDetail(ItemList.SelectedItem as PasswordItem);

    private void OnTogglePasswordClick(object sender, RoutedEventArgs e)
    {
        if (_selectedItem is null)
        {
            return;
        }

        DetailPassword.Text = EyeButton.IsChecked == true
            ? _selectedItem.Password
            : MaskedPassword;
    }

    private async void OnLockClick(object sender, RoutedEventArgs e) => await App.Shell.LockAsync();

    private void UpdateEmptyState() =>
        EmptyListPanel.Visibility = ItemList.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void ShowDetail(PasswordItem? item)
    {
        _selectedItem = item;
        EyeButton.IsChecked = false;

        DetailPanel.Visibility = item is null ? Visibility.Collapsed : Visibility.Visible;
        NoSelectionPanel.Visibility = item is null ? Visibility.Visible : Visibility.Collapsed;

        if (item is null)
        {
            return;
        }

        DetailTitle.Text = item.Title;
        DetailCreated.Text = $"创建于 {item.CreatedUtc.ToLocalTime():yyyy-MM-dd HH:mm}";
        DetailUsername.Text = OrDash(item.Username);
        DetailPassword.Text = MaskedPassword;
        DetailUrl.Text = OrDash(item.Url);
        DetailTags.Text = item.Tags.Count == 0 ? "—" : string.Join(" · ", item.Tags);
        DetailNotes.Text = OrDash(item.Notes);
    }

    private static string OrDash(string value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
}
