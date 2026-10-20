using Microsoft.UI.Xaml;
using PwdMgr.App.Services;

namespace PwdMgr.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        StartupLog.Write("App 构造");
        StartupDiagnostics.Log();
        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        InitializeComponent();
    }

    /// <summary>主窗口，供页面获取窗口句柄与导航框架。</summary>
    public static MainWindow ShellWindow { get; private set; } = null!;

    /// <summary>应用级外壳服务：持有当前解锁的密码库会话，负责页面导航。</summary>
    public static AppShell Shell { get; private set; } = null!;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        StartupLog.Write("OnLaunched 开始");

        ThemeService.Initialize(AppSettings.Current.Theme);

        _window = new MainWindow();
        ShellWindow = (MainWindow)_window;
        Shell = new AppShell(ShellWindow);

        StartupLog.Write("窗口创建完成，准备激活");
        ShellWindow.Activate();

        var startupArguments = Environment.GetCommandLineArgs().Skip(1).ToArray();
        StartupLog.Write($"启动参数：{startupArguments.Length} 个");
        _ = Shell.StartAsync(startupArguments);
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e) =>
        StartupLog.Write("XAML 未处理异常", e.Exception);

    private static void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            StartupLog.Write("AppDomain 未处理异常", exception);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) =>
        StartupLog.Write("未观察的任务异常", e.Exception);
}
