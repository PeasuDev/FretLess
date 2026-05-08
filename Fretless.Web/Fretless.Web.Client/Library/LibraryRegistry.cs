using System.Collections.Generic;
using Fretless.Web.Client.Library.Demos.Components;

namespace Fretless.Web.Client.Library;

/// <summary>
/// Central registry of every demo shown in the /components gallery.
/// Phase 2 populates this incrementally with as-is demos for existing components.
/// </summary>
public static class LibraryRegistry
{
    public static IReadOnlyList<LibraryEntry> All { get; } = new List<LibraryEntry>
    {
        new(
            Slug: "mood-legend",
            Title: "MoodLegend",
            Description: "Row of mood filter chips driven by MusicTheoryService.MoodColors.",
            Group: LibraryGroup.Components,
            DemoType: typeof(MoodLegendDemo),
            SourcePath: "Fretless.Web/Fretless.Web.Client/Components/MoodLegend.razor"),
    };

    public static LibraryEntry? FindBySlug(LibraryGroup group, string slug)
    {
        foreach (var entry in All)
        {
            if (entry.Group == group && string.Equals(entry.Slug, slug, System.StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }
        return null;
    }

    public static IReadOnlyList<LibraryEntry> ForGroup(LibraryGroup group)
    {
        var result = new List<LibraryEntry>();
        foreach (var entry in All)
        {
            if (entry.Group == group)
            {
                result.Add(entry);
            }
        }
        return result;
    }
}
