using System.Reflection;
using Microsoft.Extensions.Configuration;

var attribute = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
var informationVersion = attribute?.InformationalVersion;
Console.WriteLine($"Rss over Email v{informationVersion}");

var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

String feedsKey = "feeds";

var feedsConfig = config.GetSection(feedsKey);

foreach (var section in feedsConfig.GetChildren())
{
    String key = $"{feedsKey}:{section.Key}";
    System.Console.WriteLine(key);

    String? url = config.GetValue<String>($"{key}:url");
    String? name = config.GetValue<String>($"{key}:name");

    var rssItems = await Rss.getItems(url);
    var newItems = Persistence.getNewItems(rssItems);
    if (newItems.Count() == 0)
    {
        Logger.log($"No items to send for {url}!");
        continue;
    }

    Mail.sendRssItems(config, name, newItems);
}

class Feed
{
    public String? name { get; set; }
    public String? url { get; set; }

    public override string ToString()
    {
        return $"{name} {url}";
    }
}
