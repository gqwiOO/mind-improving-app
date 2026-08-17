using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
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

    private void OnAdd(object? sender, RoutedEventArgs e) => Model?.Add();

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: EntryRowViewModel row }) Model?.Open(row.Entry);
    }

    private void OnTagClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string tag }) Model?.ToggleTag(tag);
    }
}
