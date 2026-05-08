using System.Collections.Generic;

namespace Fretless.Web.Client.Library;

/// <summary>
/// Central registry of every demo shown in the /components gallery.
/// Phase 1 leaves this empty; Phase 2 populates it as as-is demos are added.
/// </summary>
public static class LibraryRegistry
{
    public static IReadOnlyList<LibraryEntry> All { get; } = new List<LibraryEntry>();

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
