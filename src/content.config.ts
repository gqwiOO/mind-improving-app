import { defineCollection } from 'astro:content';
import { file, glob } from 'astro/loaders';
import { z } from 'astro/zod';
import YAML from 'yaml';

/**
 * Файли даних мають кореневий ключ (`books:`, `projects:`, …), а не голий
 * список. Так їх розуміє і Astro, і зовнішні CMS — ті не вміють редагувати
 * файл, у якого корінь є масивом.
 */
const listUnder = (key: string) => (text: string) => {
  const parsed = YAML.parse(text) as Record<string, unknown> | null;
  const items = parsed?.[key];
  if (!Array.isArray(items)) {
    throw new Error(`Очікував список під ключем «${key}»`);
  }
  return items as Record<string, unknown>[];
};

/** Пости блогу — markdown-файли в src/content/posts/ */
const posts = defineCollection({
  loader: glob({ pattern: '**/*.md', base: './src/content/posts' }),
  schema: z.object({
    title: z.string(),
    date: z.coerce.date(),
    /** Короткий опис для головної, RSS і пошуку */
    description: z.string().optional(),
    tags: z.array(z.string()).default([]),
    /** Чернетка не потрапляє в збірку */
    draft: z.boolean().default(false),
  }),
});

/** TIL — короткі замітки на 2-3 речення, src/content/til/ */
const til = defineCollection({
  loader: glob({ pattern: '**/*.md', base: './src/content/til' }),
  schema: z.object({
    title: z.string(),
    date: z.coerce.date(),
    tags: z.array(z.string()).default([]),
    /** Джерело, звідки дізнався */
    source: z.url().optional(),
  }),
});

/** Статичні сторінки: about, now, uses — src/content/pages/ */
const pages = defineCollection({
  loader: glob({ pattern: '**/*.md', base: './src/content/pages' }),
  schema: z.object({
    title: z.string(),
    description: z.string().optional(),
    updated: z.coerce.date().optional(),
  }),
});

/** Книги — один YAML-файл, src/data/books.yaml */
const books = defineCollection({
  loader: file('src/data/books.yaml', { parser: listUnder('books') }),
  schema: z.object({
    title: z.string(),
    /** Рік, коли прочитав. Для status: reading можна не вказувати */
    year: z.number().int().optional(),
    /** Оцінка 0–5, дозволені половинки: 4.5 */
    rating: z.number().min(0).max(5).optional(),
    /** Власне враження, вільний текст */
    note: z.string().optional(),
    /** Посилання на Goodreads / видавця / будь-куди */
    url: z.url().optional(),
    status: z.enum(['read', 'reading']).default('read'),
  }),
});

/** Проєкти — src/data/projects.yaml */
const projects = defineCollection({
  loader: file('src/data/projects.yaml', { parser: listUnder('projects') }),
  schema: z.object({
    title: z.string(),
    description: z.string(),
    url: z.url().optional(),
    repo: z.url().optional(),
    year: z.number().int().optional(),
    /** Наприклад: Unity, C#, Astro */
    stack: z.array(z.string()).default([]),
    status: z.enum(['active', 'done', 'paused']).default('active'),
  }),
});

/** Blogroll і корисні посилання — src/data/links.yaml */
const links = defineCollection({
  loader: file('src/data/links.yaml', { parser: listUnder('links') }),
  schema: z.object({
    title: z.string(),
    url: z.url(),
    note: z.string().optional(),
    /** Група, під якою показувати: "Блоги", "Інструменти", ... */
    group: z.string().default('Інше'),
  }),
});

export const collections = { posts, til, pages, books, projects, links };
