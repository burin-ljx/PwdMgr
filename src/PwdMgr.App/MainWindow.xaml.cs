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
        StartupLog.Write("MainWindow 构造完成");
    }

    /// <summary>页面导航容器。</summary>
    public Frame RootFrame => ContentFrame;

    private void ConfigureWindowChrome()
    {
        var titleBar = AppWindow.TitleBar;
        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        try
        {
            SystemBackdrop = new MicaBackdrop();
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException)
        {
            // 旧系统不支持 Mica 时退回不透明背景，避免窗口变透明。
            RootGrid.Background = (Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"];
        }

        WindowHelper.CenterAndResize(this, 1120, 760);
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
