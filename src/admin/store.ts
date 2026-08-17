/**
 * Читання й запис контенту з диска для адмінки.
 *
 * Працює з тими самими файлами, що й сайт: markdown-колекції — один файл на
 * запис, yaml-колекції — спільний файл-масив. Коментарі в yaml зберігаються.
 */
import { parseFrontmatter } from '@astrojs/markdown-remark';
import { readdir, readFile, unlink, writeFile } from 'node:fs/promises';
import path from 'node:path';
import YAML from 'yaml';
import { isValidId, slugify, uniqueId } from './slug';
import type { Collection, Field } from './schema';

export interface Entry {
  id: string;
  data: Record<string, unknown>;
  /** Тільки для markdown-колекцій */
  body?: string;
}

const ROOT = process.cwd();

function resolveInsideRoot(...segments: string[]): string {
  const full = path.resolve(ROOT, ...segments);
  const rootWithSep = ROOT.endsWith(path.sep) ? ROOT : ROOT + path.sep;
  if (!full.startsWith(rootWithSep)) {
    throw new Error('Шлях виходить за межі проєкту');
  }
  return full;
}

function fileFor(collection: Collection, id: string): string {
  if (!isValidId(id)) throw new Error(`Недопустимий ідентифікатор: ${id}`);
  return resolveInsideRoot(collection.path, `${id}.md`);
}

/**
 * Приводить значення з форми до типу, який очікує content.config.ts.
 * Порожні значення викидаємо, щоб не засмічувати файли `field: ''`.
 */
function coerce(field: Field, raw: unknown): unknown {
  if (raw === undefined || raw === null) return undefined;

  switch (field.widget) {
    case 'boolean':
      return raw === true || raw === 'true' ? true : undefined;

    case 'number': {
      if (raw === '') return undefined;
      const parsed = typeof raw === 'number' ? raw : Number(raw);
      return Number.isFinite(parsed) ? parsed : undefined;
    }

    case 'list': {
      const items = Array.isArray(raw)
        ? raw.map(String)
        : String(raw)
            .split(',')
            .map((item) => item.trim());
      const cleaned = items.filter(Boolean);
      return cleaned.length ? cleaned : undefined;
    }

    default: {
      const text = String(raw).trim();
      return text === '' ? undefined : text;
    }
  }
}

/** Розкладає плоский об’єкт із форми на frontmatter і тіло */
function splitPayload(collection: Collection, payload: Record<string, unknown>) {
  const data: Record<string, unknown> = {};
  let body = '';

  for (const field of collection.fields) {
    if (field.widget === 'markdown') {
      body = String(payload[field.name] ?? '').replace(/\r\n/g, '\n');
      continue;
    }
    const value = coerce(field, payload[field.name]);
    if (value !== undefined) data[field.name] = value;
  }

  return { data, body };
}

function checkRequired(collection: Collection, data: Record<string, unknown>, body: string) {
  for (const field of collection.fields) {
    if (!field.required) continue;
    const filled = field.widget === 'markdown' ? body.trim() !== '' : data[field.name] !== undefined;
    if (!filled) throw new Error(`Поле «${field.label}» обов’язкове`);
  }
}

// ---------------------------------------------------------------- markdown

async function listMarkdown(collection: Collection): Promise<Entry[]> {
  const dir = resolveInsideRoot(collection.path);
  let files: string[];
  try {
    files = await readdir(dir);
  } catch {
    return [];
  }

  const entries: Entry[] = [];
  for (const file of files) {
    if (!file.endsWith('.md')) continue;
    const id = file.slice(0, -3);
    const raw = await readFile(path.join(dir, file), 'utf8');
    const { frontmatter, content } = parseFrontmatter(raw);
    entries.push({ id, data: frontmatter as Record<string, unknown>, body: content.trim() });
  }
  return entries;
}

async function writeMarkdown(
  collection: Collection,
  id: string,
  data: Record<string, unknown>,
  body: string,
) {
  // yaml.stringify лишає дати без лапок, тому дати передаємо рядками
  const frontmatter = YAML.stringify(data).trimEnd();
  const contents = `---\n${frontmatter}\n---\n\n${body.trim()}\n`;
  await writeFile(fileFor(collection, id), contents, 'utf8');
}

// -------------------------------------------------------------------- yaml

/**
 * Файли даних виглядають так:
 *
 *   books:
 *     - id: …
 *
 * Кореневий ключ (той самий, що в content.config.ts) потрібен, щоб файл
 * розуміли зовнішні CMS — голий список вони редагувати не вміють.
 */
