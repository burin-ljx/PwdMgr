using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace PwdMgr.App.Services;

/// <summary>
/// 主题切换。WinUI 里改 <see cref="Application.RequestedTheme"/> 只对启动时生效，
/// 所以这里给每个窗口的根元素设置 <see cref="FrameworkElement.RequestedTheme"/>，
/// 主题资源与 ContentDialog 都会跟着变。
/// </summary>
public static class ThemeService
{
    private static readonly List<WeakReference<Window>> TrackedWindows = [];

    /// <summary>当前生效的窗口根元素主题。</summary>
    public static ThemeMode Current { get; private set; } = ThemeMode.System;

    /// <summary>启动时用保存的设置初始化（不写文件）。</summary>
    public static void Initialize(ThemeMode mode) => Current = mode;

    /// <summary>登记一个窗口，之后切换主题时会一并应用。</summary>
    public static void Register(Window window)
    {
        Cleanup();
        TrackedWindows.Add(new WeakReference<Window>(window));
        ApplyTo(window, Current);
    }

    /// <summary>切换主题并立即应用到所有已登记窗口，同时写入设置。</summary>
    public static void Apply(ThemeMode mode)
    {
        Current = mode;
        AppSettings.Current.Theme = mode;
        AppSettings.Current.Save();

        Cleanup();
        foreach (var window in EnumerateWindows())
        {
            ApplyTo(window, mode);
        }
    }

    private static void ApplyTo(Window window, ThemeMode mode)
    {
        if (window.Content is not FrameworkElement root)
        {
            return;
        }

        root.RequestedTheme = mode switch
        {
            ThemeMode.Light => ElementTheme.Light,
            ThemeMode.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };

        // 强制主题时 Mica 仍是系统的配色，会和内容对不上，改用主题背景色。
        if (window is MainWindow mainWindow)
        {
            mainWindow.ApplyBackdropForTheme(mode);
        }
    }

    private static IEnumerable<Window> EnumerateWindows()
    {
        foreach (var reference in TrackedWindows)
        {
            if (reference.TryGetTarget(out var window))
            {
                yield return window;
            }
        }
    }

    private static void Cleanup() => TrackedWindows.RemoveAll(reference => !reference.TryGetTarget(out _));

    /// <summary>把系统主题色转成对应的背景刷子（Mica 不可用或强制主题时使用）。</summary>
    internal static SolidColorBrush? ThemeBackground(ThemeMode mode)
    {
        if (mode == ThemeMode.System)
        {
            return null;
        }

        var resource = mode == ThemeMode.Dark
            ? "#FF202020"
            : "#FFF3F3F3";

        return new SolidColorBrush(ParseColor(resource));
    }

    private static Windows.UI.Color ParseColor(string hex)
    {
        var value = Convert.ToUInt32(hex.TrimStart('#'), 16);
        return Windows.UI.Color.FromArgb(
            (byte)((value >> 24) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF));
    }
}

