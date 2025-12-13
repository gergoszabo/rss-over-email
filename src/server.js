import express from 'express';
import fs from 'fs';
import path from 'path';
import { createHash } from 'node:crypto';
import RSSParser from 'rss-parser';
import cron from 'node-cron';
import { Config } from './config.js';
import pino from 'pino';

const logger = pino({
  level: process.env.NODE_ENV === 'production' ? 'info' : 'debug',
});

// Centralized error handling for uncaught exceptions
process.on('uncaughtException', (err) => {
  logger.error({ err }, 'Uncaught Exception detected. Shutting down...');
  process.exit(1);
});

// Centralized error handling for unhandled promise rejections
process.on('unhandledRejection', (reason, promise) => {
  logger.error({ reason, promise }, 'Unhandled Rejection detected. Shutting down...');
  process.exit(1);
});

const app = express();
const port = 3000;

const DB_FILE = process.env.DB_FILE_PATH || path.join(process.cwd(), 'serverdb.json');
const parser = new RSSParser();

// Helper function to escape HTML special characters
const escapeHtml = (unsafe) => {
  const str = String(unsafe); // Convert to string to handle null/undefined
  return str
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
};

// Helper function to generate HTML for an item
const generateItemHtml = (item, previousItem, nextItem, previousUnreadItem, nextUnreadItem) => {
  let pubdateFormatted = '';
  if (item.pubDate) {
    const date = new Date(item.pubDate);
    pubdateFormatted = `${date.toISOString().substring(0, 10)} ${date.toTimeString().substring(0, 5)}`;
  }

  let navHtml = '';
  if (previousItem) {
    navHtml += `<a href="/items/${previousItem.id}">Previous</a> | `;
  }
  if (nextItem) {
    navHtml += `<a href="/items/${nextItem.id}">Next</a> | `;
  }
  if (previousUnreadItem) {
    navHtml += `<a href="/items/${previousUnreadItem.id}">Previous Unread</a> | `;
  }
  if (nextUnreadItem) {
    navHtml += `<a href="/items/${nextUnreadItem.id}">Next Unread</a>`;
  }

  return `
    <!DOCTYPE html>
    <html lang="en">
    <head>
      <meta charset="UTF-8">
      <meta name="viewport" content="width=device-width, initial-scale=1.0">
      <title>${escapeHtml(item.title)}</title>
      <style>
        body { font-family: sans-serif; margin: 1em; }
        a { display: inline-block; padding: 0.5em 1em; margin: 0.2em; border: 1px solid #ccc; text-decoration: none; color: #333; border-radius: 4px; font-size: 1.1em; }
        a:hover { background-color: #eee; }
        h1 { font-size: 1.5em; }
      </style>
    </head>
    <body>
      <h1>${escapeHtml(item.title)}</h1>
      <div>${pubdateFormatted}</div>
      <div><a href="${escapeHtml(item.link)}">${escapeHtml(item.link)}</a></div>
      ${item.comments && !item.comments.startsWith(item.link) ? `<div>${escapeHtml(item.comments)}</div>` : ''}
      <br>
      <div>${navHtml}</div>
    </body>
    </html>
  `;
};

// Helper functions for database operations
const readItems = () => {
  try {
    const data = fs.readFileSync(DB_FILE, 'utf8');
    return JSON.parse(data);
  } catch (error) {
    logger.error({ error: error.message }, 'Error reading database file');
    return [];
  }
};

const writeItems = (items) => {
  try {
    fs.writeFileSync(DB_FILE, JSON.stringify(items, null, 2), 'utf8');
  } catch (error) {
    logger.error({ error: error.message }, 'Error writing to database file');
  }
};

const findNextUnreadItem = (currentId = null) => {
  const items = readItems();
  let startIndex = 0;
  if (currentId) {
    const currentIndex = items.findIndex((item) => item.id === currentId);
    if (currentIndex !== -1) {
      startIndex = currentIndex + 1;
    }
  }
  for (let i = startIndex; i < items.length; i++) {
    if (!items[i].read) {
      return items[i];
    }
  }
  return null;
};

const findPreviousUnreadItem = (currentId) => {
  const items = readItems();
  const currentIndex = items.findIndex((item) => item.id === currentId);
  if (currentIndex === -1) {
    return null;
  }
  for (let i = currentIndex - 1; i >= 0; i--) {
    if (!items[i].read) {
      return items[i];
    }
  }
  return null;
};

const markItemAsRead = (id) => {
  const items = readItems();
  const itemIndex = items.findIndex((item) => item.id === id);
  if (itemIndex > -1) {
    items[itemIndex].read = true;
    writeItems(items);
    logger.info({ itemId: id }, 'Item marked as read');
    return true;
  }
  logger.warn({ itemId: id }, 'Attempted to mark non-existent item as read');
  return false;
};

const findItemById = (id) => {
  const items = readItems();
  return items.find((item) => item.id === id);
};

const findPreviousItem = (id) => {
  const items = readItems();
  const currentIndex = items.findIndex((item) => item.id === id);
  if (currentIndex > 0) {
    return items[currentIndex - 1];
  }
  return null;
};

const findNextItem = (id) => {
  const items = readItems();
  const currentIndex = items.findIndex((item) => item.id === id);
  if (currentIndex !== -1 && currentIndex < items.length - 1) {
    return items[currentIndex + 1];
  }
  return null;
};

