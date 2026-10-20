using PwdMgr.Core.Security;
using Xunit;

namespace PwdMgr.Core.Tests;

public sealed class PasswordStrengthTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Evaluate_Empty_ReturnsDash(string? password)
    {
        var report = PasswordStrength.Evaluate(password);

        Assert.Equal(0, report.Length);
        Assert.Equal(0, report.Bits);
        Assert.Equal("—", report.Label);
    }

    [Fact]
    public void Evaluate_ShortSimplePassword_IsWeak()
    {
        var report = PasswordStrength.Evaluate("abc123");

        Assert.Equal(6, report.Length);
        Assert.Equal("弱", report.Label);
    }

    [Fact]
    public void Evaluate_LongMixedPassword_IsVeryStrong()
    {
        var report = PasswordStrength.Evaluate("Tr0ub4dor&3xK9!pQz#Lm7@Vn2");

        Assert.True(report.Bits >= 100, $"熵值 {report.Bits:0}");
        Assert.Equal("很强", report.Label);
    }

    [Fact]
    public void Evaluate_AddingCharacterClasses_IncreasesBits()
    {
        var digitsOnly = PasswordStrength.Evaluate("839201745");
        var mixed = PasswordStrength.Evaluate("839201745aA!");

        Assert.True(mixed.Bits > digitsOnly.Bits);
    }

    [Fact]
    public void Evaluate_ChineseCharacters_CountAsOtherPool()
    {
        var report = PasswordStrength.Evaluate("密码管理器");

        Assert.Equal(5, report.Length);
        Assert.True(report.Bits > 0);
    }

    [Theory]
    [InlineData(40, "弱")]
    [InlineData(55, "中等")]
    [InlineData(85, "强")]
    [InlineData(110, "很强")]
    public void Describe_MapsBitsToLabel(double bits, string expected)
    {
        Assert.Equal(expected, PasswordStrength.Describe(bits));
    }
}
