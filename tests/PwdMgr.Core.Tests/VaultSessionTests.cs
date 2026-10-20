using PwdMgr.Core.Vault;
using Xunit;

namespace PwdMgr.Core.Tests;

public sealed class VaultSessionTests
{
    [Fact]
    public async Task Create_WritesVaultAndKeyFiles()
    {
        using var temp = new TestVault();

        await using (var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath))
        {
            Assert.True(File.Exists(temp.VaultPath));
            Assert.True(File.Exists(temp.KeyPath));
            Assert.Empty(session.Document.GetActiveItems());
            Assert.NotEqual(Guid.Empty, session.VaultId);
            Assert.Equal(16, session.KeyId.Length);
        }

        // 会话释放后锁文件应被删除，库文件保持在原处。
        Assert.False(File.Exists(temp.VaultPath + ".lock"));
        Assert.True(File.Exists(temp.VaultPath));
    }

    [Fact]
    public async Task SaveAndReopen_RoundTripsAllFields()
    {
        using var temp = new TestVault();
        Guid itemId;
        Guid vaultId;

        await using (var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath))
        {
            vaultId = session.VaultId;
            var item = session.Document.Add(
                "GitHub",
                "alice",
                "s3cret!pass",
                "https://github.com",
                "恢复码在抽屉里",
                ["工作", "开发"]);

            itemId = item.Id;
            await session.SaveAsync();
        }

        await using var reopened = await VaultSession.OpenAsync(temp.VaultPath, temp.KeyPath);

        Assert.Equal(vaultId, reopened.VaultId);

        var loaded = Assert.Single(reopened.Document.GetActiveItems());
        Assert.Equal(itemId, loaded.Id);
        Assert.Equal("GitHub", loaded.Title);
        Assert.Equal("alice", loaded.Username);
        Assert.Equal("s3cret!pass", loaded.Password);
        Assert.Equal("https://github.com", loaded.Url);
        Assert.Equal("恢复码在抽屉里", loaded.Notes);
        Assert.Equal(new[] { "工作", "开发" }, loaded.Tags);
    }

    [Fact]
    public async Task VaultFile_DoesNotContainPlaintext()
    {
        using var temp = new TestVault();

        await using (var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath))
        {
            session.Document.Add("SecretTitleMarker", "SecretUserMarker", "SecretPasswordMarker");
            await session.SaveAsync();
        }

        var text = System.Text.Encoding.UTF8.GetString(await File.ReadAllBytesAsync(temp.VaultPath));
        Assert.False(text.Contains("SecretTitleMarker", StringComparison.Ordinal));
        Assert.False(text.Contains("SecretUserMarker", StringComparison.Ordinal));
        Assert.False(text.Contains("SecretPasswordMarker", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Create_WhenTargetExists_ReportsTargetExists()
    {
        using var temp = new TestVault();

        await using (var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath))
        {
            await session.SaveAsync();
        }

        var ex = await Assert.ThrowsAsync<VaultException>(() => VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath));
        Assert.Equal(VaultErrorCode.TargetExists, ex.Code);
    }

    [Fact]
    public async Task Open_WithKeyFromAnotherVault_ReportsKeyMismatch()
    {
        using var temp = new TestVault();
        var otherVaultPath = TestVault.NewVaultPath(temp, "other.pwdb");

        await using (var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath))
        {
            await session.SaveAsync();
        }

        await using (var other = await VaultSession.CreateAsync(otherVaultPath, temp.OtherKeyPath))
        {
            await other.SaveAsync();
        }

        var ex = await Assert.ThrowsAsync<VaultException>(() => VaultSession.OpenAsync(temp.VaultPath, temp.OtherKeyPath));
        Assert.Equal(VaultErrorCode.KeyMismatch, ex.Code);
    }

    [Fact]
    public async Task Open_AfterCiphertextTamper_ReportsDecryptionFailed()
    {
        using var temp = new TestVault();

        await using (var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath))
        {
            session.Document.Add("GitHub", "alice", "s3cret!pass");
            await session.SaveAsync();
        }

        var bytes = await File.ReadAllBytesAsync(temp.VaultPath);
        bytes[^17] ^= 0xFF; // 最后一个密文字节，标签在最后 16 字节
        await File.WriteAllBytesAsync(temp.VaultPath, bytes);

        var ex = await Assert.ThrowsAsync<VaultException>(() => VaultSession.OpenAsync(temp.VaultPath, temp.KeyPath));
        Assert.Equal(VaultErrorCode.DecryptionFailed, ex.Code);
    }

    [Fact]
    public async Task Open_AfterHeaderTamper_ReportsCorruptionOrDecryptionFailure()
    {
        using var temp = new TestVault();

        await using (var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath))
        {
            session.Document.Add("GitHub");
            await session.SaveAsync();
        }

        var bytes = await File.ReadAllBytesAsync(temp.VaultPath);
        bytes[12] ^= 0xFF; // 头部 JSON 内第一个字节
        await File.WriteAllBytesAsync(temp.VaultPath, bytes);

        var ex = await Assert.ThrowsAsync<VaultException>(() => VaultSession.OpenAsync(temp.VaultPath, temp.KeyPath));
        Assert.True(ex.Code is VaultErrorCode.FileCorrupted or VaultErrorCode.DecryptionFailed);
    }

    [Fact]
    public async Task Open_TruncatedFile_ReportsFileCorrupted()
    {
        using var temp = new TestVault();

        await using (var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath))
        {
            session.Document.Add("GitHub");
            await session.SaveAsync();
        }

        var bytes = await File.ReadAllBytesAsync(temp.VaultPath);
        await File.WriteAllBytesAsync(temp.VaultPath, bytes[..^5]);

        var ex = await Assert.ThrowsAsync<VaultException>(() => VaultSession.OpenAsync(temp.VaultPath, temp.KeyPath));
        Assert.Equal(VaultErrorCode.FileCorrupted, ex.Code);
    }

    [Fact]
    public async Task Open_NotAVaultFile_ReportsNotAVaultFile()
    {
        using var temp = new TestVault();
        await File.WriteAllTextAsync(temp.VaultPath, new string('x', 200));

        var ex = await Assert.ThrowsAsync<VaultException>(() => VaultSession.OpenAsync(temp.VaultPath, temp.KeyPath));
        Assert.Equal(VaultErrorCode.NotAVaultFile, ex.Code);
    }

    [Fact]
    public async Task Open_UnsupportedVersion_ReportsUnsupportedVersion()
    {
        using var temp = new TestVault();

        await using (var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath))
        {
            await session.SaveAsync();
        }

        var bytes = await File.ReadAllBytesAsync(temp.VaultPath);
        bytes[4] = 99; // 格式版本 u16 低字节
        await File.WriteAllBytesAsync(temp.VaultPath, bytes);

        var ex = await Assert.ThrowsAsync<VaultException>(() => VaultSession.OpenAsync(temp.VaultPath, temp.KeyPath));
        Assert.Equal(VaultErrorCode.UnsupportedVersion, ex.Code);
    }

    [Fact]
    public async Task Open_MissingKeyFile_ReportsKeyFileNotFound()
    {
        using var temp = new TestVault();

        await using (var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath))
        {
            await session.SaveAsync();
        }

        File.Delete(temp.KeyPath);

        var ex = await Assert.ThrowsAsync<VaultException>(() => VaultSession.OpenAsync(temp.VaultPath, temp.KeyPath));
        Assert.Equal(VaultErrorCode.KeyFileNotFound, ex.Code);
    }

    [Fact]
    public async Task Open_MissingVaultFile_ReportsVaultFileNotFound()
    {
        using var temp = new TestVault();
        await using (var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath))
        {
            await session.SaveAsync();
        }

        File.Delete(temp.VaultPath);

        var ex = await Assert.ThrowsAsync<VaultException>(() => VaultSession.OpenAsync(temp.VaultPath, temp.KeyPath));
        Assert.Equal(VaultErrorCode.VaultFileNotFound, ex.Code);
    }

    [Fact]
    public async Task Open_WhileAnotherSessionHoldsTheVault_ReportsVaultBusy()
    {
        using var temp = new TestVault();

        await using var first = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath);
        await first.SaveAsync();

        var ex = await Assert.ThrowsAsync<VaultException>(() => VaultSession.OpenAsync(temp.VaultPath, temp.KeyPath));
        Assert.Equal(VaultErrorCode.VaultBusy, ex.Code);
    }

    [Fact]
    public async Task Save_Twice_KeepsPreviousVersionInBackup()
    {
        using var temp = new TestVault();

        await using (var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath))
        {
            session.Document.Add("First");
            await session.SaveAsync();

            session.Document.Add("Second");
            await session.SaveAsync();
        }

        Assert.True(File.Exists(temp.BackupPath), "第二次保存应保留上一版备份。");

        await using var backup = await VaultSession.OpenAsync(temp.BackupPath, temp.KeyPath);
        var titles = backup.Document.GetActiveItems().Select(item => item.Title).ToArray();
        Assert.Equal(new[] { "First" }, titles);
    }

    [Fact]
    public async Task Save_ProducesDifferentCiphertextEachTime()
    {
        using var temp = new TestVault();
        byte[] firstSave;

        await using (var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath))
        {
            session.Document.Add("GitHub");
            await session.SaveAsync();
            firstSave = await File.ReadAllBytesAsync(temp.VaultPath);

            session.Document.Add("Email");
            await session.SaveAsync();
        }

        var secondSave = await File.ReadAllBytesAsync(temp.VaultPath);
        Assert.NotEqual(firstSave.Length, secondSave.Length);

        // 即使内容相同，每次保存也必须换 nonce，密文不应重复。
        Assert.False(firstSave.AsSpan().SequenceEqual(secondSave.AsSpan()));
    }

    [Fact]
    public async Task Save_UpdatesDirtyFlag()
    {
        using var temp = new TestVault();

        await using var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath);
        Assert.False(session.Document.IsDirty);

        session.Document.Add("GitHub");
        Assert.True(session.Document.IsDirty);

        await session.SaveAsync();
        Assert.False(session.Document.IsDirty);
    }

    [Fact]
    public async Task Dispose_ClearsInMemoryItems()
    {
        using var temp = new TestVault();

        var session = await VaultSession.CreateAsync(temp.VaultPath, temp.KeyPath);
        session.Document.Add("GitHub", "alice", "s3cret");
        await session.SaveAsync();

        await session.DisposeAsync();

        Assert.Empty(session.Document.GetActiveItems());
        Assert.False(session.Document.IsDirty);
    }
}
