using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Kutochok.Models;

/// <summary>
/// Один запис будь-якого розділу. Поля зберігаємо словником, а не класом на
/// кожен тип: так форма будується зі схеми, і новий розділ не потребує
/// нового коду — лише опису в <see cref="Schema"/>.
/// </summary>
public sealed class Entry
{
    public required string Id { get; set; }

    public Dictionary<string, object?> Data { get; init; } = new(StringComparer.Ordinal);

    /// <summary>Тіло markdown-запису. У yaml-розділах — null.</summary>
    public string? Body { get; set; }

    public string Title =>
        Data.TryGetValue("title", out var value) && value is string s && s.Length > 0 ? s : Id;

    public string? GetString(string key) =>
        Data.TryGetValue(key, out var value) ? value as string : null;

    public double? GetNumber(string key)
    {
        if (!Data.TryGetValue(key, out var value) || value is null) return null;

        return value switch
        {
            double d => d,
            int i => i,
            long l => l,
            string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) => p,
            _ => null,
        };
    }

    public DateOnly? GetDate(string key)
    {
        var raw = GetString(key);
        return DateOnly.TryParse(raw, CultureInfo.InvariantCulture, out var date) ? date : null;
    }

    public IReadOnlyList<string> GetList(string key)
    {
        if (!Data.TryGetValue(key, out var value) || value is null) return [];

        return value switch
        {
            IReadOnlyList<string> list => list,
            IEnumerable<object?> items => items.Select(i => i?.ToString() ?? string.Empty)
                                               .Where(s => s.Length > 0).ToList(),
            string s => s.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            _ => [],
        };
    }

    public IReadOnlyList<string> Tags => GetList("tags");

    /// <summary>Текст, за яким шукаємо: заголовок, тіло, теги й решта рядків.</summary>
    public string SearchHaystack()
    {
        var parts = new List<string> { Title, Body ?? string.Empty };
        foreach (var (key, value) in Data)
        {
            if (key == "title") continue;
            if (value is string s) parts.Add(s);
            else if (value is not null and not bool) parts.Add(string.Join(' ', GetList(key)));
        }
        return string.Join(' ', parts).ToLower(CultureInfo.CurrentCulture);
    }
}
