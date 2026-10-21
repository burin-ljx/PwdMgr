using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PwdMgr.App.Services;
using PwdMgr.App.Views;
using PwdMgr.Core.Vault;

namespace PwdMgr.App;

/// <summary>应用主窗口。它只负责窗口外观与承载页面，业务状态放在 <see cref="AppShell"/>。</summary>
public sealed partial class MainWindow : Window
{
    private bool _allowClose;

    public MainWindow()
    {
        StartupLog.Write("MainWindow 构造");
        InitializeComponent();
        StartupLog.Write("MainWindow InitializeComponent 完成");

        Title = "PwdMgr";
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        ConfigureWindowChrome();
        ContentFrame.Navigate(typeof(WelcomePage));
        AppWindow.Closing += OnAppWindowClosing;
        Closed += OnClosed;

        // 应用保存的主题（跟随系统 / 浅色 / 深色）。
        ThemeService.Register(this);

        StartupLog.Write("MainWindow 构造完成");
    }

    /// <summary>页面导航容器。</summary>
    public Frame RootFrame => ContentFrame;

    private void ConfigureWindowChrome()
    {
        var titleBar = AppWindow.TitleBar;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        TryEnableMica();

        WindowHelper.CenterAndResize(this, 1120, 760);

        // 页面内容最窄需要约 860 逻辑像素（创建/打开页固定 760 + 内边距），
        // 低于这个尺寸会被裁切，这里直接限制窗口最小值。
        WindowHelper.SetMinimumSize(this, 900, 640);
    }

    /// <summary>
    /// 跟随系统时用 Mica；强制浅色/深色时 Mica 仍是系统配色，会与内容对不上，
    /// 改用对应的纯色背景。标题栏的最小化/最大化/关闭按钮颜色也在这里同步。
    /// </summary>
    public void ApplyBackdropForTheme(ThemeMode mode)
    {
        if (mode == ThemeMode.System)
        {
            RootGrid.Background = null;
            TryEnableMica();
            ApplyTitleBarTheme(TitleBarTheme.UseDefaultAppMode);
            return;
        }

        SystemBackdrop = null;
        RootGrid.Background = ThemeService.ThemeBackground(mode);
        ApplyTitleBarTheme(mode == ThemeMode.Dark ? TitleBarTheme.Dark : TitleBarTheme.Light);
    }

    /// <summary>让标题栏（含系统绘制的窗口按钮）跟随应用主题。</summary>
    private void ApplyTitleBarTheme(TitleBarTheme theme)
    {
        var titleBar = AppWindow.TitleBar;
        titleBar.PreferredTheme = theme;

        // 旧版本没有 PreferredTheme 生效时至少把按钮颜色设对。
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        if (theme == TitleBarTheme.UseDefaultAppMode)
        {
            titleBar.ButtonForegroundColor = null;
            titleBar.ButtonHoverForegroundColor = null;
            titleBar.ButtonPressedForegroundColor = null;
            titleBar.ButtonHoverBackgroundColor = null;
            titleBar.ButtonPressedBackgroundColor = null;
            return;
        }

        var foreground = theme == TitleBarTheme.Dark ? Colors.White : Colors.Black;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonPressedForegroundColor = foreground;
        titleBar.ButtonHoverBackgroundColor = theme == TitleBarTheme.Dark
            ? Windows.UI.Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF)
            : Windows.UI.Color.FromArgb(0x1F, 0x00, 0x00, 0x00);
        titleBar.ButtonPressedBackgroundColor = theme == TitleBarTheme.Dark
            ? Windows.UI.Color.FromArgb(0x3F, 0xFF, 0xFF, 0xFF)
            : Windows.UI.Color.FromArgb(0x3F, 0x00, 0x00, 0x00);
    }

    private void TryEnableMica()
    {
        try
        {
            SystemBackdrop = new MicaBackdrop();
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            // 旧系统不支持 Mica 时退回不透明背景，避免窗口变透明。
            RootGrid.Background = (Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"];
        }
    }

    /// <summary>
    /// 关闭前先让主界面提交未保存的修改。保存失败时取消关闭，让用户看到错误提示。
    /// </summary>
    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose || App.Shell.PendingEditsCommitter is not { } commit)
        {
            return;
        }

        args.Cancel = true;

        var saved = false;
        try
        {
            saved = await commit();
        }
        catch (VaultException)
        {
            saved = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            saved = false;
        }

        if (saved)
        {
            _allowClose = true;
            Close();
        }
    }

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        if (App.Shell.Session is { } session)
        {
            await session.DisposeAsync();
        }
    }
}


