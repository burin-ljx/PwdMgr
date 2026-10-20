namespace PwdMgr.Core.Security;

/// <summary>密码强度评估结果。</summary>
/// <param name="Length">密码长度（字符数）。</param>
/// <param name="Bits">估算熵值（比特）。</param>
/// <param name="Label">面向用户的强弱标签。</param>
public sealed record StrengthReport(int Length, double Bits, string Label);

/// <summary>
/// 对任意密码估算强度：按出现过的字符类别粗算候选池大小，再乘长度得到熵值。
/// 这只是一个提示，不替代真正的密码质量检查。
/// </summary>
public static class PasswordStrength
{
    private const int LowercasePool = 26;
    private const int UppercasePool = 26;
    private const int DigitPool = 10;
    private const int SymbolPool = 33;
    private const int OtherPool = 100;

    public static StrengthReport Evaluate(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return new StrengthReport(0, 0, "—");
        }

        var pool = 0;
        var hasLower = false;
        var hasUpper = false;
        var hasDigit = false;
        var hasSymbol = false;
        var hasOther = false;

        foreach (var character in password)
        {
            if (char.IsAsciiLetterLower(character))
            {
                hasLower = true;
            }
            else if (char.IsAsciiLetterUpper(character))
            {
                hasUpper = true;
            }
            else if (char.IsAsciiDigit(character))
            {
                hasDigit = true;
            }
            else if (char.IsAscii(character))
            {
                hasSymbol = true;
            }
            else
            {
                hasOther = true;
            }
        }

        if (hasLower)
        {
            pool += LowercasePool;
        }

        if (hasUpper)
        {
            pool += UppercasePool;
        }

        if (hasDigit)
        {
            pool += DigitPool;
        }

        if (hasSymbol)
        {
            pool += SymbolPool;
        }

        if (hasOther)
        {
            pool += OtherPool;
        }

        var bits = password.Length * Math.Log2(Math.Max(pool, 2));
        return new StrengthReport(password.Length, bits, Describe(bits));
    }

    /// <summary>按熵值给出强弱标签，与生成器的口径保持一致。</summary>
    public static string Describe(double bits) => bits switch
    {
        <= 0 => "—",
        < 50 => "弱",
        < 70 => "中等",
        < 100 => "强",
        _ => "很强",
    };
}
