import * as cheerio from 'cheerio';
import { createTransport } from 'nodemailer';
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

for (let i = 0; i < Config.feeds.length; i++) {
    const feed = Config.feeds[i];

    const xml = await (await fetch(feed.url)).text();

    const $ = cheerio.load(xml);

    const $items = $('item');

    let html = '';
    for (const item of $items) {
        const title = $(item.children.find(ch => ch.name === 'title')).text().replace('<![CDATA[', '').replace(']]>', '').trim();
        const link = $(item.children.find(ch => ch.name === 'link').next).text().trim();
        let pubdate = $(item.children.find(ch => ch.name && ch.name.toLowerCase() === 'pubdate')).text().trim();
        pubdate = new Date(Date.parse(pubdate)).toISOString().substring(0, 16).replace('T', ' ');
        let comments = item.children.find(ch => ch.name === 'comments');
        if (comments) {
            comments = $(comments).text().trim();
            if (comments) {
                comments = `<br><a href="${comments}">${comments}</a>`;
            }
        }

        const id = createHash('sha256').update(link).digest('hex');

        if (!db.find(p => p.id === id)) {
            db.push({
                id,
                title,
                link,
                comments
            });
            html += `<div>${pubdate} - ${title}<br><a href="${link}">${link}</a>${comments || ''}</div><br>`;
        }
    }

    if (html) {
        const info = await transporter.sendMail({
            from: Config.mail.from,
            to: Config.mail.to,
            subject: feed.name,
            html
        });

        console.log(`Message sent ${feed.name} ${info.messageId}`);
    }
}

writeFileSync(DB_FILENAME, JSON.stringify(db, null, 4), { encoding: 'utf8' });
