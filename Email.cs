using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;

internal class Mail
{
    public static void sendRssItems(IConfigurationRoot config, String? name, IEnumerable<RssItem> rssItems)
    {
        var smtpClient = new SmtpClient();
        var message = new MimeMessage();

        message.From.Add(new MailboxAddress(config.GetValue<string>("mail:from"), config.GetValue<string>("mail:from")));
        message.To.Add(new MailboxAddress(config.GetValue<string>("mail:to"), config.GetValue<string>("mail:to")));
        message.Subject = name;
        message.Body = new TextPart("html")
        {
            Text = rssItems.Select(item => item.ToEmailString()).Aggregate((acc, item) => $"{acc}<br>{item}")
        };

        using (var client = new SmtpClient())
        {
            client.Connect(config.GetValue<string>("mail:server"), 587, false);
            client.Authenticate(config.GetValue<string>("mail:username"), config.GetValue<string>("mail:password"));
            Logger.log($"Sending {rssItems.Count()} items");

            var response = client.Send(message);

            Logger.log($"Mail sent {response}");
            client.Disconnect(true);
        }
    }
}
