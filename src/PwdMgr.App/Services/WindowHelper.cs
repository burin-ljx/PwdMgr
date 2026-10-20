using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace PwdMgr.App.Services;

/// <summary>窗口尺寸与位置相关的 Win32 辅助方法。</summary>
internal static class WindowHelper
{
    /// <summary>按逻辑像素缩放后居中窗口。</summary>
    public static void CenterAndResize(Window window, int logicalWidth, int logicalHeight)
    {
        var handle = WindowNative.GetWindowHandle(window);
        var scale = GetDpiForWindow(handle) / 96.0;

        var width = (int)(logicalWidth * scale);
        var height = (int)(logicalHeight * scale);

        var appWindow = window.AppWindow;
        var workArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary).WorkArea;

        var x = workArea.X + ((workArea.Width - width) / 2);
        var y = workArea.Y + ((workArea.Height - height) / 2);

        appWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    /// <summary>限制窗口的最小尺寸（逻辑像素），避免被拖窄后内容被裁切。</summary>
    public static void SetMinimumSize(Window window, int logicalWidth, int logicalHeight)
    {
        var handle = WindowNative.GetWindowHandle(window);
        var scale = GetDpiForWindow(handle) / 96.0;

        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)(logicalWidth * scale);
            presenter.PreferredMinimumHeight = (int)(logicalHeight * scale);
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);
}
