using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using PwdMgr.App.Services;
using PwdMgr.App.ViewModels;
using PwdMgr.Core.Models;
using PwdMgr.Core.Security;
using PwdMgr.Core.Vault;

namespace PwdMgr.App.Views;

public sealed partial class VaultPage : Page
{
    private VaultViewModel? _viewModel;
    private VaultSession? _session;
    private PasswordItem? _currentItem;
    private FormSnapshot? _baseline;
    private bool _isDraft;
    private bool _isDirty;
    private bool _isLoading;
    private bool _suppressSelectionChange;
    private bool _lastSaveCreatedItem;
    private DispatcherQueueTimer? _transientTimer;

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

        // 关窗或自动锁定时由外壳调用，先提交未保存的修改。
        App.Shell.PendingEditsCommitter = () => SaveCurrentAsync(allowDiscardEmptyDraft: true);

        UpdateEmptyState();
        LoadItem(null);

        if (App.Shell.Notice is { } notice)
        {
            App.Shell.Notice = null;
            ShowTransient(notice, InfoBarSeverity.Success);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        App.Shell.PendingEditsCommitter = null;
    }

    private async Task NewItemAsync()
    {
        if (_session is null)
        {
            return;
        }

        if (!await SaveCurrentAsync(allowDiscardEmptyDraft: false))
        {
            return;
        }

        StartDraft();
    }

    private async void OnNewItemClick(object sender, RoutedEventArgs e) => await NewItemAsync();

