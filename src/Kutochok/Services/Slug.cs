using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Kutochok.Services;

/// <summary>
/// Транслітерація українською за постановою КМУ №55 (2010) — та сама, що в
/// закордонних паспортах. «Привіт, світе» → «pryvit-svite».
/// </summary>
public static class Slug
{
    /// <summary>Літери, що звучать по-різному на початку слова і всередині.</summary>
    private static readonly Dictionary<char, (string Start, string Inside)> Positional = new()
    {
        ['є'] = ("ye", "ie"),
        ['ї'] = ("yi", "i"),
        ['й'] = ("y", "i"),
        ['ю'] = ("yu", "iu"),
        ['я'] = ("ya", "ia"),
    };

    private static readonly Dictionary<char, string> Simple = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "h", ['ґ'] = "g",
        ['д'] = "d", ['е'] = "e", ['ж'] = "zh", ['з'] = "z", ['и'] = "y",
        ['і'] = "i", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n",
        ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t",
        ['у'] = "u", ['ф'] = "f", ['х'] = "kh", ['ц'] = "ts", ['ч'] = "ch",
        ['ш'] = "sh", ['щ'] = "shch", ['ь'] = "", ['\''] = "", ['’'] = "", ['ʼ'] = "",
        // трапляється в запозиченнях
        ['ы'] = "y", ['э'] = "e", ['ё'] = "e", ['ъ'] = "",
    };

    private static bool IsApostrophe(char ch) => ch is '\'' or '’' or 'ʼ' or '`';

    public static string Transliterate(string input)
    {
        var lower = input.ToLower(CultureInfo.InvariantCulture);
        var result = new StringBuilder(lower.Length);

        // Апостроф не відтворюється, але й не розриває слово: у «Мар’яна»
        // літера «я» стоїть усередині слова, тому дає «ia», а не «ya».
        var insideWord = false;

        for (var i = 0; i < lower.Length; i++)
        {
            var ch = lower[i];
            var prev = i > 0 ? lower[i - 1] : '\0';

            if (IsApostrophe(ch))
            {
                continue;
            }

            if (!char.IsLetter(ch))
            {
                insideWord = false;
                result.Append(ch);
                continue;
            }

            // «зг» передається як «zgh», щоб відрізнити від «ж» (zh)
            if (ch == 'г' && prev == 'з')
            {
                result.Append("gh");
                insideWord = true;
                continue;
            }

            if (Positional.TryGetValue(ch, out var pair))
            {
                result.Append(insideWord ? pair.Inside : pair.Start);
            }
            else
            {
                result.Append(Simple.TryGetValue(ch, out var mapped) ? mapped : ch.ToString());
            }

            insideWord = true;
        }

        return result.ToString();
    }

    /// <summary>Робить із заголовка безпечне ім’я файлу.</summary>
    public static string Slugify(string input)
    {
        var latin = Transliterate(input);
        var builder = new StringBuilder(latin.Length);
        var pendingDash = false;

        foreach (var ch in latin)
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
            {
                if (pendingDash && builder.Length > 0) builder.Append('-');
                pendingDash = false;
                builder.Append(ch);
            }
            else
            {
                pendingDash = true;
            }
        }

        var slug = builder.ToString();
        return slug.Length > 80 ? slug[..80].TrimEnd('-') : slug;
    }

    /// <summary>
    /// Ідентифікатор — він же ім’я файлу. Літери приймаємо будь-які, але
    /// крапки й слеші забороняємо: саме вони дали б вихід за межі теки.
    /// </summary>
    public static bool IsValidId(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 100) return false;
        if (!char.IsLetterOrDigit(id[0])) return false;

        foreach (var ch in id)
        {
            if (!char.IsLetterOrDigit(ch) && ch != '-' && ch != '_') return false;
        }

        return true;
    }

    /// <summary>Додає -2, -3 … якщо такий ідентифікатор уже зайнятий.</summary>
    public static string MakeUnique(string seed, IEnumerable<string> taken)
    {
        var used = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
        var baseId = string.IsNullOrEmpty(seed) ? "bez-nazvy" : seed;

        if (!used.Contains(baseId)) return baseId;

        for (var n = 2; ; n++)
        {
            var candidate = $"{baseId}-{n}";
            if (!used.Contains(candidate)) return candidate;
        }
    }
}
