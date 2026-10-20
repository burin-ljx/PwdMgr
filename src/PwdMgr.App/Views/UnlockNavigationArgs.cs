namespace PwdMgr.App.Views;

/// <summary>
/// 导航到“打开密码库”页时携带的参数。
/// <paramref name="SuggestKey"/> 为 true 时（例如拖入文件、命令行启动）会顺手填上同名钥匙路径；
/// 从“上锁”回来时是 false，钥匙路径保持为空，要求用户重新选择钥匙文件。
/// </summary>
public sealed record UnlockNavigationArgs(string VaultPath, bool SuggestKey);