    private void OnNewItemAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _ = NewItemAsync();
    }

    private void OnGeneratorClick(object sender, RoutedEventArgs e) => App.Shell.ShowGenerator();

    private void OnGeneratorAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;

        if (_session is null)
        {
            return;
        }

        App.Shell.ShowGenerator();
    }

    private void OnFocusSearchAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        SearchBox.Focus(FocusState.Programmatic);
    }

    private async Task LockAsync()
    {
        if (!await SaveCurrentAsync(allowDiscardEmptyDraft: true))
        {
            return;
        }

        await App.Shell.LockAsync();
    }

    private void OnLockAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        _ = LockAsync();
    }

    private async void OnSaveAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await SaveCurrentAsync(allowDiscardEmptyDraft: false);
    }

    private async void OnDiceClick(object sender, RoutedEventArgs e)
    {
        if (_session is null || (_currentItem is null && !_isDraft))
        {
            return;
        }

        if (EditPassword.Password.Length > 0)
        {
            var confirmed = await ConfirmAsync(
                "覆盖当前密码？",
                "密码框里已有内容，生成的新密码会替换它。",
                "覆盖");

            if (!confirmed)
            {
                return;
            }
        }

        try
        {
            EditPassword.Password = PasswordGenerator.Generate(App.Shell.GeneratorOptions);
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            ShowError(ex.Message);
            return;
        }

        MarkDirty();
        ShowTransient("已生成新密码，保存后写入密码库。", InfoBarSeverity.Success);
    }

    private void OnCopyUsernameClick(object sender, RoutedEventArgs e) => CopyToClipboard(EditUsername.Text, "用户名");

    private void OnCopyPasswordClick(object sender, RoutedEventArgs e) => CopyToClipboard(EditPassword.Password, "密码");

    private void OnClearClipboardClick(object sender, RoutedEventArgs e)
    {
        ClipboardService.Clear();
        ShowTransient("剪贴板已清空。", InfoBarSeverity.Success);
    }

    private async void OnBackupClick(object sender, RoutedEventArgs e)
    {
        if (_session is null || !await SaveCurrentAsync(allowDiscardEmptyDraft: true))
        {
            return;
        }

        var suggested = $"{Path.GetFileNameWithoutExtension(_session.VaultPath)}-备份-{DateTime.Now:yyyyMMdd-HHmm}.pwdb";
        var target = FilePickerService.PickBackupTarget(App.ShellWindow, suggested);

        if (target is null)
        {
            return;
        }

        try
        {
            var size = await _session.CreateBackupAsync(target);
            ShowTransient(
                $"备份已保存（{size / 1024.0:0.#} KB）：{target}。请同时妥善保管钥匙文件。",
                InfoBarSeverity.Success);
        }
        catch (VaultException ex)
        {
            ShowError($"备份失败：{ex.Message}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ShowError($"备份失败：{ex.Message}");
        }
    }

    private async void OnVerifyClick(object sender, RoutedEventArgs e)
    {
        if (_session is null || !await SaveCurrentAsync(allowDiscardEmptyDraft: true))
        {
            return;
        }

        try
        {
            var report = await _session.VerifyAsync();
            ShowTransient(
                $"校验通过：有效条目 {report.ActiveCount} 条，回收站 {report.DeletedCount} 条，文件 {report.FileSize / 1024.0:0.#} KB。",
                InfoBarSeverity.Success);
        }
        catch (VaultException ex)
        {
            ShowError($"校验失败：{ex.Message}");
        }
    }

    private async void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        if (_session is null || !await SaveCurrentAsync(allowDiscardEmptyDraft: true))
        {
            return;
        }

        var backup = FilePickerService.PickBackupToRestore(App.ShellWindow);
        if (backup is null)
        {
            return;
        }

        var confirmed = await ConfirmAsync(
            "从备份恢复？",
            $"当前密码库内容将被覆盖：{backup}{Environment.NewLine}覆盖前的版本会保留为 .bak 文件。",
            "恢复");

        if (!confirmed)
        {
            return;
        }

        var vaultPath = _session.VaultPath;
        var keyPath = _session.KeyFilePath;

        try
        {
            await VaultSession.RestoreFromFileAsync(vaultPath, keyPath, backup);
        }
        catch (VaultException ex)
        {
            ShowError($"恢复失败：{ex.Message}");
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ShowError($"恢复失败：{ex.Message}");
            return;
        }

        await App.Shell.RestartAsync("已从备份恢复。");
    }

    private void CopyToClipboard(string value, string what)
    {
        if (value.Length == 0)
        {
            ShowTransient($"没有可复制的{what}。", InfoBarSeverity.Warning);
            return;
        }

        var copied = ClipboardService.TryCopy(value);
        ShowTransient(
            copied ? $"已复制{what}，30 秒后自动清除。" : "剪贴板被其他程序占用，请稍后重试。",
            copied ? InfoBarSeverity.Success : InfoBarSeverity.Error);
    }

    /// <summary>二次确认。destructive 为 true 时确认按钮用红色（删除类操作），且不设默认按钮。</summary>
    private async Task<bool> ConfirmAsync(string title, string message, string primaryText, bool destructive = false)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = primaryText,
            CloseButtonText = "取消",
            DefaultButton = destructive ? ContentDialogButton.None : ContentDialogButton.Primary,
        };

        if (destructive)
        {
            dialog.PrimaryButtonStyle = (Style)Application.Current.Resources["DestructiveButtonStyle"];
        }

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void ShowTransient(string message, InfoBarSeverity severity)
    {
        TransientBar.Severity = severity;
        TransientBar.Message = message;
        TransientBar.IsOpen = true;

        if (_transientTimer is null)
        {
            _transientTimer = DispatcherQueue.CreateTimer();
            _transientTimer.Interval = TimeSpan.FromSeconds(4);
            _transientTimer.IsRepeating = false;
            _transientTimer.Tick += (_, _) => TransientBar.IsOpen = false;
        }

        _transientTimer.Stop();
        _transientTimer.Start();
    }

    private async void OnTrashClick(object sender, RoutedEventArgs e)
    {
        if (!await SaveCurrentAsync(allowDiscardEmptyDraft: true))
        {
            return;
        }

        App.Shell.NavigateToTrash();
    }

    private async void OnDeleteItemClick(object sender, RoutedEventArgs e)
    {
        if (_session is null || sender is not FrameworkElement { DataContext: PasswordItem item })
        {
            return;
        }

        if (!await SaveCurrentAsync(allowDiscardEmptyDraft: true))
        {
            return;
        }

        var confirmed = await ConfirmAsync(
            "删除这条记录？",
            $"“{item.Title}”会被移到回收站，可以再恢复。",
            "删除",
            destructive: true);

        if (!confirmed)
        {
            return;
        }

        SaveErrorBar.IsOpen = false;

        if (!_session.Document.MoveToTrash(item.Id))
        {
            RefreshList(_currentItem?.Id);
            return;
        }

        try
        {
            await _session.SaveAsync();
        }
        catch (VaultException ex)
        {
            _session.Document.Restore(item.Id);
            ShowError(ex.Message);
            RefreshList(_currentItem?.Id);
            return;
        }

        RefreshList(ReferenceEquals(_currentItem, item) ? null : _currentItem?.Id);
    }

    private async void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (_viewModel is null || _session is null)
        {
            return;
        }

        // 搜索会重建列表，先把未保存的修改写回。
        if (!await SaveCurrentAsync(allowDiscardEmptyDraft: true))
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

        if (!_isDraft && ReferenceEquals(next, _currentItem))
        {
            return;
        }

        _lastSaveCreatedItem = false;
        if (!await SaveCurrentAsync(allowDiscardEmptyDraft: true))
        {
            _suppressSelectionChange = true;
            ItemList.SelectedItem = _currentItem;
            _suppressSelectionChange = false;
            return;
        }

        // 新建草稿被保存成了条目：保持选中新建的那条，忽略这次切换。
        if (_lastSaveCreatedItem)
        {
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

    private async void OnSaveClick(object sender, RoutedEventArgs e) =>
        await SaveCurrentAsync(allowDiscardEmptyDraft: false);

    private void OnRevertClick(object sender, RoutedEventArgs e)
    {
        if (_isDraft)
        {
            _isDraft = false;
            LoadItem(null);
            return;
        }

        if (_currentItem is not null)
        {
            LoadItem(_currentItem);
        }
    }

    private async void OnLockClick(object sender, RoutedEventArgs e) => await LockAsync();

    /// <summary>
    /// 提交当前修改。新建草稿会变成正式条目；标题为空的草稿在允许的情况下直接丢弃。
    /// 返回 false 表示保存失败，调用方应保持当前界面。
    /// </summary>
    private async Task<bool> SaveCurrentAsync(bool allowDiscardEmptyDraft)
    {
        if (_session is null)
        {
            return true;
        }

        if (_isDraft)
        {
            return await CommitDraftAsync(allowDiscardEmptyDraft);
        }

        if (_currentItem is null || !_isDirty || _baseline is null)
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
        RefreshList(_currentItem.Id);
        return true;
    }

    private async Task<bool> CommitDraftAsync(bool allowDiscardEmptyDraft)
    {
        if (_session is null)
        {
            return true;
        }

        var title = EditTitle.Text.Trim();
        if (title.Length == 0)
        {
            if (!allowDiscardEmptyDraft)
            {
                ShowError("标题不能为空。");
                EditTitle.Focus(FocusState.Programmatic);
                return false;
            }

            _isDraft = false;
            LoadItem(null);
            return true;
        }

        SaveErrorBar.IsOpen = false;

        var created = _session.Document.Add(
            title,
            EditUsername.Text,
            EditPassword.Password,
            EditUrl.Text,
            EditNotes.Text,
            ParseTags(EditTags.Text));

        try
        {
            await _session.SaveAsync();
        }
        catch (VaultException ex)
        {
            // 保存失败：把刚加进内存的条目撤掉，避免界面和磁盘不一致。
            _session.Document.Purge(created.Id);
            ShowError(ex.Message);
            return false;
        }

        _isDraft = false;
        _lastSaveCreatedItem = true;
        RefreshList(created.Id);
        return true;
    }

    private void StartDraft()
    {
        _isLoading = true;
        _isDraft = true;
        _currentItem = null;
        _isDirty = false;
        _baseline = EmptySnapshot;
        SaveErrorBar.IsOpen = false;

        DetailPanel.Visibility = Visibility.Visible;
        NoSelectionPanel.Visibility = Visibility.Collapsed;

        EditTitle.Text = string.Empty;
        EditUsername.Text = string.Empty;
        EditPassword.Password = string.Empty;
        EditUrl.Text = string.Empty;
        EditTags.Text = string.Empty;
        EditNotes.Text = string.Empty;
        DetailCreated.Text = "新条目（尚未保存）";

        RevealButton.IsChecked = false;
        EditPassword.PasswordRevealMode = PasswordRevealMode.Hidden;

        UpdateActionBar();
        _isLoading = false;

        EditTitle.Focus(FocusState.Programmatic);
    }

    private void LoadItem(PasswordItem? item)
    {
        _isLoading = true;
        _currentItem = item;
        _isDraft = false;
        _isDirty = false;
        SaveErrorBar.IsOpen = false;

        if (item is null)
        {
            DetailPanel.Visibility = Visibility.Collapsed;
            NoSelectionPanel.Visibility = Visibility.Visible;
            _baseline = null;
            UpdateActionBar();
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
        UpdateActionBar();
        _isLoading = false;
    }

    private void MarkDirty()
    {
        if (_isLoading || _baseline is not { } baseline)
        {
            return;
        }

        if (!_isDraft && _currentItem is null)
        {
            return;
        }

        var dirty = !CaptureForm().Equals(baseline);
        if (dirty == _isDirty)
        {
            return;
        }

        _isDirty = dirty;
        UpdateActionBar();
    }

    private void UpdateActionBar()
    {
        var hasDetail = _isDraft || _currentItem is not null;

        ActionBar.Visibility = hasDetail ? Visibility.Visible : Visibility.Collapsed;
        SaveActions.Visibility = _isDraft || _isDirty ? Visibility.Visible : Visibility.Collapsed;
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

    private FormSnapshot CaptureForm() => new(
        EditTitle.Text.Trim(),
        EditUsername.Text.Trim(),
        EditPassword.Password,
        EditUrl.Text.Trim(),
        NormalizeTags(EditTags.Text),
        EditNotes.Text.Trim());

    private static FormSnapshot EmptySnapshot => new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

    private static string NormalizeTags(string text) => string.Join(", ", ParseTags(text));

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
