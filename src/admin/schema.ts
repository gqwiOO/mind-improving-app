/**
 * Опис колекцій для адмінки.
 *
 * Це «людське» дзеркало src/content.config.ts: там описано, що дозволено
 * в даних, а тут — як воно має виглядати у формі. Якщо додаєш поле в
 * content.config.ts і хочеш редагувати його через /admin — додай і сюди.
 */

export type Widget =
  | 'text'
  | 'textarea'
  | 'markdown'
  | 'date'
  | 'number'
  | 'boolean'
  | 'list'
  | 'select'
  | 'url';

export interface Field {
  name: string;
  label: string;
  widget: Widget;
  required?: boolean;
  /** Тільки для widget: 'select' */
  options?: { value: string; label: string }[];
  /** Підказка під полем */
  help?: string;
  /** Тільки для widget: 'number' */
  min?: number;
  max?: number;
  step?: number;
}

export interface Collection {
  name: string;
  label: string;
  /** Однина — для кнопки «Новий …» */
  singular: string;
  /**
   * markdown — один файл на запис, тека в `path`
   * yaml     — один спільний файл-масив, шлях у `path`
   */
  kind: 'markdown' | 'yaml';
  path: string;
  /** Тільки для kind: 'yaml' — ключ, під яким у файлі лежить список записів */
  rootKey?: string;
  fields: Field[];
  /** Поле, за яким сортувати список (спадання) */
  sortBy?: string;
  /** Чи можна створювати й видаляти записи */
  canCreate: boolean;
  canDelete: boolean;
  /** Підпис угорі списку */
  hint?: string;
}

const TAGS: Field = {
  name: 'tags',
  label: 'Теги',
  widget: 'list',
  help: 'Через кому. Сторінки тегів створюються самі.',
};

export const COLLECTIONS: Collection[] = [
  {
    name: 'posts',
    label: 'Пости',
    singular: 'пост',
    kind: 'markdown',
    path: 'src/content/posts',
    sortBy: 'date',
    canCreate: true,
    canDelete: true,
    hint: 'Ім’я файлу стає адресою: nazva.md → /writing/nazva',
    fields: [
      { name: 'title', label: 'Заголовок', widget: 'text', required: true },
      { name: 'date', label: 'Дата', widget: 'date', required: true },
      {
        name: 'description',
        label: 'Опис',
        widget: 'textarea',
        help: 'Одне речення для головної, RSS і пошуку. Необов’язково.',
      },
      TAGS,
      {
        name: 'draft',
        label: 'Чернетка',
        widget: 'boolean',
        help: 'Чернетку видно тільки локально, у зібраний сайт вона не потрапляє.',
      },
      { name: 'body', label: 'Текст', widget: 'markdown', required: true },
    ],
  },
  {
    name: 'til',
    label: 'TIL',
    singular: 'замітку',
    kind: 'markdown',
    path: 'src/content/til',
    sortBy: 'date',
    canCreate: true,
    canDelete: true,
    hint: 'Короткі замітки. Показуються повністю прямо в списку на /til',
    fields: [
      { name: 'title', label: 'Заголовок', widget: 'text', required: true },
      { name: 'date', label: 'Дата', widget: 'date', required: true },
      TAGS,
      { name: 'source', label: 'Джерело', widget: 'url', help: 'Посилання, звідки дізнався.' },
      { name: 'body', label: 'Текст', widget: 'markdown', required: true },
    ],
  },
  {
    name: 'pages',
    label: 'Сторінки',
    singular: 'сторінку',
    kind: 'markdown',
    path: 'src/content/pages',
    canCreate: true,
    canDelete: true,
    hint: 'about.md → /about. Щоб сторінка з’явилася в меню, додай її в src/site.ts',
    fields: [
      { name: 'title', label: 'Заголовок', widget: 'text', required: true },
      { name: 'description', label: 'Опис', widget: 'textarea' },
      {
        name: 'updated',
        label: 'Оновлено',
        widget: 'date',
        help: 'Показується під заголовком. Можна лишити порожнім.',
      },
      { name: 'body', label: 'Текст', widget: 'markdown', required: true },
    ],
  },
  {
    name: 'books',
    label: 'Книги',
    singular: 'книжку',
    kind: 'yaml',
    path: 'src/data/books.yaml',
    rootKey: 'books',
    sortBy: 'year',
    canCreate: true,
    canDelete: true,
    hint: 'Статистика й графік на /books рахуються самі',
    fields: [
      { name: 'title', label: 'Назва', widget: 'text', required: true },
      {
        name: 'status',
        label: 'Статус',
        widget: 'select',
        options: [
          { value: 'read', label: 'прочитано' },
          { value: 'reading', label: 'читаю зараз' },
        ],
        help: '«Читаю зараз» показується у блоці на головній.',
      },
      { name: 'year', label: 'Рік', widget: 'number', min: 1900, max: 2200, step: 1 },
      { name: 'rating', label: 'Оцінка', widget: 'number', min: 0, max: 5, step: 0.5 },
      { name: 'note', label: 'Враження', widget: 'textarea' },
      { name: 'url', label: 'Посилання', widget: 'url' },
    ],
  },
  {
    name: 'projects',
    label: 'Проєкти',
    singular: 'проєкт',
    kind: 'yaml',
    path: 'src/data/projects.yaml',
    rootKey: 'projects',
    sortBy: 'year',
    canCreate: true,
    canDelete: true,
    fields: [
      { name: 'title', label: 'Назва', widget: 'text', required: true },
      { name: 'description', label: 'Опис', widget: 'textarea', required: true },
      {
        name: 'status',
        label: 'Статус',
        widget: 'select',
        options: [
          { value: 'active', label: 'в роботі' },
          { value: 'done', label: 'завершено' },
          { value: 'paused', label: 'на паузі' },
        ],
      },
      { name: 'year', label: 'Рік', widget: 'number', min: 1900, max: 2200, step: 1 },
      { name: 'stack', label: 'Стек', widget: 'list', help: 'Через кому: Unity, C#' },
      { name: 'url', label: 'Посилання', widget: 'url' },
      { name: 'repo', label: 'Репозиторій', widget: 'url' },
    ],
  },
  {
    name: 'links',
    label: 'Посилання',
    singular: 'посилання',
    kind: 'yaml',
    path: 'src/data/links.yaml',
    rootKey: 'links',
    canCreate: true,
    canDelete: true,
    fields: [
      { name: 'title', label: 'Назва', widget: 'text', required: true },
      { name: 'url', label: 'Адреса', widget: 'url', required: true },
      { name: 'group', label: 'Група', widget: 'text', help: 'Блоги, Інструменти, …' },
      { name: 'note', label: 'Нотатка', widget: 'textarea' },
    ],
  },
];

export function getCollection(name: string): Collection | undefined {
  return COLLECTIONS.find((collection) => collection.name === name);
}
