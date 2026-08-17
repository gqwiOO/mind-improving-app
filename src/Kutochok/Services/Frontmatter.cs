using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using YamlDotNet.Serialization;

namespace Kutochok.Services;

/// <summary>
/// Markdown-файл із службовою «шапкою»:
/// <code>
/// ---
/// title: Заголовок
/// date: 2026-08-20
/// ---
///
/// Текст.
/// </code>
/// </summary>
public static class Frontmatter
{
    private const string Fence = "---";

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    public static (Dictionary<string, object?> Data, string Body) Parse(string text)
    {
        var data = new Dictionary<string, object?>(StringComparer.Ordinal);
        var normalized = text.Replace("\r\n", "\n");

        if (!normalized.StartsWith(Fence + "\n", StringComparison.Ordinal))
        {
            return (data, normalized.Trim());
        }

        var end = normalized.IndexOf("\n" + Fence, Fence.Length, StringComparison.Ordinal);
        if (end < 0) return (data, normalized.Trim());

        var yaml = normalized[(Fence.Length + 1)..end];
        var rest = normalized[(end + Fence.Length + 1)..];

        var parsed = Deserializer.Deserialize<Dictionary<string, object?>>(yaml);
        if (parsed is not null)
        {
            foreach (var (key, value) in parsed)
            {
                // Рядок теж є IEnumerable, тому спершу відсіюємо його
                data[key] = value is not string && value is IEnumerable<object?> list
                    ? list.Select(i => i?.ToString() ?? string.Empty).Where(s => s.Length > 0).ToList()
                    : value;
            }
        }

        return (data, rest.Trim());
    }

    public static string Compose(
        IReadOnlyDictionary<string, object?> data,
        string body,
        ISerializer serializer)
    {
        var builder = new StringBuilder();
        builder.Append(Fence).Append('\n');

        if (data.Count > 0)
        {
            builder.Append(serializer.Serialize(data).Replace("\r\n", "\n").TrimEnd('\n')).Append('\n');
        }

        builder.Append(Fence).Append("\n\n");
        builder.Append(body.Replace("\r\n", "\n").Trim()).Append('\n');
        return builder.ToString();
    }
}
