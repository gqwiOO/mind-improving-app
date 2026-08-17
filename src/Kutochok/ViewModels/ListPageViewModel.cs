using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Kutochok.Models;

namespace Kutochok.ViewModels;

/// <summary>Один рядок у списку.</summary>
public sealed class EntryRowViewModel
{
    public required Entry Entry { get; init; }
    public required string Title { get; init; }
    public required string Meta { get; init; }
    public string? Badge { get; init; }
    public string? Preview { get; init; }

    // Явні прапорці замість перетворень у розмітці — так видимість передбачувана
    public bool HasMeta => Meta.Length > 0;
    public bool HasBadge => Badge is { Length: > 0 };
    public bool HasPreview => Preview is { Length: > 0 };
}

/// <summary>Тег у панелі фільтра.</summary>
public partial class TagChipViewModel : ViewModelBase
{
    public required string Name { get; init; }

    [ObservableProperty] private bool _isActive;
}

/// <summary>Стовпчик графіка «книг за рік».</summary>
public sealed class YearBarViewModel
{
    public required string Year { get; init; }
    public required int Count { get; init; }
    /// <summary>Частка від найбільшого року, 0..1 — ширину рахує вже вигляд.</summary>
    public required double Fraction { get; init; }
}

public partial class ListPageViewModel : ViewModelBase
{
    private static readonly string[] MonthsShort =
        ["січ", "лют", "бер", "кві", "тра", "чер", "лип", "сер", "вер", "жов", "лис", "гру"];

    private readonly MainViewModel _main;
    private IReadOnlyList<Entry> _all = [];

    public CollectionDef Collection { get; }

    public ObservableCollection<EntryRowViewModel> Rows { get; } = [];
    public ObservableCollection<TagChipViewModel> Tags { get; } = [];
    public ObservableCollection<YearBarViewModel> YearBars { get; } = [];

    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private string? _activeTag;
    [ObservableProperty] private string _emptyMessage = string.Empty;
    [ObservableProperty] private bool _isEmpty;

    // Статистика читання — показуємо лише в розділі книг
    [ObservableProperty] private bool _showStats;
    [ObservableProperty] private string _statTotal = "0";
    [ObservableProperty] private string _statThisYear = "0";
    [ObservableProperty] private string _statPerYear = "0";
    [ObservableProperty] private string _statRating = "—";

    public string Title => Collection.Label;
    public string? Hint => Collection.Hint;
    public bool HasHint => Collection.Hint is { Length: > 0 };
    public string AddLabel => $"Додати {Collection.Singular}";

    [ObservableProperty] private bool _hasTagFilter;

    public ListPageViewModel(MainViewModel main, CollectionDef collection)
    {
        _main = main;
        Collection = collection;
        Reload();
    }

    partial void OnSearchChanged(string value) => ApplyFilter();

    public void Reload()
    {
        _all = _main.Store.List(Collection);

        Tags.Clear();
        if (Collection.HasTags)
        {
            foreach (var tag in _all.SelectMany(e => e.Tags)
                         .Distinct(StringComparer.CurrentCultureIgnoreCase)
                         .OrderBy(t => t, StringComparer.CurrentCulture))
            {
                Tags.Add(new TagChipViewModel { Name = tag });
            }
        }

        HasTagFilter = Tags.Count > 0;
        BuildStats();
        ApplyFilter();
    }