async function readDocument(collection: Collection) {
  const key = collection.rootKey;
  if (!key) throw new Error(`У колекції «${collection.name}» не вказано rootKey`);

  const file = resolveInsideRoot(collection.path);
  let raw: string;
  try {
    raw = await readFile(file, 'utf8');
  } catch {
    raw = `${key}: []\n`;
  }

  const doc = YAML.parseDocument(raw);
  const seq = YAML.isMap(doc.contents) ? doc.contents.get(key) : undefined;
  if (!YAML.isSeq(seq)) {
    throw new Error(`${collection.path}: очікував список під ключем «${key}»`);
  }
  // Після parseDocument items типізовані як ParsedNode, і в них не можна класти
  // свіжий doc.createNode(). Розширюємо тип до звичайного вузла — так і є в рантаймі.
  return { file, doc, seq: seq as YAML.YAMLSeq };
}

function idOf(node: unknown): string | undefined {
  if (!YAML.isMap(node)) return undefined;
  const value = node.get('id');
  return typeof value === 'string' ? value : undefined;
}

async function listYaml(collection: Collection): Promise<Entry[]> {
  const { seq } = await readDocument(collection);
  return seq.items.flatMap((node) => {
    if (!YAML.isMap(node)) return [];
    const plain = node.toJSON() as Record<string, unknown>;
    const { id, ...data } = plain;
    return typeof id === 'string' ? [{ id, data }] : [];
  });
}

// ------------------------------------------------------------------ public

/**
 * Порівняння «найновіше зверху». Дати з frontmatter приходять об’єктами Date,
 * роки — числами, тому порівнювати їх як рядки не можна.
 */
function descending(left: unknown, right: unknown): number {
  // Записи без значення (напр. книжка «читаю зараз» без року) — угорі
  if (left === undefined || left === null) return right === undefined || right === null ? 0 : -1;
  if (right === undefined || right === null) return 1;

  if (left instanceof Date && right instanceof Date) return right.getTime() - left.getTime();
  if (typeof left === 'number' && typeof right === 'number') return right - left;

  return String(right).localeCompare(String(left), 'uk', { numeric: true });
}

export async function list(collection: Collection): Promise<Entry[]> {
  const entries =
    collection.kind === 'markdown' ? await listMarkdown(collection) : await listYaml(collection);

  const key = collection.sortBy;
  if (!key) return entries;

  return entries.sort((a, b) => descending(a.data[key], b.data[key]));
}

export async function read(collection: Collection, id: string): Promise<Entry> {
  const entries = await list(collection);
  const entry = entries.find((candidate) => candidate.id === id);
  if (!entry) throw new Error(`Запис «${id}» не знайдено`);
  return entry;
}

/**
 * Створює або оновлює запис.
 * `id` порожній → створюємо новий, ідентифікатор робимо із заголовка.
 * `id` змінився → для markdown це перейменування файлу (а отже, і адреси).
 */
export async function save(
  collection: Collection,
  payload: Record<string, unknown>,
  options: { id?: string; newId?: string } = {},
): Promise<Entry> {
  const { data, body } = splitPayload(collection, payload);
  checkRequired(collection, data, body);

  const existing = await list(collection);
  const taken = existing.map((entry) => entry.id).filter((id) => id !== options.id);

  const requested = options.newId?.trim() || options.id || slugify(String(data.title ?? ''));
  if (requested && !isValidId(requested)) {
    throw new Error('Адреса може містити тільки латиницю, цифри й дефіс');
  }
  const id = uniqueId(requested || slugify(String(data.title ?? '')), taken);

  if (collection.kind === 'markdown') {
    await writeMarkdown(collection, id, data, body);
    if (options.id && options.id !== id) {
      await unlink(fileFor(collection, options.id)).catch(() => {});
    }
    return { id, data, body };
  }

  const { file, doc, seq } = await readDocument(collection);
  const node = doc.createNode({ id, ...data });
  const index = options.id ? seq.items.findIndex((item) => idOf(item) === options.id) : -1;

  if (index >= 0) {
    // Зберігаємо коментар і відступ, які автор зробив навколо цього запису
    const previous = seq.items[index];
    if (YAML.isMap(previous) && YAML.isMap(node)) {
      if (previous.commentBefore) node.commentBefore = previous.commentBefore;
      node.spaceBefore = previous.spaceBefore;
    }
    seq.items[index] = node;
  } else {
    // Дописуємо в кінець: так коментарі-заголовки у файлі лишаються на місці.
    // Порожній рядок перед записом — щоб файл читався так само, як писаний руками.
    if (YAML.isMap(node)) node.spaceBefore = seq.items.length > 0;
    seq.items.push(node);
  }

  await writeFile(file, doc.toString(), 'utf8');
  return { id, data };
}

export async function remove(collection: Collection, id: string): Promise<void> {
  if (collection.kind === 'markdown') {
    await unlink(fileFor(collection, id));
    return;
  }

  const { file, doc, seq } = await readDocument(collection);
  const index = seq.items.findIndex((item) => idOf(item) === id);
  if (index < 0) throw new Error(`Запис «${id}» не знайдено`);
  seq.items.splice(index, 1);
  await writeFile(file, doc.toString(), 'utf8');
}
