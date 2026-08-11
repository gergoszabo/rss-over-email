namespace RssOverEmail.Models;

public class RssOverEmailOptions
{
    public const string SectionName = "RssOverEmail";

    public List<FeedConfig> Feeds { get; set; } = [];
    public int FetchIntervalMinutes { get; set; } = 15;
}
