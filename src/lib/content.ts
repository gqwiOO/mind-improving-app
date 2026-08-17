import { getCollection, type CollectionEntry } from 'astro:content';

export type Post = CollectionEntry<'posts'>;
export type Til = CollectionEntry<'til'>;
export type Book = CollectionEntry<'books'>;

/** Чернетки видно тільки в `astro dev` */
const showDrafts = import.meta.env.DEV;

/** Усі пости, найновіші зверху */
export async function getPosts(): Promise<Post[]> {
  const posts = await getCollection('posts', ({ data }) => showDrafts || !data.draft);
  return posts.sort((a, b) => b.data.date.getTime() - a.data.date.getTime());
}

/** Усі TIL-замітки, найновіші зверху */
export async function getTils(): Promise<Til[]> {
  const tils = await getCollection('til');
  return tils.sort((a, b) => b.data.date.getTime() - a.data.date.getTime());
}

/** Прочитані книги, найновіші зверху (у межах року — в порядку файлу, знизу вгору) */
export async function getReadBooks(): Promise<Book[]> {
  const books = await getCollection('books', ({ data }) => data.status === 'read');
  return books.sort((a, b) => (b.data.year ?? 0) - (a.data.year ?? 0));
}

/** Те, що читаю зараз */
export async function getReadingNow(): Promise<Book[]> {
  return getCollection('books', ({ data }) => data.status === 'reading');
}

/** Теги з постів і TIL, відсортовані за частотою */
export async function getTagCounts(): Promise<{ tag: string; count: number }[]> {
  const [posts, tils] = await Promise.all([getPosts(), getTils()]);
  const counts = new Map<string, number>();
  for (const entry of [...posts, ...tils]) {
    for (const tag of entry.data.tags) {
      counts.set(tag, (counts.get(tag) ?? 0) + 1);
    }
  }
  return [...counts.entries()]
    .map(([tag, count]) => ({ tag, count }))
    .sort((a, b) => b.count - a.count || a.tag.localeCompare(b.tag, 'uk'));
}

/** Робить із тега частину URL: «книги та читання» → «книги-та-читання» */
export function tagSlug(tag: string): string {
  return tag
    .toLowerCase()
    .trim()
    .replace(/[\s/]+/g, '-')
    .replace(/[^\p{L}\p{N}-]/gu, '');
}
