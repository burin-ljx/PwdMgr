using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace PwdMgr.App.Services;

/// <summary>主题菜单：跟随系统 / 浅色 / 深色。每次打开时重建，保证勾选状态最新。</summary>
public static class ThemeMenu
{
    public static void Show(FrameworkElement target)
    {
        var flyout = new MenuFlyout
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedRight,
        };

        AddItem(flyout, ThemeMode.System, "跟随系统");
        AddItem(flyout, ThemeMode.Light, "浅色");
        AddItem(flyout, ThemeMode.Dark, "深色");

        flyout.ShowAt(target);
    }

    private static void AddItem(MenuFlyout flyout, ThemeMode mode, string text)
    {
        var item = new RadioMenuFlyoutItem
        {
            Text = text,
            GroupName = "Theme",
            IsChecked = ThemeService.Current == mode,
        };

        item.Click += (_, _) => ThemeService.Apply(mode);
        flyout.Items.Add(item);
    }
}
