using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Kutochok.ViewModels;

namespace Kutochok.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnSectionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SectionViewModel section } && DataContext is MainViewModel main)
        {
            main.ShowList(section.Collection);
        }
    }

    private void OnOpenFolder(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel main) return;

        try
        {
            // UseShellExecute відкриває теку провідником на Windows і Finder на macOS
            Process.Start(new ProcessStartInfo(main.ContentRoot) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Якщо система не дала — не біда, шлях усе одно написаний поруч
        }
    }
}
