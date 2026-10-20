namespace PwdMgr.Core.Security;

/// <summary>密码生成选项。</summary>
public sealed record PasswordOptions
{
    /// <summary>生成的密码长度。</summary>
    public int Length { get; init; } = 20;

    public bool IncludeUppercase { get; init; } = true;

    public bool IncludeLowercase { get; init; } = true;

    public bool IncludeDigits { get; init; } = true;

    public bool IncludeSymbols { get; init; } = true;

    /// <summary>排除容易混淆的字符（I、O、l、0、1）。</summary>
    public bool ExcludeAmbiguous { get; init; } = true;
}
