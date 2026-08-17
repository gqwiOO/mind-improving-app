/**
 * JSON-API адмінки. Не залежить від Astro — приймає звичайні node-запити,
 * тому його однаково легко підключити і як middleware, і окремим сервером.
 */
import { createMarkdownProcessor } from '@astrojs/markdown-remark';
import { readFile } from 'node:fs/promises';
import type { IncomingMessage, ServerResponse } from 'node:http';
import path from 'node:path';
import { COLLECTIONS, getCollection } from './schema';
import { list, read, remove, save } from './store';

export const ADMIN_PATH = '/admin';
const API_PREFIX = '/__admin/';
const UI_DIR = path.join(process.cwd(), 'src', 'admin', 'ui');

/** Date → «2026-08-17», щоб <input type="date"> міг це показати */
function normalize(value: unknown): unknown {
  if (value instanceof Date) return value.toISOString().slice(0, 10);
  if (Array.isArray(value)) return value.map(normalize);
  if (value && typeof value === 'object') {
    return Object.fromEntries(
      Object.entries(value as Record<string, unknown>).map(([key, item]) => [key, normalize(item)]),
    );
  }
  return value;
}

async function readJsonBody(req: IncomingMessage): Promise<Record<string, unknown>> {
  const chunks: Buffer[] = [];
  let size = 0;
  for await (const chunk of req) {
    size += chunk.length;
    // Пости бувають довгі, але не настільки
    if (size > 5_000_000) throw new Error('Забагато даних');
    chunks.push(chunk as Buffer);
  }
  const raw = Buffer.concat(chunks).toString('utf8');
  return raw ? (JSON.parse(raw) as Record<string, unknown>) : {};
}

function sendJson(res: ServerResponse, status: number, payload: unknown) {
  const body = JSON.stringify(payload);
  res.writeHead(status, {
    'content-type': 'application/json; charset=utf-8',
    'cache-control': 'no-store',
  });
  res.end(body);
}

function requireCollection(name: unknown) {
  const collection = getCollection(String(name ?? ''));
  if (!collection) throw new Error(`Невідома колекція: ${name}`);
  return collection;
}

type MarkdownConfig = Parameters<typeof createMarkdownProcessor>[0];

export function createAdminHandler(markdownConfig: MarkdownConfig) {
  let processor: Awaited<ReturnType<typeof createMarkdownProcessor>> | undefined;

  async function renderMarkdown(source: string): Promise<string> {
    processor ??= await createMarkdownProcessor(markdownConfig);
    const { code } = await processor.render(source);
    return code;
  }

  /** @returns true, якщо запит опрацьовано */
  return async function handle(req: IncomingMessage, res: ServerResponse): Promise<boolean> {
    const url = new URL(req.url ?? '/', 'http://localhost');
    const { pathname } = url;

    // Сама сторінка адмінки та її статика
    if (pathname === ADMIN_PATH || pathname === ADMIN_PATH + '/') {
      const html = await readFile(path.join(UI_DIR, 'index.html'), 'utf8');
      res.writeHead(200, { 'content-type': 'text/html; charset=utf-8', 'cache-control': 'no-store' });
      res.end(html);
      return true;
    }

    if (pathname === API_PREFIX + 'ui.css' || pathname === API_PREFIX + 'ui.js') {
      const name = pathname.slice(API_PREFIX.length);
      const body = await readFile(path.join(UI_DIR, name), 'utf8');
      res.writeHead(200, {
        'content-type': name.endsWith('.css') ? 'text/css; charset=utf-8' : 'text/javascript; charset=utf-8',
        'cache-control': 'no-store',
      });
      res.end(body);
      return true;
    }

    if (!pathname.startsWith(API_PREFIX + 'api/')) return false;

    const action = pathname.slice((API_PREFIX + 'api/').length);

    try {
      switch (action) {
        case 'schema':
          sendJson(res, 200, { collections: COLLECTIONS });
          return true;

        case 'entries': {
          const collection = requireCollection(url.searchParams.get('collection'));
          const entries = await list(collection);
          sendJson(res, 200, { entries: normalize(entries) });
          return true;
        }

        case 'entry': {
          const collection = requireCollection(url.searchParams.get('collection'));
          const entry = await read(collection, String(url.searchParams.get('id')));
          sendJson(res, 200, { entry: normalize(entry) });
          return true;
        }

        case 'save': {
          if (req.method !== 'POST') throw new Error('Потрібен POST');
          const body = await readJsonBody(req);
          const collection = requireCollection(body.collection);
          const entry = await save(collection, (body.payload ?? {}) as Record<string, unknown>, {
            id: typeof body.id === 'string' && body.id ? body.id : undefined,
            newId: typeof body.newId === 'string' ? body.newId : undefined,
          });
          sendJson(res, 200, { entry: normalize(entry) });
          return true;
        }

        case 'delete': {
          if (req.method !== 'POST') throw new Error('Потрібен POST');
          const body = await readJsonBody(req);
          const collection = requireCollection(body.collection);
          if (!collection.canDelete) throw new Error('Ця колекція захищена від видалення');
          await remove(collection, String(body.id ?? ''));
          sendJson(res, 200, { ok: true });
          return true;
        }

        case 'preview': {
          if (req.method !== 'POST') throw new Error('Потрібен POST');
          const body = await readJsonBody(req);
          sendJson(res, 200, { html: await renderMarkdown(String(body.markdown ?? '')) });
          return true;
        }

        default:
          sendJson(res, 404, { error: `Невідомий метод: ${action}` });
          return true;
      }
    } catch (error) {
      sendJson(res, 400, { error: error instanceof Error ? error.message : String(error) });
      return true;
    }
  };
}
