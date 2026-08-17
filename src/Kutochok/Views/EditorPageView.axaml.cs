using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Kutochok.Models;
using Kutochok.Services;
using Kutochok.ViewModels;

namespace Kutochok.Views;

/// <summary>
/// Форма будується з опису розділу, а не пишеться руками під кожен тип.
/// Додав поле в <see cref="Schema"/> — воно з'явилося тут саме.
/// </summary>
public partial class EditorPageView : UserControl
{
    private readonly List<(FieldDef Field, Func<object?> Read)> _readers = [];
    private bool _building;

    public EditorPageView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Build();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private EditorPageViewModel? Model => DataContext as EditorPageViewModel;

    // ------------------------------------------------------ побудова форми

    private void Build()
    {
        var host = this.FindControl<StackPanel>("FieldsHost")!;
        host.Children.Clear();
        _readers.Clear();

        if (Model is not { } model) return;

        _building = true;

        foreach (var field in model.Collection.Fields)
        {
            if (field.Widget == Widget.Markdown) continue;
            host.Children.Add(BuildField(field, model));
        }

        // Адреса запису — тільки там, де ім'я файлу щось означає
        if (model.ShowSlug) host.Children.Add(BuildSlugField(model));

        SetupBody(model);

        _building = false;
    }

    private Control BuildField(FieldDef field, EditorPageViewModel model)
    {
        var panel = new StackPanel { Spacing = 3 };
        panel.Children.Add(new TextBlock
        {
            Text = field.Required ? field.Label + " *" : field.Label,
            Classes = { "label" },
        });

        model.Values.TryGetValue(field.Name, out var current);
        Control control;

        switch (field.Widget)
        {
            case Widget.Select:
            {
                var combo = new ComboBox
                {
                    ItemsSource = field.Options!.Select(o => o.Label).ToList(),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                var index = field.Options!.ToList().FindIndex(o => o.Value == current as string);
                combo.SelectedIndex = index >= 0 ? index : 0;
                combo.SelectionChanged += (_, _) => Touch();
                control = combo;
                _readers.Add((field, () => combo.SelectedIndex >= 0
                    ? field.Options![combo.SelectedIndex].Value
                    : null));
                break;
            }

            case Widget.TextArea:
            {
                var box = MakeBox(current as string, multiline: true);
                box.MinHeight = 66;
                control = box;
                _readers.Add((field, () => box.Text));
                break;
            }

            case Widget.Date:
            {
                var picker = new DatePicker
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    SelectedDate = DateOnly.TryParse(current as string, CultureInfo.InvariantCulture, out var parsed)
                        ? new DateTimeOffset(parsed.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
                        : null,
                };
                picker.SelectedDateChanged += (_, _) => Touch();
                control = picker;
                _readers.Add((field, () => picker.SelectedDate is { } d
                    ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : null));
                break;
            }

            case Widget.Number:
            {
                var input = new NumericUpDown
                {
                    Minimum = (decimal)(field.Min ?? 0),
                    Maximum = (decimal)(field.Max ?? 100000),
                    Increment = (decimal)field.Step,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = 170,
                    Value = decimal.TryParse(current as string, NumberStyles.Any, CultureInfo.InvariantCulture, out var num)
                        ? num
                        : null,
                    FormatString = field.Step < 1 ? "0.##" : "0",
                };
                input.ValueChanged += (_, _) => Touch();
                control = input;
                _readers.Add((field, () => input.Value?.ToString(CultureInfo.InvariantCulture)));
                break;
            }

            default:
            {
                var box = MakeBox(current as string, multiline: false);
                if (field.Widget == Widget.List) box.PlaceholderText = "comma separated";
                if (field.Widget == Widget.Url) box.PlaceholderText = "https://…";
                control = box;
                _readers.Add((field, () => box.Text));
                break;
            }
        }

        panel.Children.Add(control);

        if (field.Help is { Length: > 0 })
        {
            panel.Children.Add(new TextBlock { Text = field.Help, Classes = { "faint" } });
        }

        return panel;
    }

    private TextBox MakeBox(string? text, bool multiline)
    {
        var box = new TextBox
        {
            Text = text ?? string.Empty,
            AcceptsReturn = multiline,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
        };
        box.TextChanged += (_, _) => Touch();
        return box;
    }

    private Control BuildSlugField(EditorPageViewModel model)
    {
        var panel = new StackPanel { Spacing = 3 };
        panel.Children.Add(new TextBlock { Text = "File name", Classes = { "label" } });

        var box = new TextBox
        {
            Text = model.SlugInput,
            PlaceholderText = "made from the title",
        };
        box.TextChanged += (_, _) =>
        {
            model.SlugInput = box.Text ?? string.Empty;
            Touch();
        };

        panel.Children.Add(box);
        panel.Children.Add(new TextBlock
        {
            Text = "Latin letters, digits, hyphen. Changing it renames the file.",
            Classes = { "faint" },
        });
        return panel;
    }

    // ------------------------------------------------------- markdown-блок

    private void SetupBody(EditorPageViewModel model)
    {
        var area = this.FindControl<Grid>("EditorArea")!;
        var body = this.FindControl<Grid>("Body")!;
        var box = this.FindControl<TextBox>("BodyBox")!;

        if (model.Collection.Body is not { } field)
        {
            // Розділ без тексту — форма займає все місце
            area.IsVisible = false;
            body.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            body.RowDefinitions[1].Height = new GridLength(0);
            return;
        }

        area.IsVisible = true;
        body.RowDefinitions[0].Height = GridLength.Auto;
        body.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
        this.FindControl<ScrollViewer>("FieldsScroll")!.MaxHeight = 260;
        this.FindControl<TextBlock>("BodyLabel")!.Text = field.Required ? field.Label + " *" : field.Label;

        box.Text = model.Values.GetValueOrDefault(field.Name) as string ?? string.Empty;
        BuildToolbar(box);
    }

    private readonly record struct Tool(string Label, string Tip, Action<TextBox> Run);

    private void BuildToolbar(TextBox box)
    {
        var toolbar = this.FindControl<WrapPanel>("Toolbar")!;
        toolbar.Children.Clear();

        Tool[] tools =
        [
            new("B", "Bold", t => Wrap(t, "**", "**", "text")),
            new("I", "Italic", t => Wrap(t, "*", "*", "text")),
            new("H2", "Heading", t => Prefix(t, _ => "## ")),
            new("H3", "Smaller heading", t => Prefix(t, _ => "### ")),
            new("🔗", "Link", t => Wrap(t, "[", "](https://)", "label")),
            new("❝", "Quote", t => Prefix(t, _ => "> ")),
            new("•", "Bullet list", t => Prefix(t, _ => "- ")),
            new("1.", "Numbered list", t => Prefix(t, i => $"{i + 1}. ")),
            new("<>", "Inline code", t => Wrap(t, "`", "`", "code")),
            new("{ }", "Code block", t => Wrap(t, "```\n", "\n```", "code")),
            new("—", "Divider", t => Wrap(t, "\n---\n", "", "")),
        ];

        foreach (var tool in tools)
        {
            var button = new Button { Content = tool.Label, Classes = { "tool" } };
            ToolTip.SetTip(button, tool.Tip);
            button.Click += (_, _) => tool.Run(box);
            toolbar.Children.Add(button);
        }
    }

    /// <summary>Обгортає виділене, або вставляє заготовку й виділяє її.</summary>
    private static void Wrap(TextBox box, string before, string after, string placeholder)
    {
        var text = box.Text ?? string.Empty;
        var (start, end) = Range(box, text);

        var selected = text[start..end];
        var hadSelection = selected.Length > 0;
        if (!hadSelection) selected = placeholder;

        box.Text = text[..start] + before + selected + after + text[end..];

        if (hadSelection)
        {
            box.CaretIndex = start + before.Length + selected.Length + after.Length;
        }
        else
        {
            box.SelectionStart = start + before.Length;
            box.SelectionEnd = start + before.Length + selected.Length;
        }

        box.Focus();
    }

    /// <summary>Ставить префікс на початку кожного зачепленого рядка.</summary>
    private static void Prefix(TextBox box, Func<int, string> makePrefix)
    {
        var text = box.Text ?? string.Empty;
        var (start, end) = Range(box, text);

        var from = text.LastIndexOf('\n', Math.Max(start - 1, 0)) + 1;
        if (start == 0) from = 0;

        var toIndex = text.IndexOf('\n', end);
        var to = toIndex < 0 ? text.Length : toIndex;

        var lines = text[from..to].Split('\n');
        var updated = string.Join('\n', lines.Select((line, i) => makePrefix(i) + line));

        box.Text = text[..from] + updated + text[to..];
        box.CaretIndex = from + updated.Length;
        box.Focus();
    }

    private static (int Start, int End) Range(TextBox box, string text)
    {
        var a = Math.Clamp(box.SelectionStart, 0, text.Length);
        var b = Math.Clamp(box.SelectionEnd, 0, text.Length);
        return a <= b ? (a, b) : (b, a);
    }

    // ------------------------------------------------------------- події

    private void Touch()
    {
        if (_building) return;
        Model?.MarkDirty();
        UpdateStatusColour();
    }

    private void OnBodyChanged(object? sender, TextChangedEventArgs e) => Touch();

    private void OnTogglePreview(object? sender, RoutedEventArgs e)
    {
        var toggle = this.FindControl<ToggleButton>("PreviewToggle")!;
        var box = this.FindControl<TextBox>("BodyBox")!;
        var scroll = this.FindControl<ScrollViewer>("PreviewScroll")!;
        var host = this.FindControl<ContentControl>("PreviewHost")!;

        var showing = toggle.IsChecked == true;
        box.IsVisible = !showing;
        scroll.IsVisible = showing;

        if (showing)
        {
            host.Content = MarkdownRenderer.Render(box.Text ?? string.Empty, this);
        }
    }

    private void Collect()
    {
        if (Model is not { } model) return;

        foreach (var (field, read) in _readers)
        {
            model.Values[field.Name] = read();
        }

        if (model.Collection.Body is { } body)
        {
            model.Values[body.Name] = this.FindControl<TextBox>("BodyBox")!.Text ?? string.Empty;
        }
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        Collect();
        Model?.Save();
        UpdateStatusColour();
    }

    private void OnBack(object? sender, RoutedEventArgs e) => Model?.Back();

    private async void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (Model is not { } model) return;

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is null) return;

        var confirmed = await ConfirmDialog.AskAsync(
            owner,
            "Delete permanently?",
            $"«{model.Title}» will be erased from disk. This cannot be undone.");

        if (confirmed) model.Delete();
    }

    private void UpdateStatusColour()
    {
        if (Model is not { } model) return;

        var status = this.FindControl<TextBlock>("StatusText")!;
        status.Foreground = model.StatusIsError
            ? Find("AppDanger")
            : Find("AppMuted");
    }

    // Кольори лежать у ThemeDictionaries — шукати треба разом із варіантом теми
    private IBrush Find(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush
            ? brush
            : Brushes.Gray;
}
