using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using PwdMgr.App.Views;
using PwdMgr.Core.Vault;

namespace PwdMgr.App.Services;

/// <summary>
/// 应用级外壳：持有当前解锁的 <see cref="VaultSession"/>，并在页面之间导航。
/// 页面不要自己保存会话，统一从这里取，避免上锁后仍有页面握着密钥。
/// </summary>
public sealed class AppShell(MainWindow window)
{
    /// <summary>当前解锁的密码库；未解锁时为 <see langword="null"/>。</summary>
    public VaultSession? Session { get; private set; }

    private Frame Frame => window.RootFrame;

    /// <summary>打开一个已解锁的会话并进入主界面。</summary>
    public void EnterVault(VaultSession session)
    {
        Session = session;
        Frame.Navigate(typeof(VaultPage));
    }

    public void NavigateToWelcome() => Frame.Navigate(typeof(WelcomePage));

    public void NavigateToCreate() => Frame.Navigate(typeof(CreateVaultPage));

    public void NavigateToUnlock(string? vaultPath = null) => Frame.Navigate(typeof(UnlockVaultPage), vaultPath);

    /// <summary>上锁：销毁会话并回到解锁页。</summary>
    public async Task LockAsync()
    {
        var session = Session;
        if (session is null)
        {
            return;
        }

        Session = null;
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

            EnterVault(session);
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
}
