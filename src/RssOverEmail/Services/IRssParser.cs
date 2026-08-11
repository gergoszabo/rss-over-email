using RssOverEmail.Models;

namespace RssOverEmail.Services;

public interface IRssParser
{
    IReadOnlyList<FeedItem> Parse(string xml, int feedId);
}
