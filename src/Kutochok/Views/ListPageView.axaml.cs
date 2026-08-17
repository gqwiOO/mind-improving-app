using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Kutochok.Models;
using Kutochok.ViewModels;

namespace Kutochok.Views;

public partial class ListPageView : UserControl
{
    public ListPageView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private ListPageViewModel? Model => DataContext as ListPageViewModel;

    /// <summary>І кнопка рядка, і пункт меню несуть рядок у Tag.</summary>
    private static Entry? EntryOf(object? sender) => sender switch
    {
        Button { Tag: EntryRowViewModel row } => row.Entry,
        MenuItem { Tag: EntryRowViewModel row } => row.Entry,
        _ => null,
    };

    private void OnAdd(object? sender, RoutedEventArgs e) => Model?.Add();

    private void OnTagClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag }) Model?.ToggleTag(tag);
    }

    // ЛКМ по рядку — відкрити на читання
    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is { } entry) Model?.Open(entry);
    }

    private void OnMenuOpen(object? sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is { } entry) Model?.Open(entry);
    }

    private void OnMenuEdit(object? sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is { } entry) Model?.Edit(entry);
    }

    private async void OnMenuDelete(object? sender, RoutedEventArgs e)
    {
        if (EntryOf(sender) is not { } entry) return;
        if (Model is not { } model) return;
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var confirmed = await ConfirmDialog.AskAsync(
            owner,
            "Delete permanently?",
            $"«{entry.Title}» will be erased from disk. This cannot be undone.");

        if (confirmed) model.Delete(entry);
    }
}
