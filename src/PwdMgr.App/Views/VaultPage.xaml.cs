using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PwdMgr.App.ViewModels;
using PwdMgr.Core.Models;
using PwdMgr.Core.Vault;

namespace PwdMgr.App.Views;

public sealed partial class VaultPage : Page
{
    private VaultViewModel? _viewModel;
    private VaultSession? _session;
    private PasswordItem? _currentItem;
    private FormSnapshot? _baseline;
    private bool _isDirty;
    private bool _isLoading;
    private bool _suppressSelectionChange;

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

        _session = session;
        _viewModel = new VaultViewModel(session);
        VaultNameText.Text = _viewModel.VaultFileName;
        VaultMetaText.Text = _viewModel.MetaText;
        ItemList.ItemsSource = _viewModel.Items;
        StatusText.Text = _viewModel.StatusText;

        // 关窗时由 MainWindow 调用，避免把未保存的修改丢掉。
        App.Shell.PendingEditsCommitter = SaveCurrentAsync;

        UpdateEmptyState();
        LoadItem(null);
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        App.Shell.PendingEditsCommitter = null;
    }

    private async void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (_viewModel is null || _session is null)
        {
            return;
        }

        // 搜索会重建列表，先把未保存的修改写回，避免用户以为改动还在。
        if (!await SaveCurrentAsync())
        {
            return;
        }

        RefreshList(_currentItem?.Id);
    }

    private async void OnItemSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChange || _viewModel is null)
        {
            return;
        }

        var next = ItemList.SelectedItem as PasswordItem;
        if (ReferenceEquals(next, _currentItem))
        {
            return;
        }

        // 切换条目前自动保存，失败就退回原来的选中项。
        if (!await SaveCurrentAsync())
        {
            _suppressSelectionChange = true;
            ItemList.SelectedItem = _currentItem;
            _suppressSelectionChange = false;
            return;
        }

        LoadItem(next);
    }

    private void OnFieldChanged(object sender, TextChangedEventArgs e) => MarkDirty();

    private void OnPasswordChanged(object sender, RoutedEventArgs e) => MarkDirty();

    private void OnToggleRevealClick(object sender, RoutedEventArgs e)
    {
        EditPassword.PasswordRevealMode = RevealButton.IsChecked == true
            ? PasswordRevealMode.Visible
            : PasswordRevealMode.Hidden;
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e) => await SaveCurrentAsync();

    private void OnRevertClick(object sender, RoutedEventArgs e)
    {
        if (_currentItem is not null)
        {
            LoadItem(_currentItem);
        }
    }

    private async void OnLockClick(object sender, RoutedEventArgs e)
    {
        if (!await SaveCurrentAsync())
        {
            return;
        }

        await App.Shell.LockAsync();
    }

    private async Task<bool> SaveCurrentAsync()
    {
        if (_currentItem is null || _session is null || !_isDirty || _baseline is null)
        {
            return true;
        }

        var title = EditTitle.Text.Trim();
        if (title.Length == 0)
        {
            ShowError("标题不能为空。");
            EditTitle.Focus(FocusState.Programmatic);
            return false;
        }

        SaveErrorBar.IsOpen = false;

        var updated = new PasswordItem
        {
            Id = _currentItem.Id,
            Title = title,
            Username = EditUsername.Text,
            Password = EditPassword.Password,
            Url = EditUrl.Text,
            Notes = EditNotes.Text,
            Tags = ParseTags(EditTags.Text),
        };

        // Update 会就地更新内存中的同一条记录，然后由 VaultSession 加密写回文件。
        _session.Document.Update(updated);

        try
        {
            await _session.SaveAsync();
        }
        catch (VaultException ex)
        {
            ShowError(ex.Message);
            return false;
        }

        _isDirty = false;
        SaveBar.Visibility = Visibility.Collapsed;
        RefreshList(_currentItem.Id);
        return true;
    }

    private void RefreshList(Guid? selectedId)
    {
        if (_viewModel is null)
        {
            return;
        }

        _suppressSelectionChange = true;
        _viewModel.ApplySearch(SearchBox.Text);
        StatusText.Text = _viewModel.StatusText;
        UpdateEmptyState();

        var target = selectedId is { } id
            ? _viewModel.Items.FirstOrDefault(item => item.Id == id)
            : null;

        ItemList.SelectedItem = target;
        _suppressSelectionChange = false;

        LoadItem(target);
    }

    private void LoadItem(PasswordItem? item)
    {
        _isLoading = true;
        _currentItem = item;
        SaveErrorBar.IsOpen = false;
        _isDirty = false;
        SaveBar.Visibility = Visibility.Collapsed;

        if (item is null)
        {
            DetailPanel.Visibility = Visibility.Collapsed;
            NoSelectionPanel.Visibility = Visibility.Visible;
            _baseline = null;
            _isLoading = false;
            return;
        }

        DetailPanel.Visibility = Visibility.Visible;
        NoSelectionPanel.Visibility = Visibility.Collapsed;

        EditTitle.Text = item.Title;
        EditUsername.Text = item.Username;
        EditPassword.Password = item.Password;
        EditUrl.Text = item.Url;
        EditTags.Text = string.Join(", ", item.Tags);
        EditNotes.Text = item.Notes;
        DetailCreated.Text = $"创建于 {item.CreatedUtc.ToLocalTime():yyyy-MM-dd HH:mm}";

        RevealButton.IsChecked = false;
        EditPassword.PasswordRevealMode = PasswordRevealMode.Hidden;

        _baseline = CaptureForm();
        _isLoading = false;
    }

    private void MarkDirty()
    {
        if (_isLoading || _currentItem is null || _baseline is not { } baseline)
        {
            return;
        }

        var dirty = !CaptureForm().Equals(baseline);
        if (dirty == _isDirty)
        {
            return;
        }

        _isDirty = dirty;
        SaveBar.Visibility = dirty ? Visibility.Visible : Visibility.Collapsed;
    }

    private FormSnapshot CaptureForm() => new(
        EditTitle.Text.Trim(),
        EditUsername.Text.Trim(),
        EditPassword.Password,
        EditUrl.Text.Trim(),
        NormalizeTags(EditTags.Text),
        EditNotes.Text.Trim());

    private static string NormalizeTags(string text) =>
        string.Join(", ", ParseTags(text));

    private static List<string> ParseTags(string text) =>
        text.Split([',', '，'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => tag.Length > 0)
            .ToList();

    private void ShowError(string message)
    {
        SaveErrorBar.Message = message;
        SaveErrorBar.IsOpen = true;
    }

    private void UpdateEmptyState() =>
        EmptyListPanel.Visibility = ItemList.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>表单快照，用于判断“有没有改动”。</summary>
    private readonly record struct FormSnapshot(
        string Title,
        string Username,
        string Password,
        string Url,
        string Tags,
        string Notes);
}
