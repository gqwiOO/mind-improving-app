using System;
using System.Globalization;
using System.Threading;
using Avalonia;

namespace Kutochok;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Інтерфейс застосунку англійською: назви місяців у виборі дати тощо.
        // Дані у файлах при цьому лишаються в InvariantCulture — крапка як
        // роздільник дробу й дата виду 2026-08-17 незалежно від системи.
        var ui = new CultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentCulture = ui;
        CultureInfo.DefaultThreadCurrentUICulture = ui;
        Thread.CurrentThread.CurrentCulture = ui;
        Thread.CurrentThread.CurrentUICulture = ui;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
