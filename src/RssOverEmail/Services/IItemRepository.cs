using RssOverEmail.Dto;
using RssOverEmail.Models;

namespace RssOverEmail.Services;

public interface IItemRepository
{
    Task<PaginatedResponse<FeedItemResponse>> GetItemsAsync(
        int? feedId, bool? isRead, int page, int pageSize, CancellationToken ct = default);

    Task<FeedItem?> GetByIdAsync(int id, CancellationToken ct = default);

    Task MarkReadAsync(int id, bool isRead, CancellationToken ct = default);

    Task<int> AddNewItemsAsync(IEnumerable<FeedItem> items, CancellationToken ct = default);
}
