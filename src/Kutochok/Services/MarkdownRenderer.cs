using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Kutochok.Services;

/// <summary>
/// Малює markdown контролами Avalonia.
///
/// Готової бібліотеки під Avalonia 12 немає, тому парсимо через Markdig (він
/// не залежить від UI) і будуємо дерево контролів самі. Плюс у тому, що
/// передперегляд бере кольори з теми застосунку й не виглядає чужорідним.
/// </summary>
public static class MarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseEmphasisExtras()
        .UsePipeTables()
        .UseAutoLinks()
        .Build();

    public static Control Render(string markdown, Control owner)
    {
        var panel = new StackPanel { Spacing = 10 };

        if (string.IsNullOrWhiteSpace(markdown))
        {
            panel.Children.Add(new TextBlock
            {
                Text = "Порожньо. Напиши щось у полі поруч.",
                Foreground = Brush(owner, "AppFaint"),
                FontStyle = FontStyle.Italic,
            });
            return panel;
        }

        var document = Markdown.Parse(markdown, Pipeline);
        foreach (var block in document)
        {
            var control = RenderBlock(block, owner);
            if (control is not null) panel.Children.Add(control);
        }

        return panel;
    }

    // ------------------------------------------------------------- блоки

    private static Control? RenderBlock(Block block, Control res) => block switch
    {
        HeadingBlock heading => RenderHeading(heading, res),
        ParagraphBlock paragraph => RenderParagraph(paragraph, res),
        ListBlock list => RenderList(list, res),
        QuoteBlock quote => RenderQuote(quote, res),
        CodeBlock code => RenderCode(code, res),
        ThematicBreakBlock => new Border
        {
            Height = 1,
            Background = Brush(res, "AppBorder"),
            Margin = new Thickness(0, 8),
        },
        _ => null,
    };

    private static Control RenderHeading(HeadingBlock heading, Control res)
    {
        var size = heading.Level switch
        {
            1 => 21.0,
            2 => 17.5,
            3 => 15.5,
            _ => 14.0,
        };

        var text = new SelectableTextBlock
        {
            FontSize = size,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush(res, "AppText"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, heading.Level <= 2 ? 8 : 4, 0, 0),
        };

        FillInlines(text.Inlines!, heading.Inline, res);
        return text;
    }

    private static Control RenderParagraph(ParagraphBlock paragraph, Control res)
    {
        var text = new SelectableTextBlock
        {
            Foreground = Brush(res, "AppText"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
        };

        FillInlines(text.Inlines!, paragraph.Inline, res);
        return text;
    }

    private static Control RenderList(ListBlock list, Control res)
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(4, 0, 0, 0) };
        var index = list.IsOrdered && int.TryParse(list.OrderedStart, out var start) ? start : 1;

        foreach (var item in list)
        {
            if (item is not ListItemBlock listItem) continue;

            var marker = new TextBlock
            {
                Text = list.IsOrdered ? $"{index}." : "•",
                Foreground = Brush(res, "AppMuted"),
                Width = list.IsOrdered ? 22 : 14,
                VerticalAlignment = VerticalAlignment.Top,
            };

            var content = new StackPanel { Spacing = 4 };
            foreach (var child in listItem)
            {
                var rendered = RenderBlock(child, res);
                if (rendered is not null) content.Children.Add(rendered);
            }

            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            row.Children.Add(marker);
            content.HorizontalAlignment = HorizontalAlignment.Stretch;
            row.Children.Add(content);

            panel.Children.Add(row);
            index++;
        }

        return panel;
    }

    private static Control RenderQuote(QuoteBlock quote, Control res)
    {
        var content = new StackPanel { Spacing = 8 };
        foreach (var child in quote)
        {
            var rendered = RenderBlock(child, res);
            if (rendered is not null) content.Children.Add(rendered);
        }

        return new Border
        {
            BorderBrush = Brush(res, "AppBorder"),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(12, 2, 0, 2),
            Child = content,
        };
    }

    private static Control RenderCode(CodeBlock code, Control res)
    {
        var lines = string.Join('\n', code.Lines.Lines
            .Take(code.Lines.Count)
            .Select(l => l.ToString()));

        return new Border
        {
            Background = Brush(res, "AppPanel"),
            BorderBrush = Brush(res, "AppBorder"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(12, 9),
            Child = new SelectableTextBlock
            {
                Text = lines.TrimEnd(),
                FontFamily = MonoFont(res),
                FontSize = 13,
                Foreground = Brush(res, "AppText"),
                TextWrapping = TextWrapping.NoWrap,
            },
        };
    }

    // ------------------------------------------------------------ рядкове

    private static void FillInlines(InlineCollection target, ContainerInline? container, Control res)
    {
        if (container is null) return;

        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    target.Add(new Run(literal.Content.ToString()));
                    break;

                case EmphasisInline emphasis:
                {
                    var span = new Span();
                    // ** — жирний, * — курсив, ~~ — закреслений
                    if (emphasis.DelimiterChar == '~') span.TextDecorations = TextDecorations.Strikethrough;
                    else if (emphasis.DelimiterCount >= 2) span.FontWeight = FontWeight.Bold;
                    else span.FontStyle = FontStyle.Italic;

                    FillInlines(span.Inlines, emphasis, res);
                    target.Add(span);
                    break;
                }

                case CodeInline code:
                    target.Add(new Run(code.Content)
                    {
                        FontFamily = MonoFont(res),
                        Foreground = Brush(res, "AppAccent"),
                    });
                    break;

                case LinkInline link:
                    target.Add(BuildLink(link, res));
                    break;

                case LineBreakInline lineBreak:
                    if (lineBreak.IsHard) target.Add(new LineBreak());
                    else target.Add(new Run(" "));
                    break;

                case ContainerInline nested:
                    FillInlines(target, nested, res);
                    break;
            }
        }
    }

    private static Avalonia.Controls.Documents.Inline BuildLink(LinkInline link, Control res)
    {
        var label = new List<string>();
        CollectText(link, label);
        var text = string.Concat(label);
        var url = link.Url ?? string.Empty;

        if (link.IsImage)
        {
            // Картинки не показуємо — лишаємо видимий слід, щоб текст не «зникав»
            return new Run($"[картинка: {(text.Length > 0 ? text : url)}]")
            {
                Foreground = Brush(res, "AppFaint"),
                FontStyle = FontStyle.Italic,
            };
        }

        var clickable = new TextBlock
        {
            Text = text.Length > 0 ? text : url,
            Foreground = Brush(res, "AppLink"),
            TextDecorations = TextDecorations.Underline,
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        ToolTip.SetTip(clickable, url);
        clickable.PointerPressed += (_, _) => OpenUrl(url);

        return new InlineUIContainer(clickable);
    }

    private static void CollectText(ContainerInline container, List<string> into)
    {
        foreach (var inline in container)
        {
            switch (inline)
            {
                case LiteralInline literal: into.Add(literal.Content.ToString()); break;
                case CodeInline code: into.Add(code.Content); break;
                case ContainerInline nested: CollectText(nested, into); break;
            }
        }
    }

    /// <summary>Відкриває посилання у браузері за замовчуванням (Windows і macOS).</summary>
    public static void OpenUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return;

        try
        {
            Process.Start(new ProcessStartInfo(uri.ToString()) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Немає браузера або система заборонила — мовчки нічого не робимо
        }
    }

    // ------------------------------------------------------------ ресурси

    /// <summary>
    /// Кольори теми лежать у ThemeDictionaries, тому шукати їх треба разом із
    /// поточним варіантом теми — інакше знаходиться порожньо й усе сіріє.
    /// </summary>
    private static object? Resource(Control owner, string key) =>
        owner.TryFindResource(key, owner.ActualThemeVariant, out var value) ? value : null;

    private static IBrush Brush(Control res, string key) =>
        Resource(res, key) is IBrush brush ? brush : Brushes.Gray;

    private static FontFamily MonoFont(Control res) =>
        Resource(res, "MonoFont") is FontFamily family
            ? family
            : FontFamily.Parse("Consolas,Menlo,monospace");
}
