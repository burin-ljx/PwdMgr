using Microsoft.UI.Xaml;

namespace PwdMgr.App.Services;

/// <summary>
/// 文件选择入口。使用 Win32 原生对话框，保证普通权限与提权运行下都能弹出，
/// 也不会像 WinRT 保存选择器那样预先创建 0 字节文件。
/// </summary>
public static class FilePickerService
{
    private const string VaultFilter = "密码库文件 (*.pwdb)\0*.pwdb\0所有文件 (*.*)\0*.*\0\0";
    private const string KeyFilter = "钥匙文件 (*.key)\0*.key\0所有文件 (*.*)\0*.*\0\0";
    private const string VaultSaveFilter = "密码库文件 (*.pwdb)\0*.pwdb\0\0";
    private const string KeySaveFilter = "钥匙文件 (*.key)\0*.key\0\0";

    /// <summary>选择要打开的密码库文件。</summary>
    public static string? PickVaultToOpen(Window window) =>
        NativeFileDialog.ShowOpen(window, "选择密码库文件", VaultFilter);

    /// <summary>选择要打开的钥匙文件。</summary>
    public static string? PickKeyToOpen(Window window) =>
        NativeFileDialog.ShowOpen(window, "选择钥匙文件", KeyFilter);

    /// <summary>选择新密码库的保存位置。</summary>
    public static string? PickVaultToCreate(Window window, string suggestedName) =>
        NativeFileDialog.ShowSave(window, "保存密码库", VaultSaveFilter, "pwdb", suggestedName);

    /// <summary>选择新钥匙文件的保存位置。</summary>
    public static string? PickKeyToCreate(Window window, string suggestedName) =>
        NativeFileDialog.ShowSave(window, "保存钥匙文件", KeySaveFilter, "key", suggestedName);

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
