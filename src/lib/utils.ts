const MONTHS_SHORT = [
  'січ',
  'лют',
  'бер',
  'кві',
  'тра',
  'чер',
  'лип',
  'сер',
  'вер',
  'жов',
  'лис',
  'гру',
];

const MONTHS_FULL = [
  'січня',
  'лютого',
  'березня',
  'квітня',
  'травня',
  'червня',
  'липня',
  'серпня',
  'вересня',
  'жовтня',
  'листопада',
  'грудня',
];

/** «14 сер» — для списків */
export function formatShort(date: Date): string {
  return `${date.getUTCDate()} ${MONTHS_SHORT[date.getUTCMonth()]}`;
}

/** «14 серпня 2026» — для сторінки посту */
export function formatFull(date: Date): string {
  return `${date.getUTCDate()} ${MONTHS_FULL[date.getUTCMonth()]} ${date.getUTCFullYear()}`;
}

/** ISO-дата для атрибута datetime */
export function isoDate(date: Date): string {
  return date.toISOString().slice(0, 10);
}

/**
 * Групує записи по роках і повертає роки за спаданням.
 */
export function groupByYear<T>(
  items: T[],
  getYear: (item: T) => number,
): { year: number; items: T[] }[] {
  const buckets = new Map<number, T[]>();
  for (const item of items) {
    const year = getYear(item);
    const bucket = buckets.get(year);
    if (bucket) bucket.push(item);
    else buckets.set(year, [item]);
  }
  return [...buckets.entries()]
    .sort((a, b) => b[0] - a[0])
    .map(([year, items]) => ({ year, items }));
}

/** 4.5 → «★★★★½» */
export function stars(rating: number): string {
  const full = Math.floor(rating);
  const half = rating - full >= 0.25 && rating - full < 0.75;
  const bonus = rating - full >= 0.75 ? 1 : 0;
  return '★'.repeat(full + bonus) + (half ? '½' : '');
}

/**
 * Українське відмінювання після числівника:
 * 1 пост, 2 пости, 5 постів — і 11–14 завжди як «постів».
 */
export function plural(count: number, one: string, few: string, many: string): string {
  const tail = Math.abs(count) % 10;
  const teen = Math.abs(count) % 100 >= 11 && Math.abs(count) % 100 <= 14;
  if (teen || tail === 0 || tail >= 5) return many;
  return tail === 1 ? one : few;
}

/** «3 пости» — число разом із правильною формою слова */
export function counted(count: number, one: string, few: string, many: string): string {
  return `${count} ${plural(count, one, few, many)}`;
}

/** Приблизний час читання українською */
export function readingTime(body: string | undefined): string {
  const words = (body ?? '').trim().split(/\s+/).filter(Boolean).length;
  const minutes = Math.max(1, Math.round(words / 180));
  return `${counted(minutes, 'хвилина', 'хвилини', 'хвилин')} читання`;
}

/** Перше речення / обрізаний текст — для пошуку і RSS */
export function excerpt(body: string | undefined, limit = 180): string {
  const plain = (body ?? '')
    .replace(/```[\s\S]*?```/g, ' ')
    .replace(/!?\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/[#>*_`~-]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
  if (plain.length <= limit) return plain;
  return plain.slice(0, plain.lastIndexOf(' ', limit)) + '…';
}
