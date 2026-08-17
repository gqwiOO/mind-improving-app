using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Kutochok.Services;
using Kutochok.ViewModels;

namespace Kutochok.Views;

public partial class ReaderPageView : UserControl
{
    public ReaderPageView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => RenderBody();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private ReaderPageViewModel? Model => DataContext as ReaderPageViewModel;

    private void RenderBody()
    {
        var host = this.FindControl<ContentControl>("BodyHost")!;
        host.Content = Model is { } model
            ? MarkdownRenderer.Render(model.BodyMarkdown, this)
            : null;
    }

    private void OnBack(object? sender, RoutedEventArgs e) => Model?.Back();

    private void OnEdit(object? sender, RoutedEventArgs e) => Model?.Edit();

    private void OnOpenLink(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string url }) MarkdownRenderer.OpenUrl(url);
    }

    private async void OnDelete(object? sender, RoutedEventArgs e)
    {
        if (Model is not { } model) return;
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var confirmed = await ConfirmDialog.AskAsync(
            owner,
            "Delete permanently?",
            $"«{model.Title}» will be erased from disk. This cannot be undone.");

        if (!confirmed) return;

        if (model.Delete() is { } error)
        {
            var status = this.FindControl<TextBlock>("StatusText")!;
            status.Text = error;
            status.Foreground = this.TryFindResource("AppDanger", ActualThemeVariant, out var brush) && brush is IBrush b
                ? b
                : Brushes.Red;
        }
    }
}
