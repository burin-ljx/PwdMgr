using PwdMgr.Core.Vault;
using Xunit;

namespace PwdMgr.Core.Tests;

public sealed class VaultBackupTests
{
    [Fact]
    public async Task CreateBackup_ProducesFileThatOpensWithSameKey()
    {
        using var temp = new TestVault();
        var backupPath = Path.Combine(temp.Root, "backup.pwdb");
        Guid vaultId;

        await using (var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath))
        {
            session.Document.Add("GitHub", "alice", "s3cret", "https://github.com", "note", ["工作"]);
            vaultId = session.Document.VaultId;
            await session.SaveAsync();

            var size = await session.CreateBackupAsync(backupPath);

            Assert.True(size > 0);
            Assert.True(File.Exists(backupPath));
        }

        await using var reopened = await VaultSession.OpenAsync(backupPath, temp.KeyPath);
        Assert.Equal(vaultId, reopened.Document.VaultId);
        Assert.Contains(
            reopened.Document.GetActiveItems(),
            item => item.Title == "GitHub" && item.Password == "s3cret");
    }

    [Fact]
    public async Task CreateBackup_SavesUnsavedChangesFirst()
    {
        using var temp = new TestVault();
        var backupPath = Path.Combine(temp.Root, "backup.pwdb");

        await using (var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath))
        {
            session.Document.Add("未保存的条目", "user", "pass");
            await session.CreateBackupAsync(backupPath);
        }

        await using var reopened = await VaultSession.OpenAsync(backupPath, temp.KeyPath);
        Assert.Contains(reopened.Document.GetActiveItems(), item => item.Title == "未保存的条目");
    }

    [Fact]
    public async Task CreateBackup_ToSamePathAsVault_Throws()
    {
        using var temp = new TestVault();
        await using var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath);

        await Assert.ThrowsAsync<ArgumentException>(() => session.CreateBackupAsync(temp.VaultPath));
    }

    [Fact]
    public async Task Verify_ReportsItemCountsAndSize()
    {
        using var temp = new TestVault();

        await using var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath);
        session.Document.Add("GitHub", "alice", "s3cret");
        var second = session.Document.Add("Steam", "alice_games", "p");
        await session.SaveAsync();
        session.Document.MoveToTrash(second.Id);
        await session.SaveAsync();

        var report = await session.VerifyAsync();

        Assert.Equal(1, report.ActiveCount);
        Assert.Equal(1, report.DeletedCount);
        Assert.True(report.FileSize > 0);
    }

    [Fact]
    public async Task Verify_AfterCiphertextTamper_ReportsDecryptionFailed()
    {
        using var temp = new TestVault();

        await using var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath);
        session.Document.Add("GitHub", "alice", "s3cret");
        await session.SaveAsync();

        var bytes = await File.ReadAllBytesAsync(temp.VaultPath);
        bytes[^17] ^= 0xFF;
        await File.WriteAllBytesAsync(temp.VaultPath, bytes);

        var ex = await Assert.ThrowsAsync<VaultException>(() => session.VerifyAsync());
        Assert.Equal(VaultErrorCode.DecryptionFailed, ex.Code);
    }

    [Fact]
    public async Task Verify_AfterTruncation_ReportsFileCorrupted()
    {
        using var temp = new TestVault();

        await using var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath);
        session.Document.Add("GitHub", "alice", "s3cret");
        await session.SaveAsync();

        var bytes = await File.ReadAllBytesAsync(temp.VaultPath);
        await File.WriteAllBytesAsync(temp.VaultPath, bytes[..^8]);

        var ex = await Assert.ThrowsAsync<VaultException>(() => session.VerifyAsync());
        Assert.Equal(VaultErrorCode.FileCorrupted, ex.Code);
    }

    [Fact]
    public async Task RestoreFromFile_RestoresDeletedItem()
    {
        using var temp = new TestVault();
        var backupPath = Path.Combine(temp.Root, "backup.pwdb");

        Guid itemId;
        await using (var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath))
        {
            itemId = session.Document.Add("GitHub", "alice", "s3cret").Id;
            await session.SaveAsync();
            await session.CreateBackupAsync(backupPath);

            session.Document.MoveToTrash(itemId);
            await session.SaveAsync();
        }

        await VaultSession.RestoreFromFileAsync(temp.VaultPath, temp.KeyPath, backupPath);

        await using var reopened = await VaultSession.OpenAsync(temp.VaultPath, temp.KeyPath);
        Assert.Contains(reopened.Document.GetActiveItems(), item => item.Id == itemId);
        Assert.Empty(reopened.Document.GetDeletedItems());
    }

    [Fact]
    public async Task RestoreFromFile_WithAnotherVaultsBackup_ReportsKeyMismatch()
    {
        using var temp = new TestVault();
        var otherVaultPath = TestVault.NewVaultPath(temp, "other.pwdb");
        var otherKeyPath = Path.Combine(temp.Root, "other.key");

        await using (var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath))
        {
            session.Document.Add("GitHub", "alice", "s3cret");
            await session.SaveAsync();
        }

        await using (var other = await VaultSession.CreateAsync(otherVaultPath, otherKeyPath))
        {
            other.Document.Add("别处的库", "bob", "p");
            await other.SaveAsync();
        }

        var ex = await Assert.ThrowsAsync<VaultException>(
            () => VaultSession.RestoreFromFileAsync(temp.VaultPath, temp.KeyPath, otherVaultPath));
        Assert.Equal(VaultErrorCode.KeyMismatch, ex.Code);
    }

    [Fact]
    public async Task RestoreFromFile_WithMissingBackup_ReportsFileNotFound()
    {
        using var temp = new TestVault();
        var missing = Path.Combine(temp.Root, "nope.pwdb");

        await using var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath);

        var ex = await Assert.ThrowsAsync<VaultException>(
            () => VaultSession.RestoreFromFileAsync(temp.VaultPath, temp.KeyPath, missing));
        Assert.Equal(VaultErrorCode.VaultFileNotFound, ex.Code);
    }
}
