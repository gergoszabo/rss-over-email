# Rss over Email

Get notified about new posts/articles from sites' rss feed in email! This is a NodeJS (20), ESM project with NodeMailer and Cheerio dependencies with esbuild bundler.

## Secrets

Feeds, email details are stored in `config.js` file, look up `config.sample.js` file for example.

Either run `npm run start` after you install dependencies or run `npm run build` to bundle files (and secrets too) and copy the result `rss-over-email.js` to the destination server.