const fetchFeeds = async () => {
  logger.info('Fetching RSS feeds...');
  let currentItems = readItems();
  const initialItemCount = currentItems.length;
  const newlyAddedLinks = [];

  for (const feedConfig of Config.feeds) {
    logger.info({ name: feedConfig.name, url: feedConfig.url }, 'Processing feed');
    try {
      const feed = await parser.parseURL(feedConfig.url);
      feed.items.forEach((item) => {
        const existingItem = currentItems.find((i) => i.id === item.id || i.link === item.link);
        if (!existingItem) {
          const idSource = item.link || item.guid || item.title || Date.now().toString();
          const newItem = {
            id: createHash('sha256').update(idSource).digest('hex'),
            title: item.title || '',
            link: item.link || '',
            comments: item.comments || '',
            pubDate: item.pubDate || '',
            read: false,
          };
          currentItems.push(newItem);
          newlyAddedLinks.push(newItem.link);
          logger.debug(
            { id: newItem.id, title: newItem.title, link: newItem.link },
            'Added new item'
          );
        }
      });
    } catch (error) {
      logger.error(
        { feed: feedConfig.name, error: error.message },
        `Error fetching feed ${feedConfig.name}`
      );
    }
  }
  writeItems(currentItems);
  const newItemsCount = currentItems.length - initialItemCount;
  logger.info(
    { totalItemsAdded: newItemsCount, newlyAddedLinks },
    'RSS feeds fetched and database updated'
  );
};

app.get('/', (req, res) => {
  logger.info('GET / request received');
  const nextItem = findNextUnreadItem();

  if (nextItem) {
    logger.info({ itemId: nextItem.id }, 'Serving first unread item');
    markItemAsRead(nextItem.id);

    const previousItem = findPreviousItem(nextItem.id);
    const nextItemFull = findNextItem(nextItem.id);
    const previousUnreadItem = findPreviousUnreadItem(nextItem.id);
    const nextUnreadItem = findNextUnreadItem(nextItem.id);

    res.send(
      generateItemHtml(nextItem, previousItem, nextItemFull, previousUnreadItem, nextUnreadItem)
    );
  } else {
    logger.info('No unread items available');
    res.status(204).send(); // No content
  }
});

app.get('/items/next', (req, res) => {
  logger.info('GET /items/next request received');
  const nextItem = findNextUnreadItem();
  if (nextItem) {
    logger.info({ itemId: nextItem.id }, 'Serving next unread item');
    markItemAsRead(nextItem.id);

    let pubdateFormatted = '';
    if (nextItem.pubDate) {
      const date = new Date(nextItem.pubDate);
      pubdateFormatted = `${date.toISOString().substring(0, 10)} ${date.toTimeString().substring(0, 5)}`;
    }

    const htmlResponse = `
      <!DOCTYPE html>
      <html lang="en">
      <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>${escapeHtml(nextItem.title || '')}</title>
        <style>
          body { font-family: sans-serif; margin: 1em; }
          a { display: inline-block; padding: 0.5em 1em; margin: 0.2em; border: 1px solid #ccc; text-decoration: none; color: #333; border-radius: 4px; font-size: 1.1em; }
          a:hover { background-color: #eee; }
          h1 { font-size: 1.5em; }
        </style>
      </head>
      <body>
        <h1>${escapeHtml(nextItem.title || '')}</h1>
        <div>${pubdateFormatted}</div>
        <div><a href="${escapeHtml(nextItem.link || '')}">${escapeHtml(nextItem.link || '')}</a></div>
        ${nextItem.comments && !nextItem.comments.startsWith(nextItem.link) ? `<div>${escapeHtml(nextItem.comments)}</div>` : ''}
        <br>
        <div></div>
      </body>
      </html>
    `;
    res.send(htmlResponse);
  } else {
    logger.info('No unread items available for /items/next');
    res.status(204).send(); // No content
  }
});

app.get('/items/:id', (req, res) => {
  const itemId = req.params.id;
  logger.info({ itemId }, 'GET /items/:id request received');

  // Input validation for itemId
  // Now expecting a SHA256 hash (64 hexadecimal characters)
  if (!itemId || !/^[a-fA-F0-9]{64}$/.test(itemId)) {
    logger.warn({ itemId }, 'Invalid item ID format received');
    return res.status(400).send('Invalid item ID.');
  }

  const item = findItemById(itemId);

  if (item) {
    logger.info({ itemId }, 'Serving item by ID');
    markItemAsRead(itemId);

    const previousItem = findPreviousItem(itemId);
    const nextItem = findNextItem(itemId);
    const previousUnreadItem = findPreviousUnreadItem(itemId);
    const nextUnreadItem = findNextUnreadItem(itemId);

    res.send(generateItemHtml(item, previousItem, nextItem, previousUnreadItem, nextUnreadItem));
  } else {
    logger.warn({ itemId }, 'Item not found');
    res.status(404).send('Item not found.');
  }
});

app.listen(port, () => {
  logger.info({ port }, `Server listening at http://localhost:${port}`);
  // Initial fetch on startup
  fetchFeeds();
  // Schedule feed fetching
  const fetchInterval = Config.fetchIntervalHours || 1; // Default to 1 hour
  cron.schedule(`0 */${fetchInterval} * * *`, () => {
    fetchFeeds();
  });
});
