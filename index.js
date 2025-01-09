import { createTransport } from 'nodemailer';
import { XMLParser } from 'fast-xml-parser';
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { Config } from './config.js';

const DB_FILENAME = './db.json';

if (!existsSync(DB_FILENAME)) {
    writeFileSync(DB_FILENAME, JSON.stringify([]), { encoding: 'utf8' });
}

const db = JSON.parse(readFileSync(DB_FILENAME, { encoding: 'utf8' }));

const transporter = createTransport({
    host: Config.mail.server,
    port: 587,
    secure: false,
    auth: {
        user: Config.mail.username,
        pass: Config.mail.password,
    },
});

(async () => {
    const fragments = [];
    for (let i = 0; i < Config.feeds.length; i++) {
        const feed = Config.feeds[i];

        const xml = await (await fetch(feed.url)).text();

        const doc = new XMLParser().parse(xml);

        const _rssItems =
            'rss' in doc && 'item' in doc.rss
                ? doc.rss.item
                : 'rss' in doc &&
                  'channel' in doc.rss &&
                  'item' in doc.rss.channel
                ? doc.rss.channel.item
                : [];

        fragments.push(
            _rssItems
                .map((item) => ({
                    title: item.title,
                    link: item.link,
                    pubDate: new Date(item.pubDate)
                        .toISOString()
                        .substring(0, 16)
                        .replace('T', ' '),
                    comments:
                        'comments' in item &&
                        !item.comments.startsWith(item.link)
                            ? `<br><a href="${item.comments}">${item.comments}</a>`
                            : '',
                    id: createHash('sha256')
                        .update(`${item.title}-${item.link}-${item.pubDate}`)
                        .digest('hex'),
                }))
                .filter((item) => {
                    if (!db.find((p) => p.id === item.id)) {
                        db.push(item);
                        return true;
                    }
                    return false;
                })
                .map(
                    (item) =>
                        `<div>${item.pubDate} - ${item.title}<br><a href="${item.link}">${item.link}</a>${item.comments}</div>`
                )
                .join('<br>')
        );
    }

    if (fragments.length) {
        const info = await transporter.sendMail({
            from: Config.mail.from,
            to: Config.mail.to,
            subject: 'RSS',
            html: fragments.join('<br>'),
        });

        console.log(`Message sent ${info.messageId}`);
    }

    writeFileSync(DB_FILENAME, JSON.stringify(db, null, 4), {
        encoding: 'utf8',
    });
})();
