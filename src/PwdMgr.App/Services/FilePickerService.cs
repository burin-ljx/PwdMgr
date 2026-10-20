using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PwdMgr.App.Services;

/// <summary>
/// 文件选择对话框。非打包（绿色版）应用必须先把窗口句柄交给 Picker，否则会抛出错误。
/// </summary>
public static class FilePickerService
{
    /// <summary>选择要打开的密码库文件。</summary>
    public static async Task<string?> PickVaultToOpenAsync(Window window)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add(".pwdb");
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    /// <summary>选择要打开的钥匙文件。</summary>
    public static async Task<string?> PickKeyToOpenAsync(Window window)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add(".key");
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));

        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    /// <summary>选择新密码库的保存位置。</summary>
    public static async Task<string?> PickVaultToCreateAsync(Window window, string suggestedName)
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = suggestedName,
            DefaultFileExtension = ".pwdb",
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeChoices.Add("密码库文件", [".pwdb"]);
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    /// <summary>选择新钥匙文件的保存位置。</summary>
    public static async Task<string?> PickKeyToCreateAsync(Window window, string suggestedName)
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = suggestedName,
            DefaultFileExtension = ".key",
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeChoices.Add("钥匙文件", [".key"]);
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    /// <summary>根据密码库路径推导钥匙路径：同目录、同名、扩展名 .key。</summary>
    public static string SuggestKeyPath(string vaultPath)
    {
        if (string.IsNullOrWhiteSpace(vaultPath))
        {
            return string.Empty;
        }

        var directory = Path.GetDirectoryName(vaultPath);
        var name = Path.GetFileNameWithoutExtension(vaultPath);
        return string.IsNullOrEmpty(directory) ? $"{name}.key" : Path.Combine(directory, $"{name}.key");
    }
}
