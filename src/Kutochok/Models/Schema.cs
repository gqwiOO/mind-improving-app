using System;
using System.Collections.Generic;
using System.Linq;

namespace Kutochok.Models;

public enum Widget
{
    Text,
    TextArea,
    Markdown,
    Date,
    Number,
    List,
    Select,
    Url,
}

/// <summary>Як поле має виглядати у формі.</summary>
public sealed record FieldDef(
    string Name,
    string Label,
    Widget Widget,
    bool Required = false,
    string? Help = null,
    IReadOnlyList<SelectOption>? Options = null,
    double? Min = null,
    double? Max = null,
    double Step = 1);

public sealed record SelectOption(string Value, string Label);

/// <summary>
/// Markdown — один файл на запис у теці <see cref="Path"/>.
/// Yaml — спільний файл-список, ключ <see cref="RootKey"/>.
/// </summary>
public enum StorageKind
{
    Markdown,
    Yaml,
}

public sealed record CollectionDef(
    string Name,
    string Label,
    string Singular,
    StorageKind Kind,
    string Path,
    IReadOnlyList<FieldDef> Fields,
    string? RootKey = null,
    string? SortBy = null,
    string? Hint = null)
{
    public FieldDef? Body => Fields.FirstOrDefault(f => f.Widget == Widget.Markdown);

    public bool HasTags => Fields.Any(f => f.Name == "tags");
}

/// <summary>Опис усіх розділів застосунку.</summary>
public static class Schema
{
    private static readonly FieldDef Tags = new(
        "tags", "Теги", Widget.List,
        Help: "Через кому. За ними працюють фільтр і пошук.");

    public static readonly IReadOnlyList<CollectionDef> Collections =
    [
        new CollectionDef(
            "notes", "Записи", "запис", StorageKind.Markdown, "notes",
            SortBy: "date",
            Hint: "Довгі тексти: думки, чернетки, щоденник",
            Fields:
            [
                new FieldDef("title", "Заголовок", Widget.Text, Required: true),
                new FieldDef("date", "Дата", Widget.Date, Required: true),
                Tags,
                new FieldDef("body", "Текст", Widget.Markdown, Required: true),
            ]),

        new CollectionDef(
            "til", "TIL", "замітку", StorageKind.Markdown, "til",
            SortBy: "date",
            Hint: "Today I learned — дрібниці, які шкода забути",
            Fields:
            [
                new FieldDef("title", "Заголовок", Widget.Text, Required: true),
                new FieldDef("date", "Дата", Widget.Date, Required: true),
                Tags,
                new FieldDef("source", "Джерело", Widget.Url, Help: "Звідки дізнався."),
                new FieldDef("body", "Текст", Widget.Markdown, Required: true),
            ]),

        new CollectionDef(
            "books", "Книги", "книжку", StorageKind.Yaml, "books.yaml",
            RootKey: "books",
            SortBy: "year",
            Hint: "Статистика й графік рахуються самі",
            Fields:
            [
                new FieldDef("title", "Назва", Widget.Text, Required: true),
                new FieldDef("status", "Статус", Widget.Select,
                    Help: "«Читаю зараз» піднімає книжку на початок списку.",
                    Options: [new SelectOption("read", "прочитано"), new SelectOption("reading", "читаю зараз")]),
                new FieldDef("year", "Рік", Widget.Number, Min: 1900, Max: 2200, Step: 1),
                new FieldDef("rating", "Оцінка", Widget.Number, Min: 0, Max: 5, Step: 0.5),
                new FieldDef("note", "Враження", Widget.TextArea),
                new FieldDef("url", "Посилання", Widget.Url),
            ]),

        new CollectionDef(
            "projects", "Проєкти", "проєкт", StorageKind.Yaml, "projects.yaml",
            RootKey: "projects",
            SortBy: "year",
            Fields:
            [
                new FieldDef("title", "Назва", Widget.Text, Required: true),
                new FieldDef("description", "Опис", Widget.TextArea, Required: true),
                new FieldDef("status", "Статус", Widget.Select,
                    Options:
                    [
                        new SelectOption("active", "в роботі"),
                        new SelectOption("done", "завершено"),
                        new SelectOption("paused", "на паузі"),
                    ]),
                new FieldDef("year", "Рік", Widget.Number, Min: 1900, Max: 2200, Step: 1),
                new FieldDef("stack", "Стек", Widget.List, Help: "Через кому: Unity, C#"),
                new FieldDef("url", "Посилання", Widget.Url),
                new FieldDef("repo", "Репозиторій", Widget.Url),
            ]),

        new CollectionDef(
            "links", "Посилання", "посилання", StorageKind.Yaml, "links.yaml",
            RootKey: "links",
            Hint: "Те, що варто зберегти й колись перечитати",
            Fields:
            [
                new FieldDef("title", "Назва", Widget.Text, Required: true),
                new FieldDef("url", "Адреса", Widget.Url, Required: true),
                new FieldDef("group", "Група", Widget.Text, Help: "Блоги, Інструменти, …"),
                new FieldDef("note", "Нотатка", Widget.TextArea),
            ]),
    ];

    public static CollectionDef? Find(string name) =>
        Collections.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal));
}
