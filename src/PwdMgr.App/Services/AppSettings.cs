using System.Text.Json;
using System.Text.Json.Serialization;

namespace PwdMgr.App.Services;

/// <summary>主题模式。</summary>
public enum ThemeMode
{
    /// <summary>跟随系统。</summary>
    System = 0,

    Light = 1,

    Dark = 2,
}

/// <summary>
/// 应用级设置（与密码库无关，按用户保存在本地）。
/// 读写失败时全部降级为默认值，不影响主流程。
/// </summary>
public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public ThemeMode Theme { get; set; } = ThemeMode.System;

    /// <summary>当前设置（启动时读取一次）。</summary>
    public static AppSettings Current { get; } = Load();

    private static string SettingsPath
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "PwdMgr", "settings.json");
        }
    }

    /// <summary>把当前设置写回磁盘；失败只记日志。</summary>
    public void Save()
    {
        try
        {
            var path = SettingsPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            StartupLog.Write($"保存设置失败：{ex.Message}");
        }
    }

    private static AppSettings Load()
    {
        try
        {
            var path = SettingsPath;
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            StartupLog.Write($"读取设置失败，使用默认值：{ex.Message}");
            return new AppSettings();
        }
    }
}
