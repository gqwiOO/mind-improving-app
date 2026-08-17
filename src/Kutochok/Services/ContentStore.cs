using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Kutochok.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Kutochok.Services;

/// <summary>
/// Читання й запис контенту. Формат навмисно простий і людський: markdown із
/// frontmatter та yaml-списки. Усе відкривається будь-яким редактором, і
/// застосунок не є єдиним способом дістатися власних текстів.
/// </summary>
public sealed class ContentStore
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(NullNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(NullNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    // ------------------------------------------------------------- читання

    public IReadOnlyList<Entry> List(CollectionDef collection)
    {
        var entries = collection.Kind == StorageKind.Markdown
            ? ReadMarkdownFolder(collection)
            : ReadYamlFile(collection);

        return Sort(collection, entries);
    }

    private static List<Entry> ReadMarkdownFolder(CollectionDef collection)
    {
        var dir = ContentPaths.Resolve(collection.Path);
        var entries = new List<Entry>();
        if (!Directory.Exists(dir)) return entries;

        foreach (var file in Directory.EnumerateFiles(dir, "*.md"))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            if (!Slug.IsValidId(id)) continue;

            try
            {
                var (data, body) = Frontmatter.Parse(File.ReadAllText(file));
                entries.Add(new Entry { Id = id, Data = data, Body = body });
            }
            catch (Exception ex)
            {
                // Один зіпсований файл не має ховати решту розділу
                entries.Add(new Entry
                {
                    Id = id,
                    Data = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["title"] = $"⚠ {id} — could not be read",
                    },
                    Body = ex.Message,
                });
            }
        }

        return entries;
    }

    private static List<Entry> ReadYamlFile(CollectionDef collection)
    {
        var file = ContentPaths.Resolve(collection.Path);
        var entries = new List<Entry>();
        if (!File.Exists(file)) return entries;

        var text = File.ReadAllText(file);
        if (string.IsNullOrWhiteSpace(text)) return entries;

        var root = Deserializer.Deserialize<Dictionary<string, object?>>(text);
        if (root is null || !root.TryGetValue(collection.RootKey!, out var raw)) return entries;
        if (raw is not IEnumerable<object?> items) return entries;

        foreach (var item in items)
        {
            if (item is not IDictionary<object, object?> map) continue;

            var data = new Dictionary<string, object?>(StringComparer.Ordinal);
            string? id = null;

            foreach (var (key, value) in map)
            {
                var name = key?.ToString();
                if (name is null) continue;
                if (name == "id") { id = value?.ToString(); continue; }
                data[name] = Normalize(value);
            }

            if (!string.IsNullOrEmpty(id)) entries.Add(new Entry { Id = id, Data = data });
        }

        return entries;
    }

    /// <summary>YamlDotNet віддає вкладені списки як object — зводимо їх до рядків.</summary>
    private static object? Normalize(object? value) => value switch
    {
        null => null,
        IEnumerable<object?> list => list.Select(i => i?.ToString() ?? string.Empty)
                                        .Where(s => s.Length > 0).ToList(),
        _ => value,
    };

    private static List<Entry> Sort(CollectionDef collection, List<Entry> entries)
    {
        if (collection.SortBy is not { } key)
        {
            return entries.OrderBy(e => e.Title, StringComparer.CurrentCulture).ToList();
        }

        // «Читаю зараз» не має року — такі записи тримаємо вгорі
        return entries
            .OrderByDescending(e => SortValue(e, key) ?? double.MaxValue)
            .ThenBy(e => e.Title, StringComparer.CurrentCulture)
            .ToList();
    }

    private static double? SortValue(Entry entry, string key)
    {
        if (entry.GetDate(key) is { } date) return date.DayNumber;
        return entry.GetNumber(key);
    }

    // -------------------------------------------------------------- запис

    /// <summary>
    /// Створює або оновлює запис. Порожній <paramref name="existingId"/> —
    /// створення. Змінений <paramref name="desiredId"/> для markdown-розділу
    /// означає перейменування файлу.
    /// </summary>
    public Entry Save(
        CollectionDef collection,
        IReadOnlyDictionary<string, object?> values,
        string? existingId = null,
        string? desiredId = null)
    {
        var (data, body) = SplitValues(collection, values);
        Validate(collection, data, body);

        var taken = List(collection).Select(e => e.Id)
            .Where(id => !string.Equals(id, existingId, StringComparison.Ordinal));

        var requested = !string.IsNullOrWhiteSpace(desiredId)
            ? desiredId.Trim()
            : existingId ?? Slug.Slugify(data.GetValueOrDefault("title") as string ?? string.Empty);

        if (!string.IsNullOrEmpty(requested) && !Slug.IsValidId(requested))
        {
            throw new InvalidOperationException("File name may contain only letters, digits, hyphen and underscore");
        }

        var id = Slug.MakeUnique(requested, taken);
        var entry = new Entry { Id = id, Data = data, Body = body };

        if (collection.Kind == StorageKind.Markdown)
        {
            WriteMarkdown(collection, entry);
            if (!string.IsNullOrEmpty(existingId) && existingId != id)
            {
                TryDeleteMarkdown(collection, existingId);
            }
        }
        else
        {
            WriteYamlEntry(collection, entry, existingId);
        }

        return entry;
    }

    public void Delete(CollectionDef collection, string id)
    {
        if (collection.Kind == StorageKind.Markdown)
        {
            var file = MarkdownFile(collection, id);
            if (!File.Exists(file)) throw new FileNotFoundException($"Entry «{id}» not found");
            File.Delete(file);
            return;
        }

        var all = ReadYamlFile(collection);
        var remaining = all.Where(e => !string.Equals(e.Id, id, StringComparison.Ordinal)).ToList();
        if (remaining.Count == all.Count) throw new InvalidOperationException($"Entry «{id}» not found");
        WriteYamlList(collection, remaining);
    }

    // ----------------------------------------------------------- внутрішнє

    private static string MarkdownFile(CollectionDef collection, string id)
    {
        if (!Slug.IsValidId(id)) throw new InvalidOperationException($"Invalid identifier: {id}");
        return ContentPaths.Resolve(Path.Combine(collection.Path, id + ".md"));
    }

    private static void TryDeleteMarkdown(CollectionDef collection, string id)
    {
        try { File.Delete(MarkdownFile(collection, id)); }
        catch (IOException) { /* файл могли прибрати ззовні — не привід падати */ }
    }

    private static (Dictionary<string, object?> Data, string? Body) SplitValues(
        CollectionDef collection,
        IReadOnlyDictionary<string, object?> values)
    {
        var data = new Dictionary<string, object?>(StringComparer.Ordinal);
        string? body = null;

        // Порядок полів у файлі повторює порядок у схемі
        foreach (var field in collection.Fields)
        {
            values.TryGetValue(field.Name, out var raw);

            if (field.Widget == Widget.Markdown)
            {
                body = (raw as string ?? string.Empty).Replace("\r\n", "\n");
                continue;
            }

            var value = Coerce(field, raw);
            if (value is not null) data[field.Name] = value;
        }

        return (data, body);
    }

    /// <summary>Приводить значення з форми до типу, який ляже у файл. null — поле пропускаємо.</summary>
    private static object? Coerce(FieldDef field, object? raw)
    {
        if (raw is null) return null;

        switch (field.Widget)
        {
            case Widget.Number:
            {
                var text = raw as string ?? raw.ToString();
                if (string.IsNullOrWhiteSpace(text)) return null;
                if (!double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var number)) return null;
                // Цілі пишемо цілими, щоб у файлі був «2026», а не «2026.0»
                return Math.Abs(number % 1) < double.Epsilon ? (object)(long)number : number;
            }

            case Widget.List:
            {
                var items = raw switch
                {
                    IEnumerable<string> list => list.ToList(),
                    string text => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                    _ => [],
                };
                items = items.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
                return items.Count > 0 ? items : null;
            }

            default:
            {
                var text = (raw as string ?? raw.ToString() ?? string.Empty).Trim();
                return text.Length > 0 ? text : null;
            }
        }
    }

    private static void Validate(CollectionDef collection, Dictionary<string, object?> data, string? body)
    {
        foreach (var field in collection.Fields)
        {
            if (!field.Required) continue;

            var filled = field.Widget == Widget.Markdown
                ? !string.IsNullOrWhiteSpace(body)
                : data.ContainsKey(field.Name);

            if (!filled) throw new InvalidOperationException($"«{field.Label}» is required");
        }
    }

    private static void WriteMarkdown(CollectionDef collection, Entry entry)
    {
        var file = MarkdownFile(collection, entry.Id);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, Frontmatter.Compose(entry.Data, entry.Body ?? string.Empty, Serializer));
    }

    private static void WriteYamlEntry(CollectionDef collection, Entry entry, string? existingId)
    {
        var all = ReadYamlFile(collection).ToList();
        var index = existingId is null
            ? -1
            : all.FindIndex(e => string.Equals(e.Id, existingId, StringComparison.Ordinal));

        if (index >= 0) all[index] = entry;
        else all.Add(entry);

        WriteYamlList(collection, all);
    }

    private static void WriteYamlList(CollectionDef collection, IReadOnlyList<Entry> entries)
    {
        var items = entries.Select(entry =>
        {
            var map = new Dictionary<string, object?>(StringComparer.Ordinal) { ["id"] = entry.Id };
            foreach (var field in collection.Fields)
            {
                if (entry.Data.TryGetValue(field.Name, out var value) && value is not null)
                {
                    map[field.Name] = value;
                }
            }
            return map;
        }).ToList();

        var root = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [collection.RootKey!] = items,
        };

        var file = ContentPaths.Resolve(collection.Path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, Serializer.Serialize(root));
    }
}
