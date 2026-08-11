namespace RssOverEmail.Services;

public interface IFeedFetcher
{
    Task<string> FetchXmlAsync(string url, CancellationToken ct = default);
}
