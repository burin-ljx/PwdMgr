using System.Runtime.InteropServices;

namespace PwdMgr.App.Services;

/// <summary>
/// 启动时记录运行环境，用于排查“程序写不了文件”这类与权限有关的问题：
/// 是否以打包应用（AppContainer）身份运行、临时目录/程序目录是否可写。
/// </summary>
internal static class StartupDiagnostics
{
    public static void Log()
    {
        StartupLog.Write(
            $"进程：用户={Environment.UserName} 64位={Environment.Is64BitProcess} 打包身份={GetPackageIdentity()}");
        StartupLog.Write($"令牌：{TokenInfo.Describe()}");

        var temp = Path.GetTempPath();
        StartupLog.Write($"临时目录可写={CanWrite(temp)}（{temp}）");
        StartupLog.Write($"程序目录可写={CanWrite(AppContext.BaseDirectory)}（{AppContext.BaseDirectory}）");

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        StartupLog.Write($"用户目录可写={CanWrite(profile)}（{profile}）");
    }

    private static string GetPackageIdentity()
    {
        try
        {
            var length = 0;
            var result = GetCurrentPackageFullName(ref length, null);

            // APPMODEL_ERROR_NO_PACKAGE = 15700
            return result == 15700 ? "无（非打包应用）" : $"未知（{result}）";
        }
        catch (EntryPointNotFoundException)
        {
            return "未知";
        }
    }

    private static string CanWrite(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return "目录不存在";
        }

        var probe = Path.Combine(directory, $".pwdmgr-probe-{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1))
            {
                stream.WriteByte(0);
            }

            try
            {
                File.Delete(probe);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // 没有删除权限不影响“可写”的结论。
            }

            return "是";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return $"否（{ex.GetType().Name}: {ex.Message}）";
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, [Out] char[]? packageFullName);
}
