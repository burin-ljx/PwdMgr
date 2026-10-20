using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.UI.Dispatching;

namespace PwdMgr.App.Services;

/// <summary>
/// 剪贴板读写。复制密码时：
/// <list type="bullet">
///   <item>同时放入 <c>CanIncludeInClipboardHistory=0</c>、<c>CanUploadToCloudClipboard=0</c>，
///         让 Win+V 历史与云剪贴板尽量忽略这段内容（尽力而为，取决于系统版本）；</item>
///   <item>30 秒后自动清除，但只清除“仍然是我们放进去的内容”，不会误删用户后来复制的东西。</item>
/// </list>
/// </summary>
public static class ClipboardService
{
    private static readonly TimeSpan ClearDelay = TimeSpan.FromSeconds(30);

    private static readonly uint TextFormat = 13;
    private static readonly uint CanIncludeInClipboardHistoryFormat = RegisterClipboardFormatW("CanIncludeInClipboardHistory");
    private static readonly uint CanUploadToCloudClipboardFormat = RegisterClipboardFormatW("CanUploadToCloudClipboard");
    private static readonly uint ExcludeFromMonitorFormat = RegisterClipboardFormatW("ExcludeClipboardContentFromMonitorProcessing");

    private static DispatcherQueueTimer? _timer;
    private static byte[]? _digest;

    /// <summary>应用启动时调用一次，取得 UI 线程的计时器。</summary>
    public static void Initialize(DispatcherQueue dispatcherQueue)
    {
        if (_timer is not null)
        {
            return;
        }

        _timer = dispatcherQueue.CreateTimer();
        _timer.Interval = ClearDelay;
        _timer.IsRepeating = false;
        _timer.Tick += (_, _) => ClearIfOwned();
    }

    /// <summary>把文本写入剪贴板，并在 30 秒后尝试清除。返回 false 表示剪贴板被别的程序占用。</summary>
    public static bool TryCopy(string text, bool excludeFromHistory = true)
    {
        if (!TryOpenClipboard())
        {
            return false;
        }

        try
        {
            EmptyClipboard();

            if (!SetText(text))
            {
                return false;
            }

            if (excludeFromHistory)
            {
                SetDword(CanIncludeInClipboardHistoryFormat, 0);
                SetDword(CanUploadToCloudClipboardFormat, 0);
                SetDword(ExcludeFromMonitorFormat, 1);
            }

            _digest = Digest(text);
            _timer?.Stop();
            _timer?.Start();
            return true;
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>只清除仍然属于本程序的内容；用户后来复制了别的东西则保持原样。</summary>
    public static void ClearIfOwned()
    {
        _timer?.Stop();

        if (_digest is null || !TryOpenClipboard())
        {
            return;
        }

        try
        {
            var current = ReadText();
            if (current is null || !Digest(current).AsSpan().SequenceEqual(_digest))
            {
                return;
            }

            EmptyClipboard();
            _digest = null;
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>用户主动点击“清空剪贴板”：无条件清空。</summary>
    public static void Clear()
    {
        _timer?.Stop();
        _digest = null;

        if (!TryOpenClipboard())
        {
            return;
        }

        try
        {
            EmptyClipboard();
        }
        finally
        {
            CloseClipboard();
        }
    }

    private static bool TryOpenClipboard()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                return true;
            }

            Thread.Sleep(30);
        }

        return false;
    }

    private static bool SetText(string text)
    {
        var bytes = (text.Length + 1) * sizeof(char);
        var handle = GlobalAlloc(MoveableMemory, (UIntPtr)bytes);
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        var pointer = GlobalLock(handle);
        if (pointer == IntPtr.Zero)
        {
            GlobalFree(handle);
            return false;
        }

        try
        {
            var buffer = new char[text.Length + 1];
            text.CopyTo(buffer.AsSpan());
            Marshal.Copy(buffer, 0, pointer, buffer.Length);
        }
        finally
        {
            GlobalUnlock(handle);
        }

        // SetClipboardData 成功后内存所有权归系统，不能自己释放。
        if (SetClipboardData(TextFormat, handle) == IntPtr.Zero)
        {
            GlobalFree(handle);
            return false;
        }

        return true;
    }

    private static void SetDword(uint format, int value)
    {
        if (format == 0)
        {
            return;
        }

        var handle = GlobalAlloc(MoveableMemory, 4);
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var pointer = GlobalLock(handle);
        if (pointer == IntPtr.Zero)
        {
            GlobalFree(handle);
            return;
        }

        try
        {
            Marshal.WriteInt32(pointer, value);
        }
        finally
        {
            GlobalUnlock(handle);
        }

        if (SetClipboardData(format, handle) == IntPtr.Zero)
        {
            GlobalFree(handle);
        }
    }

    private static string? ReadText()
    {
        var handle = GetClipboardData(TextFormat);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        var pointer = GlobalLock(handle);
        if (pointer == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStringUni(pointer);
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    private static byte[] Digest(string text) => SHA256.HashData(Encoding.UTF8.GetBytes(text));

    private const uint MoveableMemory = 0x0042;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr memory);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterClipboardFormatW(string format);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr memory);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr memory);
}
