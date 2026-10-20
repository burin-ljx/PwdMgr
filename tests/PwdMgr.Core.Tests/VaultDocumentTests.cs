using PwdMgr.Core.Models;
using PwdMgr.Core.Vault;
using Xunit;

namespace PwdMgr.Core.Tests;

public sealed class VaultDocumentTests
{
    [Fact]
    public void CreateNew_StartsEmptyAndClean()
    {
        var document = VaultDocument.CreateNew();

        Assert.Empty(document.GetActiveItems());
        Assert.Empty(document.GetDeletedItems());
        Assert.False(document.IsDirty);
    }

    [Fact]
    public void Add_RequiresTitle()
    {
        var document = VaultDocument.CreateNew();

        Assert.Throws<ArgumentException>(() => document.Add("   "));
    }

    [Fact]
    public void Add_SetsTimestampsAndDirtyFlag()
    {
        var document = VaultDocument.CreateNew();

        var item = document.Add("GitHub");

        Assert.Equal("GitHub", item.Title);
        Assert.Equal(item.CreatedUtc, item.UpdatedUtc);
        Assert.True(document.IsDirty);
    }

    [Fact]
    public void Add_ClampsOverlongFields()
    {
        var document = VaultDocument.CreateNew();

        var item = document.Add(
            new string('T', 500),
            new string('u', 500),
            new string('p', 2000),
            new string('w', 3000),
            new string('n', 10000));

        Assert.Equal(200, item.Title.Length);
        Assert.Equal(200, item.Username.Length);
        Assert.Equal(1000, item.Password.Length);
        Assert.Equal(2000, item.Url.Length);
        Assert.Equal(8000, item.Notes.Length);
    }

    [Fact]
    public void Add_NormalizesTags()
    {
        var document = VaultDocument.CreateNew();

        var item = document.Add("A", tags: [" 工作 ", "工作", "", "Dev", "dev"]);

        Assert.Equal(new[] { "工作", "Dev" }, item.Tags);
    }

    [Fact]
    public void Search_MatchesAllFieldsIgnoringCase()
    {
        var document = VaultDocument.CreateNew();
        document.Add("GitHub", "Alice", url: "https://github.com", notes: "工作账号", tags: ["Dev"]);
        document.Add("邮箱", "bob@example.com", notes: "私人");

        Assert.Single(document.Search("github"));
        Assert.Single(document.Search("ALICE"));
        Assert.Single(document.Search("工作"));
        Assert.Single(document.Search("dev"));
        Assert.Equal(2, document.Search(string.Empty).Count);
        Assert.Empty(document.Search("不存在的关键字"));
    }

    [Fact]
    public void MoveToTrash_HidesFromActiveAndCanBeRestored()
    {
        var document = VaultDocument.CreateNew();
        var item = document.Add("GitHub");

        Assert.True(document.MoveToTrash(item.Id));
        Assert.Empty(document.GetActiveItems());

        var trashed = Assert.Single(document.GetDeletedItems());
        Assert.True(trashed.IsDeleted);

        Assert.True(document.Restore(item.Id));
        Assert.Single(document.GetActiveItems());
        Assert.Empty(document.GetDeletedItems());
    }

    [Fact]
    public void Purge_RemovesItemPermanently()
    {
        var document = VaultDocument.CreateNew();
        var item = document.Add("GitHub");
        document.MoveToTrash(item.Id);

        Assert.True(document.Purge(item.Id));
        Assert.Null(document.Find(item.Id));
        Assert.Empty(document.GetDeletedItems());
    }

    [Fact]
    public void PurgeExpiredTrash_RemovesOnlyExpiredItems()
    {
        var document = VaultDocument.CreateNew();
        var oldItem = document.Add("旧条目");
        var newItem = document.Add("新条目");
        var now = DateTimeOffset.UtcNow;

        document.MoveToTrash(oldItem.Id, now.AddDays(-40));
        document.MoveToTrash(newItem.Id, now.AddDays(-5));

        var removed = document.PurgeExpiredTrash(TimeSpan.FromDays(30), now);

        Assert.Equal(1, removed);
        Assert.Null(document.Find(oldItem.Id));
        Assert.NotNull(document.Find(newItem.Id));
    }

    [Fact]
    public void Update_ReplacesFieldsAndBumpsTimestamp()
    {
        var document = VaultDocument.CreateNew();
        var item = document.Add("GitHub", "alice", "old");
        var originalTimestamp = item.UpdatedUtc;

        var edited = new PasswordItem { Id = item.Id, Title = "GitHub 2", Username = "bob", Password = "new" };

        Assert.True(document.Update(edited));

        var stored = Assert.Single(document.GetActiveItems());
        Assert.Equal("GitHub 2", stored.Title);
        Assert.Equal("bob", stored.Username);
        Assert.Equal("new", stored.Password);
        Assert.True(stored.UpdatedUtc >= originalTimestamp);
    }

    [Fact]
    public void Update_UnknownId_ReturnsFalse()
    {
        var document = VaultDocument.CreateNew();

        Assert.False(document.Update(new PasswordItem { Title = "X" }));
    }

    [Fact]
    public void Restore_NonDeletedItem_ReturnsFalse()
    {
        var document = VaultDocument.CreateNew();
        var item = document.Add("GitHub");

        Assert.False(document.Restore(item.Id));
    }
}
