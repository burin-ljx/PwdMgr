namespace PwdMgr.Core.Tests;

/// <summary>为每个测试提供独立的临时目录与库/钥匙文件路径，测试结束自动清理。</summary>
internal sealed class TestVault : IDisposable
{
    public TestVault()
    {
        Root = Path.Combine(AppContext.BaseDirectory, "test-tmp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);

        VaultPath = Path.Combine(Root, "vault.pwdb");
        KeyPath = Path.Combine(Root, "vault.key");
        OtherKeyPath = Path.Combine(Root, "other.key");
    }

    public string Root { get; }

    public string VaultPath { get; }

    public string KeyPath { get; }

    public string OtherKeyPath { get; }

    public string BackupPath => VaultPath + ".bak";

    public static string NewVaultPath(TestVault owner, string fileName) => Path.Combine(owner.Root, fileName);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 测试目录清理失败不应让测试失败。
        }
    }
}
