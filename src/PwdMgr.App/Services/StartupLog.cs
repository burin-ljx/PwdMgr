namespace PwdMgr.App.Services;

/// <summary>
/// 启动阶段的最小日志。GUI 应用没有控制台，出问题时只能靠它定位。
/// 文件写在程序所在目录，随绿色版一起搬迁：Debug 构建记录每一步，Release 构建只记录异常。
/// </summary>
internal static class StartupLog
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "startup.log");

    public static void Write(string message)
    {
#if DEBUG
        Append(message);
#endif
    }

    public static void Write(string context, Exception exception) =>
        Append($"{context}: {exception.GetType().Name}: {exception.Message}{Environment.NewLine}{exception.StackTrace}");

    private static void Append(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 日志写不进去也不能影响启动。
        }
    }
}
