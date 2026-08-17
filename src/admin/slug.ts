/**
 * Транслітерація українською за постановою КМУ №55 (2010) — та сама, що в
 * закордонних паспортах. «Привіт, світе» → «pryvit-svite».
 */

/** Літери, що звучать по-різному на початку слова і всередині */
const POSITIONAL: Record<string, [start: string, rest: string]> = {
  є: ['ye', 'ie'],
  ї: ['yi', 'i'],
  й: ['y', 'i'],
  ю: ['yu', 'iu'],
  я: ['ya', 'ia'],
};

const SIMPLE: Record<string, string> = {
  а: 'a',
  б: 'b',
  в: 'v',
  г: 'h',
  ґ: 'g',
  д: 'd',
  е: 'e',
  ж: 'zh',
  з: 'z',
  и: 'y',
  і: 'i',
  к: 'k',
  л: 'l',
  м: 'm',
  н: 'n',
  о: 'o',
  п: 'p',
  р: 'r',
  с: 's',
  т: 't',
  у: 'u',
  ф: 'f',
  х: 'kh',
  ц: 'ts',
  ч: 'ch',
  ш: 'sh',
  щ: 'shch',
  ь: '',
  '’': '',
  "'": '',
  ʼ: '',
  // трапляється в запозиченнях
  ы: 'y',
  э: 'e',
  ё: 'e',
  ъ: '',
};

const isLetter = (char: string) => /\p{L}/u.test(char);

export function transliterate(input: string): string {
  const lower = input.toLowerCase();
  let out = '';

  for (let i = 0; i < lower.length; i++) {
    const char = lower[i]!;
    const prev = i > 0 ? lower[i - 1]! : '';
    const atWordStart = i === 0 || !isLetter(prev);

    // «зг» передається як «zgh», щоб відрізнити від «ж» (zh)
    if (char === 'г' && prev === 'з') {
      out += 'gh';
      continue;
    }

    const positional = POSITIONAL[char];
    if (positional) {
      out += atWordStart ? positional[0] : positional[1];
      continue;
    }

    const simple = SIMPLE[char];
    out += simple === undefined ? char : simple;
  }

  return out;
}

/** Робить із заголовка безпечне ім’я файлу / ідентифікатор */
export function slugify(input: string): string {
  return transliterate(input)
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 80);
}

/**
 * Перевірка ідентифікатора (він же ім’я файлу, він же шматок URL).
 *
 * Літери приймаємо будь-які, не лише латинські: зовнішня CMS може створити
 * файл із кириличною назвою, і адмінка має вміти його відкрити. Заборонені
 * крапки та слеші — саме вони дали б вихід за межі теки.
 */
export function isValidId(id: unknown): id is string {
  return typeof id === 'string' && /^[\p{L}\p{N}][\p{L}\p{N}_-]*$/u.test(id) && id.length <= 100;
}

/** Додає -2, -3 … якщо такий ідентифікатор уже зайнятий */
export function uniqueId(base: string, taken: Iterable<string>): string {
  const used = new Set(taken);
  const seed = base || 'bez-nazvy';
  if (!used.has(seed)) return seed;
  for (let n = 2; ; n++) {
    const candidate = `${seed}-${n}`;
    if (!used.has(candidate)) return candidate;
  }
}
