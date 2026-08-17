using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Kutochok.Models;
using Kutochok.Services;

namespace Kutochok.ViewModels;

/// <summary>Пункт бічного меню.</summary>
public partial class SectionViewModel : ViewModelBase
{
    public required CollectionDef Collection { get; init; }

    public string Label => Collection.Label;

    [ObservableProperty] private int _count;
    [ObservableProperty] private bool _isActive;
}

public partial class MainViewModel : ViewModelBase
{
    public ContentStore Store { get; } = new();

    public ObservableCollection<SectionViewModel> Sections { get; } = [];

    [ObservableProperty] private ViewModelBase? _currentPage;

    /// <summary>Шлях до теки з контентом — показуємо, щоб файли не були таємницею.</summary>
    public string ContentRoot => ContentPaths.Root;

    public MainViewModel()
    {
        ContentPaths.EnsureCreated();

        foreach (var collection in Schema.Collections)
        {
            Sections.Add(new SectionViewModel { Collection = collection });
        }

        RefreshCounts();
        if (Sections.Count > 0) ShowList(Sections[0].Collection);
    }

    public void ShowList(CollectionDef collection)
    {
        foreach (var item in Sections)
        {
            item.IsActive = item.Collection.Name == collection.Name;
        }

        RefreshCounts();
        CurrentPage = new ListPageViewModel(this, collection);
    }

    public void ShowEditor(CollectionDef collection, Entry? entry, string initialStatus = "")
    {
        CurrentPage = new EditorPageViewModel(this, collection, entry, initialStatus);
    }

    public void RefreshCounts()
    {
        foreach (var section in Sections)
        {
            try { section.Count = Store.List(section.Collection).Count; }
            catch (Exception) { section.Count = 0; }
        }
    }

    /// <summary>Усі теги з усіх розділів — для фільтра.</summary>
    public IReadOnlyList<string> AllTags()
    {
        var tags = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
        foreach (var collection in Schema.Collections.Where(c => c.HasTags))
        {
            foreach (var entry in Store.List(collection))
            {
                foreach (var tag in entry.Tags) tags.Add(tag);
            }
        }
        return tags.OrderBy(t => t, StringComparer.CurrentCulture).ToList();
    }
}
