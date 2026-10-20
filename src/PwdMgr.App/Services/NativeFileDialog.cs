using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace PwdMgr.App.Services;

/// <summary>
/// Win32 原生文件对话框（comdlg32）。相比 WinRT 的 FileOpenPicker/FileSavePicker：
/// <list type="bullet">
///   <item>在提权进程里也能正常弹出（WinRT 选择器受 UIPI 限制）；</item>
///   <item>不会预先创建 0 字节占位文件，创建流程更简单；</item>
///   <item>不依赖打包身份或选择器代理进程。</item>
/// </list>
/// </summary>
internal static class NativeFileDialog
{
    private const int MaxPathChars = 4096;

    private const uint OFN_OVERWRITEPROMPT = 0x00000002;
    private const uint OFN_HIDEREADONLY = 0x00000004;
    private const uint OFN_NOCHANGEDIR = 0x00000008;
    private const uint OFN_PATHMUSTEXIST = 0x00000800;
    private const uint OFN_FILEMUSTEXIST = 0x00001000;
    private const uint OFN_EXPLORER = 0x00080000;

    public static string? ShowOpen(Window window, string title, string filter) =>
        Show(window, title, filter, null, null, save: false);

    public static string? ShowSave(
        Window window,
        string title,
        string filter,
        string defaultExtension,
        string suggestedFileName) =>
        Show(window, title, filter, defaultExtension, suggestedFileName, save: true);

    private static string? Show(
        Window window,
        string title,
        string filter,
        string? defaultExtension,
        string? suggestedFileName,
        bool save)
    {
        var fileBuffer = Marshal.AllocHGlobal(MaxPathChars * sizeof(char));
        var filterBuffer = Marshal.StringToHGlobalUni(filter);
        var titleBuffer = Marshal.StringToHGlobalUni(title);
        var extensionBuffer = defaultExtension is null
            ? IntPtr.Zero
            : Marshal.StringToHGlobalUni(defaultExtension);

        try
        {
            // 清空缓冲区，再写入建议文件名（保存对话框用）。
            Marshal.Copy(new char[MaxPathChars], 0, fileBuffer, MaxPathChars);
            if (!string.IsNullOrEmpty(suggestedFileName))
            {
                var name = suggestedFileName.ToCharArray();
                var length = Math.Min(name.Length, MaxPathChars - 1);
                Marshal.Copy(name, 0, fileBuffer, length);
            }

            var dialog = new OpenFileName
            {
                StructSize = (uint)Marshal.SizeOf<OpenFileName>(),
                Owner = WindowNative.GetWindowHandle(window),
                Filter = filterBuffer,
                File = fileBuffer,
                MaxFile = MaxPathChars,
                Title = titleBuffer,
                DefaultExtension = extensionBuffer,
                Flags = OFN_EXPLORER | OFN_NOCHANGEDIR | OFN_PATHMUSTEXIST | OFN_HIDEREADONLY |
                        (save ? OFN_OVERWRITEPROMPT : OFN_FILEMUSTEXIST),
            };

            var accepted = save ? GetSaveFileNameW(ref dialog) : GetOpenFileNameW(ref dialog);
            if (!accepted)
            {
                return null;
            }

            return Marshal.PtrToStringUni(fileBuffer);
        }
        finally
        {
            Marshal.FreeHGlobal(fileBuffer);
            Marshal.FreeHGlobal(filterBuffer);
            Marshal.FreeHGlobal(titleBuffer);
            if (extensionBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(extensionBuffer);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public uint StructSize;
        public IntPtr Owner;
        public IntPtr Instance;
        public IntPtr Filter;
        public IntPtr CustomFilter;
        public uint MaxCustomFilter;
        public uint FilterIndex;
        public IntPtr File;
        public uint MaxFile;
        public IntPtr FileTitle;
        public uint MaxFileTitle;
        public IntPtr InitialDirectory;
        public IntPtr Title;
        public uint Flags;
        public ushort FileOffset;
        public ushort FileExtension;
        public IntPtr DefaultExtension;
        public IntPtr CustomData;
        public IntPtr Hook;
        public IntPtr TemplateName;
        public IntPtr Reserved;
        public uint ReservedValue;
        public uint FlagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileNameW(ref OpenFileName dialog);

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetSaveFileNameW(ref OpenFileName dialog);
}
