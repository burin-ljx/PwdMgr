using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using PwdMgr.App.Views;
using PwdMgr.Core.Security;
using PwdMgr.Core.Vault;

namespace PwdMgr.App.Services;

/// <summary>
/// 应用级外壳：持有当前解锁的 <see cref="VaultSession"/>，并在页面之间导航。
/// 页面不要自己保存会话，统一从这里取，避免上锁后仍有页面握着密钥。
/// </summary>
public sealed class AppShell
{
    /// <summary>回收站条目的保留时长。</summary>
    private static readonly TimeSpan TrashRetention = TimeSpan.FromDays(30);

    private readonly MainWindow _window;
    private readonly AutoLockService _autoLock;
    private GeneratorWindow? _generatorWindow;

    public AppShell(MainWindow window)
    {
        _window = window;
        _autoLock = new AutoLockService(window.DispatcherQueue, ReadIdleLimit());
        _autoLock.LockRequested += OnAutoLockRequested;
        ClipboardService.Initialize(window.DispatcherQueue);
    }

    /// <summary>密码生成器选项，主界面“骰子”按钮与生成器窗口共用。</summary>
    public PasswordOptions GeneratorOptions { get; set; } = new();

    /// <summary>打开（或激活）独立的密码生成器窗口。</summary>
    public void ShowGenerator()
    {
        if (_generatorWindow is not null)
        {
            _generatorWindow.Activate();
            return;
        }

        try
        {
            _generatorWindow = new GeneratorWindow();
        }
        catch (Exception ex)
        {
            StartupLog.Write("生成器窗口创建失败", ex);
            throw;
        }

        _generatorWindow.Closed += (_, _) => _generatorWindow = null;
        _generatorWindow.Activate();
    }

    /// <summary>当前解锁的密码库；未解锁时为 <see langword="null"/>。</summary>
    public VaultSession? Session { get; private set; }

    /// <summary>
    /// 由主界面注册的“提交未保存修改”回调。关窗或上锁前由外壳调用，
    /// 返回 false 表示保存失败，调用方应保持窗口打开。
    /// </summary>
    public Func<Task<bool>>? PendingEditsCommitter { get; set; }

    private Frame Frame => _window.RootFrame;

    /// <summary>打开一个已解锁的会话并进入主界面（顺带清理过期的回收站条目）。</summary>
    public async Task EnterVaultAsync(VaultSession session)
    {
        Session = session;

        if (session.Document.PurgeExpiredTrash(TrashRetention) > 0)
        {
            try
            {
                await session.SaveAsync();
            }
            catch (VaultException ex)
            {
                StartupLog.Write("清理回收站失败", ex);
            }
        }

        Frame.Navigate(typeof(VaultPage));
        _autoLock.Start();
    }

    public void NavigateToWelcome() => Frame.Navigate(typeof(WelcomePage));

    public void NavigateToCreate() => Frame.Navigate(typeof(CreateVaultPage));

    public void NavigateToUnlock(string? vaultPath = null) => Frame.Navigate(typeof(UnlockVaultPage), vaultPath);

    public void NavigateToTrash() => Frame.Navigate(typeof(TrashPage));

    public void NavigateBackToVault() => Frame.Navigate(typeof(VaultPage));

    /// <summary>上锁：销毁会话并回到解锁页。</summary>
    public async Task LockAsync()
    {
        var session = Session;
        if (session is null)
        {
            return;
        }

        _autoLock.Stop();
        ClipboardService.ClearIfOwned();
        Session = null;
        PendingEditsCommitter = null;
        var vaultPath = session.VaultPath;

        await session.DisposeAsync();
        NavigateToUnlock(vaultPath);
    }

    /// <summary>处理启动参数：支持 <c>PwdMgr.App.exe &lt;密码库&gt; &lt;钥匙&gt;</c> 直接打开（便于验收与调试）。</summary>
    public async Task StartAsync(IReadOnlyList<string> arguments)
    {
        if (arguments.Count < 2)
        {
            return;
        }

        StartupLog.Write("StartAsync：按启动参数打开密码库");

        try
        {
            var session = await VaultSession.OpenAsync(arguments[0], arguments[1]);
            StartupLog.Write("StartAsync：打开成功，准备进入主界面");

            await EnterVaultAsync(session);
            StartupLog.Write("StartAsync：已导航到主界面");
        }
        catch (VaultException ex)
        {
            StartupLog.Write($"StartAsync：打开失败 {ex.Code} {ex.Message}");
            NavigateToUnlock(arguments[0]);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                      or InvalidOperationException or XamlParseException)
        {
            StartupLog.Write("StartAsync：意外失败", ex);
            NavigateToUnlock(arguments[0]);
        }
    }

    private async void OnAutoLockRequested()
    {
        StartupLog.Write("OnAutoLockRequested：开始处理");

        if (Session is null)
        {
            return;
        }

        if (PendingEditsCommitter is { } commit && !await commit())
        {
            StartupLog.Write("OnAutoLockRequested：提交修改失败，暂不锁定");
            // 保存失败时不锁定，但也不算“已锁定”，下次闲置会再次尝试。
            _autoLock.ResetTrigger();
            return;
        }

        StartupLog.Write("OnAutoLockRequested：执行锁定");
        await LockAsync();
    }

    /// <summary>闲置锁定阈值，默认 5 分钟；开发时可用 PWDMR_IDLE_SECONDS 覆盖。</summary>
    private static TimeSpan ReadIdleLimit()
    {
        var raw = Environment.GetEnvironmentVariable("PWDMR_IDLE_SECONDS");
        return int.TryParse(raw, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.FromMinutes(5);
    }
}
