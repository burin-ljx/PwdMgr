using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace PwdMgr.App.Services;

/// <summary>一次拖放里识别出来的文件。</summary>
/// <param name="VaultPath">拖入的 .pwdb 文件路径。</param>
/// <param name="KeyPath">拖入的 .key 文件路径。</param>
/// <param name="Error">无法接受时给用户的提示。</param>
public sealed record FileDropResult(string? VaultPath, string? KeyPath, string? Error);

/// <summary>处理从资源管理器 / 桌面拖入的密码库与钥匙文件。</summary>
public static class FileDropHelper
{
    public const string VaultExtension = ".pwdb";
    public const string KeyExtension = ".key";

    public const string UnsupportedMessage = "只支持拖入 .pwdb 密码库文件或 .key 钥匙文件。";

    public const string KeyFirstMessage = "请先拖入密码库文件，再拖入钥匙文件。";

    /// <summary>拖放内容是否可能是文件。</summary>
    public static bool CanAccept(DragEventArgs e)
    {
        try
        {
            return e.DataView.Contains(StandardDataFormats.StorageItems);
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException)
        {
            return false;
        }
    }

    /// <summary>在 DragOver 里调用，告诉系统这里可以放。</summary>
    public static void Accept(DragEventArgs e, string caption)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = caption;
        e.DragUIOverride.IsCaptionVisible = true;
        e.Handled = true;
    }

    /// <summary>读取拖入的文件，按扩展名分类。同类文件只取第一个。</summary>
    public static async Task<FileDropResult> ReadAsync(DragEventArgs e)
    {
        IReadOnlyList<IStorageItem> items;
        try
        {
            items = await e.DataView.GetStorageItemsAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException or UnauthorizedAccessException)
        {
            return new FileDropResult(null, null, $"无法读取拖入的内容：{ex.Message}");
        }

        string? vaultPath = null;
        string? keyPath = null;

        foreach (var item in items)
        {
            if (item is not StorageFile file || string.IsNullOrEmpty(file.Path))
            {
                continue;
            }

            var extension = Path.GetExtension(file.Path);
            if (extension.Equals(VaultExtension, StringComparison.OrdinalIgnoreCase))
            {
                vaultPath ??= file.Path;
            }
            else if (extension.Equals(KeyExtension, StringComparison.OrdinalIgnoreCase))
            {
                keyPath ??= file.Path;
            }
        }

        if (vaultPath is null && keyPath is null)
        {
            return new FileDropResult(null, null, UnsupportedMessage);
        }

        return new FileDropResult(vaultPath, keyPath, null);
    }
}
