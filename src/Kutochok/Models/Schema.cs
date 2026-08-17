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
    string? Hint = null,
    /// <summary>Поле, за яким список ділиться на теки/групи із заголовками.</summary>
    string? GroupBy = null)
{
    public FieldDef? Body => Fields.FirstOrDefault(f => f.Widget == Widget.Markdown);

    public bool HasTags => Fields.Any(f => f.Name == "tags");
}

/// <summary>Опис усіх розділів застосунку.</summary>
public static class Schema
{
    private static readonly FieldDef Tags = new(
        "tags", "Tags", Widget.List,
        Help: "Comma separated. Used by the filter and search.");

    public static readonly IReadOnlyList<CollectionDef> Collections =
    [
        new CollectionDef(
            "notes", "Notes", "note", StorageKind.Markdown, "notes",
            SortBy: "date",
            Hint: "Longer writing: thoughts, drafts, journal",
            Fields:
            [
                new FieldDef("title", "Title", Widget.Text, Required: true),
                new FieldDef("date", "Date", Widget.Date, Required: true),
                Tags,
                new FieldDef("body", "Text", Widget.Markdown, Required: true),
            ]),

        new CollectionDef(
            "til", "TIL", "TIL", StorageKind.Markdown, "til",
            SortBy: "date",
            Hint: "Today I learned — small things worth keeping",
            Fields:
            [
                new FieldDef("title", "Title", Widget.Text, Required: true),
                new FieldDef("date", "Date", Widget.Date, Required: true),
                Tags,
                new FieldDef("source", "Source", Widget.Url, Help: "Where you learned it."),
                new FieldDef("body", "Text", Widget.Markdown, Required: true),
            ]),

        new CollectionDef(
            "books", "Books", "book", StorageKind.Yaml, "books.yaml",
            RootKey: "books",
            SortBy: "year",
            Hint: "Stats and the chart are computed automatically",
            Fields:
            [
                new FieldDef("title", "Title", Widget.Text, Required: true),
                new FieldDef("status", "Status", Widget.Select,
                    Help: "«Reading now» keeps the book at the top of the list.",
                    Options: [new SelectOption("read", "read"), new SelectOption("reading", "reading now")]),
                new FieldDef("year", "Year", Widget.Number, Min: 1900, Max: 2200, Step: 1),
                new FieldDef("rating", "Rating", Widget.Number, Min: 0, Max: 5, Step: 0.5),
                new FieldDef("note", "Thoughts", Widget.TextArea),
                new FieldDef("url", "Link", Widget.Url),
            ]),

        new CollectionDef(
            "projects", "Projects", "project", StorageKind.Yaml, "projects.yaml",
            RootKey: "projects",
            SortBy: "year",
            GroupBy: "folder",
            Hint: "Grouped into folders — type any name you like",
            Fields:
            [
                new FieldDef("title", "Title", Widget.Text, Required: true),
                new FieldDef("folder", "Folder", Widget.Text,
                    Help: "For example: Work, Side projects. Empty means «No folder»."),
                new FieldDef("description", "Description", Widget.TextArea, Required: true),
                new FieldDef("status", "Status", Widget.Select,
                    Options:
                    [
                        new SelectOption("active", "active"),
                        new SelectOption("done", "done"),
                        new SelectOption("paused", "paused"),
                    ]),
                new FieldDef("year", "Year", Widget.Number, Min: 1900, Max: 2200, Step: 1),
                new FieldDef("stack", "Stack", Widget.List, Help: "Comma separated: Unity, C#"),
                new FieldDef("url", "Link", Widget.Url),
                new FieldDef("repo", "Repository", Widget.Url),
            ]),

        new CollectionDef(
            "links", "Links", "link", StorageKind.Yaml, "links.yaml",
            RootKey: "links",
            GroupBy: "group",
            Hint: "Worth keeping and re-reading some day",
            Fields:
            [
                new FieldDef("title", "Title", Widget.Text, Required: true),
                new FieldDef("url", "Address", Widget.Url, Required: true),
                new FieldDef("group", "Group", Widget.Text, Help: "Blogs, Tools, …"),
                new FieldDef("note", "Note", Widget.TextArea),
            ]),
    ];

    public static CollectionDef? Find(string name) =>
        Collections.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal));
}
