using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;

namespace PwdMgr.App.Services;

/// <summary>
/// 闲置或锁屏时请求锁定。每 5 秒检查一次：
/// <list type="bullet">
///   <item>系统锁屏：当前输入桌面变成 Winlogon；</item>
///   <item>闲置：<see cref="GetLastInputInfo"/> 记录的键盘鼠标空闲时长超过阈值。</item>
/// </list>
/// 触发后不会重复请求，直到用户重新有输入（<see cref="ResetTrigger"/> 或自动复位）。
/// </summary>
internal sealed class AutoLockService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

    private readonly DispatcherQueueTimer _timer;
    private readonly TimeSpan _idleLimit;
    private bool _requested;

    public AutoLockService(DispatcherQueue dispatcherQueue, TimeSpan idleLimit)
    {
        _idleLimit = idleLimit;
        _timer = dispatcherQueue.CreateTimer();
        _timer.Interval = CheckInterval;
        _timer.IsRepeating = true;
        _timer.Tick += OnTick;
    }

    /// <summary>需要锁定时触发（在 UI 线程上）。</summary>
    public event Action? LockRequested;

    public void Start()
    {
        _requested = false;
        _timer.Start();
        StartupLog.Write($"自动锁定已启动，闲置阈值 {_idleLimit.TotalSeconds:0} 秒，检查间隔 {CheckInterval.TotalSeconds:0} 秒");
    }

    public void Stop() => _timer.Stop();

    /// <summary>保存失败等情况下放弃本次锁定，并允许之后重新触发。</summary>
    public void ResetTrigger() => _requested = false;

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        if (_requested)
        {
            // 用户重新有操作后，允许下一次再触发。
            if (!IsSessionLocked() && GetIdleTime() < _idleLimit / 2)
            {
                _requested = false;
            }

            return;
        }

        if (IsSessionLocked() || GetIdleTime() >= _idleLimit)
        {
            _requested = true;
            StartupLog.Write($"自动锁定触发：闲置 {GetIdleTime().TotalSeconds:0} 秒，锁屏={IsSessionLocked()}");
            LockRequested?.Invoke();
        }
    }

    private static TimeSpan GetIdleTime()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        var idleMilliseconds = unchecked((uint)Environment.TickCount - info.Time);
        return TimeSpan.FromMilliseconds(idleMilliseconds);
    }

    private static bool IsSessionLocked()
    {
        var desktop = OpenInputDesktop(0, false, DesktopReadObjects);
        if (desktop == IntPtr.Zero)
        {
            return true;
        }

        try
        {
            var buffer = new char[256];
            if (!GetUserObjectInformationW(desktop, UoiName, buffer, buffer.Length * sizeof(char), out _))
            {
                return false;
            }

            var name = new string(buffer).TrimEnd('\0');
            return string.Equals(name, "Winlogon", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CloseDesktop(desktop);
        }
    }

    private const uint DesktopReadObjects = 0x0001;
    private const int UoiName = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint desiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseDesktop(IntPtr desktop);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetUserObjectInformationW(IntPtr handle, int index, [Out] char[] info, int length, out int needed);
}
