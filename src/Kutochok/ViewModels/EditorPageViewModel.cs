using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Kutochok.Models;
using Kutochok.Services;

namespace Kutochok.ViewModels;

public partial class EditorPageViewModel : ViewModelBase
{
    private readonly MainViewModel _main;

    public CollectionDef Collection { get; }

    /// <summary>Значення полів. Форму будує вигляд, спираючись на схему.</summary>
    public Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal);

    public bool IsNew { get; }

    [ObservableProperty] private string _entryId = string.Empty;
    [ObservableProperty] private string _slugInput = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _statusIsError;
    [ObservableProperty] private bool _isDirty;

    private readonly string _openedTitle;
    private readonly string _openedId;

    /// <summary>Заголовок сторінки — назва запису, а не ім’я файлу.</summary>
    public string Title => IsNew ? $"Додати {Collection.Singular}" : _openedTitle;

    /// <summary>Шлях до файлу, який редагуємо — щоб було видно, де це лежить.</summary>
    public string FileHint => Collection.Kind == StorageKind.Markdown
        ? System.IO.Path.Combine(Collection.Path, (EntryId.Length > 0 ? EntryId : "…") + ".md")
        : Collection.Path;

    public bool ShowSlug => Collection.Kind == StorageKind.Markdown;

    public bool CanDelete => !IsNew;

    public EditorPageViewModel(
        MainViewModel main,
        CollectionDef collection,
        Entry? entry,
        string initialStatus = "")
    {
        _main = main;
        Collection = collection;
        IsNew = entry is null;
        _openedTitle = entry?.Title ?? string.Empty;
        _openedId = entry?.Id ?? string.Empty;
        Status = initialStatus;

        if (entry is not null)
        {
            EntryId = entry.Id;
            SlugInput = entry.Id;

            foreach (var field in collection.Fields)
            {
                Values[field.Name] = field.Widget == Widget.Markdown
                    ? entry.Body ?? string.Empty
                    : ReadValue(entry, field);
            }
        }
        else
        {
            foreach (var field in collection.Fields)
            {
                Values[field.Name] = field switch
                {
                    { Widget: Widget.Date, Required: true } => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    { Widget: Widget.Select, Options.Count: > 0 } => field.Options[0].Value,
                    { Widget: Widget.Markdown } => string.Empty,
                    _ => null,
                };
            }
        }
    }

    private static object? ReadValue(Entry entry, FieldDef field) => field.Widget switch
    {
        Widget.List => string.Join(", ", entry.GetList(field.Name)),
        Widget.Number => entry.GetNumber(field.Name) is { } n
            ? (Math.Abs(n % 1) < double.Epsilon
                ? ((long)n).ToString(CultureInfo.InvariantCulture)
                : n.ToString(CultureInfo.InvariantCulture))
            : null,
        _ => entry.Data.GetValueOrDefault(field.Name),
    };

    public void MarkDirty()
    {
        IsDirty = true;
        Status = string.Empty;
        StatusIsError = false;
    }

    /// <returns>true, якщо збереглося</returns>
    public bool Save()
    {
        try
        {
            var saved = _main.Store.Save(
                Collection,
                Values,
                existingId: IsNew ? null : EntryId,
                desiredId: ShowSlug ? SlugInput : null);

            EntryId = saved.Id;
            SlugInput = saved.Id;
            IsDirty = false;
            StatusIsError = false;
            Status = "Збережено";
            _main.RefreshCounts();

            // Щойно створений запис перестає бути новим: перевідкриваємо сторінку,
            // щоб з'явилися правильний заголовок, шлях до файлу й кнопка видалення
            if (IsNew || saved.Id != _openedId)
            {
                _main.ShowEditor(Collection, saved, "Збережено");
            }

            return true;
        }
        catch (Exception ex)
        {
            StatusIsError = true;
            Status = ex.Message;
            return false;
        }
    }

    public void Delete()
    {
        try
        {
            _main.Store.Delete(Collection, EntryId);
            IsDirty = false;
            Back();
        }
        catch (Exception ex)
        {
            StatusIsError = true;
            Status = ex.Message;
        }
    }

    public void Back() => _main.ShowList(Collection);

    public void SaveAndBack()
    {
        if (Save()) Back();
    }

    /// <summary>Готовий markdown для передперегляду.</summary>
    public string BodyText =>
        Collection.Body is { } field && Values.TryGetValue(field.Name, out var value)
            ? value as string ?? string.Empty
            : string.Empty;
}