    public void ToggleTag(string tag)
    {
        ActiveTag = string.Equals(ActiveTag, tag, StringComparison.CurrentCultureIgnoreCase) ? null : tag;

        foreach (var chip in Tags)
        {
            chip.IsActive = string.Equals(chip.Name, ActiveTag, StringComparison.CurrentCultureIgnoreCase);
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var needle = Search.Trim().ToLower(CultureInfo.CurrentCulture);

        var filtered = _all.Where(entry =>
        {
            if (ActiveTag is not null &&
                !entry.Tags.Contains(ActiveTag, StringComparer.CurrentCultureIgnoreCase))
            {
                return false;
            }

            return needle.Length == 0 || entry.SearchHaystack().Contains(needle, StringComparison.Ordinal);
        });

        Rows.Clear();
        foreach (var entry in filtered) Rows.Add(BuildRow(entry));

        IsEmpty = Rows.Count == 0;
        EmptyMessage = _all.Count == 0
            ? $"Поки порожньо. Натисни «{AddLabel}»."
            : "Нічого не знайшлося. Спробуй інше слово або зніми фільтр.";
    }

    private EntryRowViewModel BuildRow(Entry entry)
    {
        var meta = new List<string>();

        if (entry.GetDate("date") is { } date)
        {
            meta.Add($"{date.Day} {MonthsShort[date.Month - 1]} {date.Year}");
        }
        else if (entry.GetNumber("year") is { } year)
        {
            meta.Add(((int)year).ToString(CultureInfo.InvariantCulture));
        }

        if (entry.GetString("group") is { } group) meta.Add(group);
        if (entry.GetNumber("rating") is { } rating) meta.Add(FormatRating(rating));
        if (entry.Tags.Count > 0) meta.Add("#" + string.Join("  #", entry.Tags));

        string? badge = entry.GetString("status") switch
        {
            "reading" => "читаю",
            "active" => "в роботі",
            "paused" => "на паузі",
            _ => null,
        };

        var preview = entry.Body is { Length: > 0 } body
            ? Shorten(body)
            : entry.GetString("note") ?? entry.GetString("description");

        return new EntryRowViewModel
        {
            Entry = entry,
            Title = entry.Title,
            Meta = string.Join("  ·  ", meta),
            Badge = badge,
            Preview = preview,
        };
    }

    private static string FormatRating(double rating)
    {
        var full = (int)Math.Floor(rating);
        var half = rating - full >= 0.25 && rating - full < 0.75;
        if (rating - full >= 0.75) full++;
        return new string('★', Math.Clamp(full, 0, 5)) + (half ? "½" : string.Empty);
    }

    private static string Shorten(string text)
    {
        var plain = text.Replace('\n', ' ').Replace("#", string.Empty).Replace("*", string.Empty).Trim();
        while (plain.Contains("  ", StringComparison.Ordinal)) plain = plain.Replace("  ", " ", StringComparison.Ordinal);
        return plain.Length <= 140 ? plain : plain[..plain.LastIndexOf(' ', 140)] + "…";
    }

    // ------------------------------------------------------- статистика

    private void BuildStats()
    {
        YearBars.Clear();
        ShowStats = Collection.Name == "books";
        if (!ShowStats) return;

        var read = _all.Where(e => e.GetString("status") != "reading").ToList();
        var byYear = read
            .Where(e => e.GetNumber("year") is not null)
            .GroupBy(e => (int)e.GetNumber("year")!.Value)
            .OrderByDescending(g => g.Key)
            .ToList();

        var maxPerYear = byYear.Count > 0 ? byYear.Max(g => g.Count()) : 1;
        foreach (var group in byYear)
        {
            YearBars.Add(new YearBarViewModel
            {
                Year = group.Key.ToString(CultureInfo.InvariantCulture),
                Count = group.Count(),
                Fraction = (double)group.Count() / maxPerYear,
            });
        }

        var rated = read.Where(e => e.GetNumber("rating") is not null).ToList();
        var thisYear = DateTime.Now.Year;

        StatTotal = read.Count.ToString(CultureInfo.InvariantCulture);
        StatThisYear = read.Count(e => (int?)e.GetNumber("year") == thisYear).ToString(CultureInfo.InvariantCulture);
        StatPerYear = byYear.Count > 0
            ? ((double)read.Count / byYear.Count).ToString("0.0", CultureInfo.InvariantCulture)
            : "0";
        StatRating = rated.Count > 0
            ? rated.Average(e => e.GetNumber("rating")!.Value).ToString("0.00", CultureInfo.InvariantCulture)
            : "—";
    }

    // ----------------------------------------------------------- дії

    public void Add() => _main.ShowEditor(Collection, null);

    public void Open(Entry entry) => _main.ShowEditor(Collection, entry);
}
