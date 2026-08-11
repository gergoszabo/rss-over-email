using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RssOverEmail.Data;
using RssOverEmail.Models;
using RssOverEmail.Services;

namespace RssOverEmail.Tests;

public class ItemRepositoryTests
{
    private static AppDbContext CreateDbContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    private static async Task SeedFeedAsync(AppDbContext db, int id, string name = "Test Feed")
    {
        db.Feeds.Add(new Feed { Id = id, Name = name, Url = "https://example.com/rss" });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetItemsAsync_ReturnsPaginatedItems()
    {
        using var db = CreateDbContext();
        await SeedFeedAsync(db, 1);
        db.FeedItems.Add(new FeedItem
        {
            FeedId = 1,
            Title = "Item 1",
            Link = "https://ex.com/1",
            Hash = "a",
            CreatedAt = DateTime.UtcNow
        });
        db.FeedItems.Add(new FeedItem
        {
            FeedId = 1,
            Title = "Item 2",
            Link = "https://ex.com/2",
            Hash = "b",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var repo = new ItemRepository(db);
        var result = await repo.GetItemsAsync(null, null, 1, 10);

        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetItemsAsync_FiltersByFeedId()
    {
        using var db = CreateDbContext();
        await SeedFeedAsync(db, 1);
        await SeedFeedAsync(db, 2, "Other Feed");
        db.FeedItems.Add(new FeedItem
        {
            FeedId = 1,
            Title = "Feed 1 Item",
            Link = "https://ex.com/1",
            Hash = "a",
            CreatedAt = DateTime.UtcNow
        });
        db.FeedItems.Add(new FeedItem
        {
            FeedId = 2,
            Title = "Feed 2 Item",
            Link = "https://ex.com/2",
            Hash = "b",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var repo = new ItemRepository(db);
        var result = await repo.GetItemsAsync(1, null, 1, 10);

        result.Items.Should().ContainSingle(i => i.Title == "Feed 1 Item");
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetItemsAsync_FiltersByReadStatus()
    {
        using var db = CreateDbContext();
        await SeedFeedAsync(db, 1);
        db.FeedItems.Add(new FeedItem
        {
            FeedId = 1,
            Title = "Unread",
            Link = "https://ex.com/1",
            Hash = "a",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });
        db.FeedItems.Add(new FeedItem
        {
            FeedId = 1,
            Title = "Read",
            Link = "https://ex.com/2",
            Hash = "b",
            IsRead = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var repo = new ItemRepository(db);
        var result = await repo.GetItemsAsync(null, false, 1, 10);

        result.Items.Should().ContainSingle(i => i.Title == "Unread");
    }

    [Fact]
    public async Task MarkReadAsync_UpdatesIsRead()
    {
        using var db = CreateDbContext();
        await SeedFeedAsync(db, 1);
        var item = new FeedItem
        {
            FeedId = 1,
            Title = "Test",
            Link = "https://ex.com/1",
            Hash = "a",
            IsRead = false
        };
        db.FeedItems.Add(item);
        await db.SaveChangesAsync();

        var repo = new ItemRepository(db);
        await repo.MarkReadAsync(item.Id, true);

        var updated = await db.FeedItems.FindAsync(item.Id);
        updated!.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task AddNewItemsAsync_OnlyInsertsNewHashes()
    {
        using var db = CreateDbContext();
        await SeedFeedAsync(db, 1);

        var existing = new FeedItem
        {
            FeedId = 1,
            Title = "Existing",
            Link = "https://ex.com/1",
            Hash = "existing_hash"
        };
        db.FeedItems.Add(existing);
        await db.SaveChangesAsync();

        var repo = new ItemRepository(db);
        var newItems = new List<FeedItem>
        {
            new() { FeedId = 1, Title = "New", Link = "https://ex.com/2", Hash = "new_hash" },
            new() { FeedId = 1, Title = "Duplicate", Link = "https://ex.com/1", Hash = "existing_hash" }
        };

        var inserted = await repo.AddNewItemsAsync(newItems);

        inserted.Should().Be(1);
        var all = await db.FeedItems.ToListAsync();
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddNewItemsAsync_ReturnsZeroWhenAllExist()
    {
        using var db = CreateDbContext();
        await SeedFeedAsync(db, 1);

        db.FeedItems.Add(new FeedItem
        {
            FeedId = 1,
            Title = "Existing",
            Link = "https://ex.com/1",
            Hash = "hash"
        });
        await db.SaveChangesAsync();

        var repo = new ItemRepository(db);
        var inserted = await repo.AddNewItemsAsync(
            [new FeedItem { FeedId = 1, Title = "Dup", Link = "https://ex.com/1", Hash = "hash" }]);

        inserted.Should().Be(0);
    }
}
