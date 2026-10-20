using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PwdMgr.App.Common;

/// <summary>
/// 极简的 <see cref="INotifyPropertyChanged"/> 基类。界面绑定只需要属性变更通知，
/// 不额外引入 MVVM 框架依赖。
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
