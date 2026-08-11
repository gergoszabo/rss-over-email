using Microsoft.Extensions.Options;
using RssOverEmail.Models;

namespace RssOverEmail.Services;

public class RssFetchService(
    IFeedFetcher feedFetcher,
    IRssParser rssParser,
    IFeedRepository feedRepository,
    IItemRepository itemRepository,
    IOptionsMonitor<RssOverEmailOptions> options,
    ILogger<RssFetchService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RssFetchService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnsureFeedsExistAsync(stoppingToken);
                await FetchAndStoreAllFeedsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error during feed fetch cycle");
            }

            var interval = TimeSpan.FromMinutes(
                Math.Max(1, options.CurrentValue.FetchIntervalMinutes));

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task EnsureFeedsExistAsync(CancellationToken ct)
    {
        var configuredFeeds = options.CurrentValue.Feeds;
        var existingFeeds = await feedRepository.GetAllAsync(ct);

        foreach (var configured in configuredFeeds)
        {
            if (existingFeeds.All(f => f.Name != configured.Name))
            {
                await feedRepository.AddAsync(new Feed
                {
                    Name = configured.Name,
                    Url = configured.Url
                }, ct);

                logger.LogInformation("Added feed: {Name}", configured.Name);
            }
        }
    }

    private async Task FetchAndStoreAllFeedsAsync(CancellationToken ct)
    {
        var feeds = await feedRepository.GetAllAsync(ct);

        foreach (var feed in feeds)
        {
            try
            {
                var xml = await feedFetcher.FetchXmlAsync(feed.Url, ct);
                var items = rssParser.Parse(xml, feed.Id);
                var newItems = items.Where(i => i.Link is not null).ToList();

                if (newItems.Count != 0)
                {
                    await itemRepository.AddNewItemsAsync(newItems, ct);
                    logger.LogInformation(
                        "Stored {Count} new items from feed: {Name}", newItems.Count, feed.Name);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to fetch/parse feed: {Name} ({Url})", feed.Name, feed.Url);
            }
        }
    }
}
