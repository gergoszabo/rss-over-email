using System.Text;

internal class Persistence
{
    public static IEnumerable<RssItem> getNewItems(IEnumerable<RssItem> rssItems)
    {
        var newItems = new List<RssItem>();
        foreach (var rssItem in rssItems)
        {
            var files = Directory.EnumerateFiles("./data").ToList();
            var fileOnDisk = files.FirstOrDefault(f => f.EndsWith(rssItem.ID));
            if (fileOnDisk != null)
            {
                var lastWriteTime = File.GetLastWriteTime(fileOnDisk);
                if (lastWriteTime != rssItem.PubDate)
                {
                    Logger.log($"Updating writetime of {fileOnDisk} to {rssItem.PubDate}");
                    File.SetLastWriteTime(fileOnDisk, rssItem.PubDate);
                }
            }
            else
            {
                var filename = $"./data/{rssItem.ID}";
                var fileStream = File.Create(filename);

                var bytes = Encoding.UTF8.GetBytes(rssItem.Title ?? "");
                fileStream.Write(bytes, 0, bytes.Length);
                fileStream.Close();
                File.SetLastWriteTime(filename, rssItem.PubDate);

                newItems.Add(rssItem);
            }
        }

        return newItems;
    }
}
