/**
 * Головні налаштування сайту. Майже все, що варто змінити «під себе»,
 * лежить тут — решта файлів читає ці значення.
 */
export const site = {
  title: 'Мій куточок',
  /** Одне речення про себе — показується на головній під іменем */
  tagline: 'Пишу про те, що читаю, роблю і думаю.',
  author: 'Твоє Ім’я',
  description: 'Персональний сайт: пости, книги, нотатки.',
  lang: 'uk',
  /** Файл в public/. Прибери рядок, якщо аватар не потрібен */
  avatar: '/avatar.svg',
  email: 'you@example.com',
};

/** Верхня навігація — тримай короткою */
export const nav = [
  { href: '/', label: 'Головна' },
  { href: '/writing', label: 'Блог' },
  { href: '/til', label: 'TIL' },
  { href: '/books', label: 'Книги' },
  { href: '/projects', label: 'Проєкти' },
  { href: '/about', label: 'Про мене' },
];

/** Другорядні сторінки — тільки у футері */
export const footerNav = [
  { href: '/now', label: 'Зараз' },
  { href: '/uses', label: 'Інструменти' },
  { href: '/links', label: 'Посилання' },
  { href: '/tags', label: 'Теги' },
  { href: '/search', label: 'Пошук' },
  { href: '/rss.xml', label: 'RSS' },
];

/** Профілі. Порожній масив — блок просто не з’явиться */
export const socials: { label: string; href: string }[] = [
  // { label: 'GitHub', href: 'https://github.com/username' },
  // { label: 'Telegram', href: 'https://t.me/username' },
];
