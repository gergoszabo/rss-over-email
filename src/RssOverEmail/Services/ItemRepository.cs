using Microsoft.EntityFrameworkCore;
using RssOverEmail.Data;
using RssOverEmail.Dto;
using RssOverEmail.Models;

namespace RssOverEmail.Services;

public class ItemRepository(AppDbContext db) : IItemRepository
{
    public async Task<PaginatedResponse<FeedItemResponse>> GetItemsAsync(
        int? feedId, bool? isRead, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.FeedItems
            .Include(i => i.Feed)
            .AsQueryable();

        if (feedId.HasValue)
            query = query.Where(i => i.FeedId == feedId.Value);

        if (isRead.HasValue)
            query = query.Where(i => i.IsRead == isRead.Value);

        query = query.OrderByDescending(i => i.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new FeedItemResponse
            {
                Id = i.Id,
                FeedName = i.Feed.Name,
                Title = i.Title,
                Link = i.Link,
                Comments = i.Comments,
                PubDate = i.PubDate,
                IsRead = i.IsRead,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync(ct);

        return new PaginatedResponse<FeedItemResponse>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<FeedItem?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await db.FeedItems.FindAsync([id], ct);
    }

    public async Task MarkReadAsync(int id, bool isRead, CancellationToken ct = default)
    {
        var item = await db.FeedItems.FindAsync([id], ct);
        if (item is not null)
        {
            item.IsRead = isRead;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<int> AddNewItemsAsync(IEnumerable<FeedItem> items, CancellationToken ct = default)
    {
        var existingHashes = await db.FeedItems
            .Select(i => i.Hash)
            .ToHashSetAsync(ct);

        var newItems = items
            .Where(i => !existingHashes.Contains(i.Hash))
            .ToList();

        if (newItems.Count == 0)
            return 0;

        db.FeedItems.AddRange(newItems);
        await db.SaveChangesAsync(ct);
        return newItems.Count;
    }
}
