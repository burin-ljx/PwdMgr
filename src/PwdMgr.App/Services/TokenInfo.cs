using System.Runtime.InteropServices;

namespace PwdMgr.App.Services;

/// <summary>
/// 读取当前进程令牌的关键属性：完整性级别、是否提权、受限 SID 数量。
/// 用于判断“程序写不了文件”是否由令牌降权（低完整性/受限令牌）造成。
/// </summary>
internal static class TokenInfo
{
    private const int TokenElevation = 20;
    private const int TokenIntegrityLevel = 25;
    private const int TokenRestrictedSids = 11;
    private const uint TokenQuery = 0x0008;

    public static string Describe()
    {
        if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out var token))
        {
            return "无法读取令牌";
        }

        try
        {
            return $"完整性={GetIntegrityLevel(token)} 提权={IsElevated(token)} 受限SID={GetRestrictedSidCount(token)}";
        }
        finally
        {
            CloseHandle(token);
        }
    }

    private static string GetIntegrityLevel(IntPtr token)
    {
        // 第一次调用只取所需缓冲区大小，函数本身会返回 false 并设置最后错误为“缓冲区不足”。
        GetTokenInformation(token, TokenIntegrityLevel, IntPtr.Zero, 0, out var size);
        if (size <= 0)
        {
            return "未知";
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (!GetTokenInformation(token, TokenIntegrityLevel, buffer, size, out _))
            {
                return "未知";
            }

            var sid = Marshal.ReadIntPtr(buffer);
            if (sid == IntPtr.Zero)
            {
                return "未知";
            }

            var count = Marshal.ReadByte(sid, 1);
            if (count == 0)
            {
                return "未知";
            }

            var ridPointer = GetSidSubAuthority(sid, (uint)(count - 1));
            if (ridPointer == IntPtr.Zero)
            {
                return "未知";
            }

            var rid = (uint)Marshal.ReadInt32(ridPointer);

            return rid switch
            {
                0x0000 => "不受信任",
                0x1000 => "低",
                0x2000 => "中",
                0x2100 => "中+",
                0x3000 => "高",
                0x4000 => "系统",
                _ => $"0x{rid:X}",
            };
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string IsElevated(IntPtr token)
    {
        var buffer = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            return GetTokenInformation(token, TokenElevation, buffer, sizeof(int), out _)
                ? (Marshal.ReadInt32(buffer) != 0 ? "是" : "否")
                : "未知";
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string GetRestrictedSidCount(IntPtr token)
    {
        if (!GetTokenInformation(token, TokenRestrictedSids, IntPtr.Zero, 0, out var size) || size <= 0)
        {
            return "0";
        }

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (!GetTokenInformation(token, TokenRestrictedSids, buffer, size, out _))
            {
                return "未知";
            }

            return Marshal.ReadInt32(buffer).ToString();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr tokenHandle, int infoClass, IntPtr info, int infoLength, out int returnLength);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern IntPtr GetSidSubAuthority(IntPtr sid, uint subAuthorityIndex);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
