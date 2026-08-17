using System;
using System.IO;

namespace Kutochok.Services;

/// <summary>
/// Де живе контент. Свідомо в «Документах», а не в прихованій теці програми:
/// це звичайні markdown- і yaml-файли, і ти маєш бачити їх, копіювати й
/// відкривати будь-чим іншим, не питаючи застосунок.
/// </summary>
public static class ContentPaths
{
    public const string FolderName = "Kutochok";

    public static string Root { get; private set; } = DefaultRoot();

    private static string DefaultRoot()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        // На деяких системах MyDocuments порожній — тоді беремо домашню теку
        if (string.IsNullOrEmpty(documents))
        {
            documents = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Path.Combine(documents, FolderName);
    }

    /// <summary>Абсолютний шлях до файлу чи теки розділу.</summary>
    public static string Resolve(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(Root, relative));
        var root = Root.EndsWith(Path.DirectorySeparatorChar) ? Root : Root + Path.DirectorySeparatorChar;

        if (!full.StartsWith(root, StringComparison.Ordinal) &&
            !string.Equals(full, Root, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Шлях виходить за межі теки контенту: {relative}");
        }

        return full;
    }

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        foreach (var collection in Models.Schema.Collections)
        {
            if (collection.Kind == Models.StorageKind.Markdown)
            {
                Directory.CreateDirectory(Resolve(collection.Path));
            }
        }
    }
}
