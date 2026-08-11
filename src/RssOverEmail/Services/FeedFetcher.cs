namespace RssOverEmail.Services;

public class FeedFetcher(HttpClient httpClient) : IFeedFetcher
{
    public async Task<string> FetchXmlAsync(string url, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
