using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using PwdMgr.App.Services;
using PwdMgr.Core.Security;
using Windows.Graphics;

namespace PwdMgr.App.Views;

/// <summary>独立的密码生成器窗口。选项与主界面的“骰子”按钮共用 <see cref="AppShell.GeneratorOptions"/>。</summary>
public sealed partial class GeneratorWindow : Window
{
    public GeneratorWindow()
    {
        InitializeComponent();

        Title = "密码生成器";
        AppWindow.Resize(new SizeInt32(560, 740));
        AppWindow.Move(CenteredPosition(560, 740));
        WindowHelper.SetMinimumSize(this, 460, 660);

        _isLoading = true;
        LengthSlider.Minimum = PasswordGenerator.MinLength;
        LengthSlider.Maximum = 64;
        LengthSlider.StepFrequency = 1;
        LengthSlider.TickFrequency = 4;

        var options = App.Shell.GeneratorOptions;
        LengthSlider.Value = options.Length;
        LengthText.Text = options.Length.ToString(System.Globalization.CultureInfo.CurrentCulture);
        UppercaseCheck.IsChecked = options.IncludeUppercase;
        LowercaseCheck.IsChecked = options.IncludeLowercase;
        DigitsCheck.IsChecked = options.IncludeDigits;
        SymbolsCheck.IsChecked = options.IncludeSymbols;
        AmbiguousCheck.IsChecked = options.ExcludeAmbiguous;
        CustomCharsBox.Text = options.CustomCharacters;
        _isLoading = false;

        Generate();
    }

    private bool _isLoading;

    /// <summary>规则改过但还没点“生成”：界面上会提示一下。</summary>
    private bool _settingsDirty;

    private PointInt32 CenteredPosition(int width, int height)
    {
        var display = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var work = display.WorkArea;
        return new PointInt32(
            work.X + ((work.Width - width) / 2),
            work.Y + ((work.Height - height) / 2));
    }

    private PasswordOptions ReadOptions() => new()
    {
        Length = (int)LengthSlider.Value,
        IncludeUppercase = UppercaseCheck.IsChecked == true,
        IncludeLowercase = LowercaseCheck.IsChecked == true,
        IncludeDigits = DigitsCheck.IsChecked == true,
        IncludeSymbols = SymbolsCheck.IsChecked == true,
        ExcludeAmbiguous = AmbiguousCheck.IsChecked == true,
        CustomCharacters = CustomCharsBox.Text,
    };

    private void OnCustomCharsChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isLoading)
        {
            ApplySettings();
        }
    }

    /// <summary>规则改动只更新设置与提示，不重新生成；点“生成”才按新规则生成。</summary>
    private void ApplySettings()
    {
        var options = ReadOptions();
        App.Shell.GeneratorOptions = options;
        _settingsDirty = true;
        UpdateStrengthHint(options);
        StatusBar.IsOpen = false;
    }

    private void Generate()
    {
        var options = ReadOptions();
        App.Shell.GeneratorOptions = options;

        try
        {
            PasswordBox.Text = PasswordGenerator.Generate(options);
            _settingsDirty = false;
            UpdateStrengthHint(options);
            StatusBar.IsOpen = false;
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            PasswordBox.Text = string.Empty;
            StrengthText.Text = string.Empty;
            ShowStatus(ex.Message, InfoBarSeverity.Warning);
        }
    }

    private void UpdateStrengthHint(PasswordOptions options)
    {
        var entropy = PasswordGenerator.EntropyBits(options);
        var text =
            $"强度：{PasswordGenerator.DescribeStrength(entropy)}（约 {entropy:0} 比特，候选字符 {PasswordGenerator.PoolSize(options)} 个）";

        StrengthText.Text = _settingsDirty
            ? text + " · 设置已修改，点“生成”应用"
            : text;
    }

    private void OnLengthChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        LengthText.Text = ((int)e.NewValue).ToString(System.Globalization.CultureInfo.CurrentCulture);
        ApplySettings();
    }

    private void OnOptionChanged(object sender, RoutedEventArgs e)
    {
        if (!_isLoading)
        {
            ApplySettings();
        }
    }

    private void OnPasswordTextChanged(object sender, TextChangedEventArgs e) =>
        CopyButton.IsEnabled = PasswordBox.Text.Length > 0;

    private void OnRegenerateClick(object sender, RoutedEventArgs e) => Generate();

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (PasswordBox.Text.Length == 0)
        {
            return;
        }

        if (ClipboardService.TryCopy(PasswordBox.Text))
        {
            ShowStatus("已复制，30 秒后自动清除。", InfoBarSeverity.Success);
        }
        else
        {
            ShowStatus("剪贴板被其他程序占用，请稍后重试。", InfoBarSeverity.Error);
        }
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusBar.Severity = severity;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }
}
