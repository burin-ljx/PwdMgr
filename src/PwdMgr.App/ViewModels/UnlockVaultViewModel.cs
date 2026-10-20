using PwdMgr.App.Common;

namespace PwdMgr.App.ViewModels;

/// <summary>解锁页面的状态。</summary>
public sealed class UnlockVaultViewModel : ObservableObject
{
    private string _vaultPath = string.Empty;
    private string _keyPath = string.Empty;
    private bool _isBusy;
    private string? _errorMessage;

    public string VaultPath
    {
        get => _vaultPath;
        set
        {
            if (SetProperty(ref _vaultPath, value))
            {
                OnPropertyChanged(nameof(CanOpen));
            }
        }
    }

    public string KeyPath
    {
        get => _keyPath;
        set
        {
            if (SetProperty(ref _keyPath, value))
            {
                OnPropertyChanged(nameof(CanOpen));
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanOpen));
            }
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool CanOpen =>
        !IsBusy
        && !string.IsNullOrWhiteSpace(VaultPath)
        && !string.IsNullOrWhiteSpace(KeyPath);
}
