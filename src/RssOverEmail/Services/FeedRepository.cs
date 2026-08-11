using Microsoft.EntityFrameworkCore;
using RssOverEmail.Data;
using RssOverEmail.Models;

namespace RssOverEmail.Services;

public class FeedRepository(AppDbContext db) : IFeedRepository
{
    public async Task<List<Feed>> GetAllAsync(CancellationToken ct = default)
    {
        return await db.Feeds.ToListAsync(ct);
    }

    public async Task<Feed?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        return await db.Feeds.FirstOrDefaultAsync(f => f.Name == name, ct);
    }

    public async Task<Feed> AddAsync(Feed feed, CancellationToken ct = default)
    {
        db.Feeds.Add(feed);
        await db.SaveChangesAsync(ct);
        return feed;
    }
}
