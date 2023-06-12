# Rss over Email

Get notified about new posts/articles from sites' rss feed in email!

## Secrets

Feeds, email details are stored in `secrets.json` file, look up `UserSecretsId` property from csproj file. Read more about it at https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-7.0&tabs=windows#how-the-secret-manager-tool-works

The `secrets.json` looks like this:
```json
{
    "mail:from": "a@a.a",
    "mail:to": "b@b.b",
    "mail:server": "some-server.com",
    "mail:username": "a@a.a",
    "mail:password": "super-secret-password",
    "feeds": [
        {
            "name": "Tile of page",
            "url": "https://page1.com/feed/"
        },
        {
            "name": "Other page",
            "url": "https://page2.com/rss"
        }
    ]
}
```
