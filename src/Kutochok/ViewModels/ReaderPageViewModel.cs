using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Kutochok.Models;

namespace Kutochok.ViewModels;

/// <summary>Пара «підпис — значення» під заголовком у режимі читання.</summary>
public sealed class DetailViewModel
{
    public required string Label { get; init; }
    public required string Value { get; init; }
    /// <summary>Непорожнє — значення показуємо як клікабельне посилання.</summary>
    public string? Url { get; init; }
    public bool IsLink => Url is { Length: > 0 };
}

/// <summary>
/// Читання запису на все вікно. Редагування — окремою кнопкою, щоб текст
/// не був постійно в полі вводу й читався як текст.
/// </summary>
public partial class ReaderPageViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    public CollectionDef Collection { get; }
    public Entry Entry { get; }

    public string Title => Entry.Title;

    /// <summary>Рядок під заголовком: дата, теги, оцінка, статус.</summary>
    public string Meta { get; }

    public bool HasMeta => Meta.Length > 0;

    public List<DetailViewModel> Details { get; } = [];

    public bool HasDetails => Details.Count > 0;

    /// <summary>Markdown, який малює вигляд. Для yaml-розділів — нотатка чи опис.</summary>
    public string BodyMarkdown { get; }

    public ReaderPageViewModel(MainViewModel main, CollectionDef collection, Entry entry)
    {
        _main = main;
        Collection = collection;
        Entry = entry;

        Meta = BuildMeta(entry);
        BodyMarkdown = entry.Body is { Length: > 0 } body
            ? body
            : entry.GetString("note") ?? entry.GetString("description") ?? string.Empty;

        BuildDetails();
    }

    private static string BuildMeta(Entry entry)
    {
        var parts = new List<string>();

        if (entry.GetDate("date") is { } date)
        {
            parts.Add(date.ToString("d MMMM yyyy", CultureInfo.CurrentCulture));
        }
        else if (entry.GetNumber("year") is { } year)
        {
            parts.Add(((int)year).ToString(CultureInfo.InvariantCulture));
        }

        if (entry.GetString("status") is { Length: > 0 } status)
        {
            parts.Add(status == "reading" ? "reading now" : status);
        }

        if (entry.GetNumber("rating") is { } rating)
        {
            parts.Add($"{rating.ToString("0.#", CultureInfo.InvariantCulture)}/5");
        }

        if (entry.Tags.Count > 0) parts.Add("#" + string.Join("  #", entry.Tags));

        return string.Join("  ·  ", parts);
    }

    /// <summary>
    /// Показуємо ті поля, яких немає ні в заголовку, ні в мета-рядку, ні в тілі —
    /// щоб у режимі читання нічого не губилося.
    /// </summary>
    private void BuildDetails()
    {
        var shownInMeta = new HashSet<string>(StringComparer.Ordinal)
        {
            "title", "date", "year", "status", "rating", "tags",
        };

        var bodySource = Entry.Body is { Length: > 0 } ? null : FindBodySourceField();

        foreach (var field in Collection.Fields)
        {
            if (field.Widget == Widget.Markdown) continue;
            if (shownInMeta.Contains(field.Name)) continue;
            if (field.Name == bodySource) continue;

            if (field.Widget == Widget.List)
            {
                var items = Entry.GetList(field.Name);
                if (items.Count > 0)
                {
                    Details.Add(new DetailViewModel { Label = field.Label, Value = string.Join(", ", items) });
                }
                continue;
            }

            if (Entry.GetString(field.Name) is not { Length: > 0 } value) continue;

            Details.Add(new DetailViewModel
            {
                Label = field.Label,
                Value = value,
                Url = field.Widget == Widget.Url ? value : null,
            });
        }
    }

    /// <summary>Яке поле пішло в «тіло» для розділів без markdown.</summary>
    private string? FindBodySourceField()
    {
        if (Entry.GetString("note") is { Length: > 0 }) return "note";
        if (Entry.GetString("description") is { Length: > 0 }) return "description";
        return null;
    }

    public void Edit() => _main.ShowEditor(Collection, Entry);

    public void Back() => _main.ShowList(Collection);

    public string? Delete()
    {
        try
        {
            _main.Store.Delete(Collection, Entry.Id);
            _main.RefreshCounts();
            Back();
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
