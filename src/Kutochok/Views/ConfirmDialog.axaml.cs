using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Kutochok.Views;

/// <summary>Модальне «ти впевнений?» — Avalonia не має вбудованого.</summary>
public partial class ConfirmDialog : Window
{
    private bool _result;

    public ConfirmDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public static async Task<bool> AskAsync(Window owner, string heading, string message)
    {
        var dialog = new ConfirmDialog();
        dialog.FindControl<TextBlock>("HeadingText")!.Text = heading;
        dialog.FindControl<TextBlock>("MessageText")!.Text = message;

        await dialog.ShowDialog(owner);
        return dialog._result;
    }

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        _result = true;
        Close();
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();
}
