using RssOverEmail.Models;

namespace RssOverEmail.Services;

public interface IFeedRepository
{
    Task<List<Feed>> GetAllAsync(CancellationToken ct = default);
    Task<Feed?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<Feed> AddAsync(Feed feed, CancellationToken ct = default);
}
