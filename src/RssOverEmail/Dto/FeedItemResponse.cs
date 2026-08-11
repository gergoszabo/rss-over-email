namespace RssOverEmail.Dto;

public class FeedItemResponse
{
    public int Id { get; set; }
    public string FeedName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string? Comments { get; set; }
    public string? PubDate { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
