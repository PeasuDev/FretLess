using System.Collections.Generic;
using Fretless.Web.Client.Library.Demos.Atoms;
using Fretless.Web.Client.Library.Demos.Components;

namespace Fretless.Web.Client.Library;

/// <summary>
/// Central registry of every demo shown in the /components gallery.
/// Populated incrementally as Phase 2 (component demos) and Phase 3 (atoms) ship.
/// </summary>
public static class LibraryRegistry
{
    public static IReadOnlyList<LibraryEntry> All { get; } = new List<LibraryEntry>
    {
        new(
            Slug: "mood-chip",
            Title: "MoodChip",
            Description: "Single mood-filter chip atom: pill button bordered in a mood color, with mood + mode label and an Active state.",
            Group: LibraryGroup.Atoms,
            DemoType: typeof(MoodChipDemo),
            SourcePath: "Fretless.Web/Fretless.Web.Client/Components/Atoms/MoodChip.razor"),
        new(
            Slug: "note-chip",
            Title: "NoteChip",
            Description: "Pill-shaped note label with a colored background; optional ✕ remove button when Removable=true.",
            Group: LibraryGroup.Atoms,
            DemoType: typeof(NoteChipDemo),
            SourcePath: "Fretless.Web/Fretless.Web.Client/Components/Atoms/NoteChip.razor"),
        new(
            Slug: "roman-numeral-button",
            Title: "RomanNumeralButton",
            Description: "Position-selector button: white pill, gold-Active. Numeral accepts any short label (I, IV, All, etc.).",
            Group: LibraryGroup.Atoms,
            DemoType: typeof(RomanNumeralButtonDemo),
            SourcePath: "Fretless.Web/Fretless.Web.Client/Components/Atoms/RomanNumeralButton.razor"),
        new(
            Slug: "scale-note-chip",
            Title: "ScaleNoteChip",
            Description: "Small pill labeling a scale degree; gold for root, cyan glow for playing.",
            Group: LibraryGroup.Atoms,
            DemoType: typeof(ScaleNoteChipDemo),
            SourcePath: "Fretless.Web/Fretless.Web.Client/Components/Atoms/ScaleNoteChip.razor"),
        new(
            Slug: "string-tuning-row",
            Title: "StringTuningRow",
            Description: "Per-string editor row: number, gauge marker, MIDI-note dropdown, resolved note label.",
            Group: LibraryGroup.Atoms,
            DemoType: typeof(StringTuningRowDemo),
            SourcePath: "Fretless.Web/Fretless.Web.Client/Components/Atoms/StringTuningRow.razor"),
        new(
            Slug: "chord-builder-panel",
            Title: "ChordBuilderPanel",
            Description: "Built-up chord accumulator with removable note chips, stability cluster at 3+ notes, and chord summary.",
            Group: LibraryGroup.Components,
            DemoType: typeof(ChordBuilderPanelDemo),
            SourcePath: "Fretless.Web/Fretless.Web.Client/Components/ChordBuilderPanel.razor"),
        new(
            Slug: "chord-pairings",
            Title: "ChordPairings",
            Description: "Lists chords that pair with the active chord, grouped into Resolved & Diatonic vs Suspended & Borrowed.",
            Group: LibraryGroup.Components,
            DemoType: typeof(ChordPairingsDemo),
            SourcePath: "Fretless.Web/Fretless.Web.Client/Components/ChordPairings.razor"),
        new(
            Slug: "harmonic-stability-display",
            Title: "HarmonicStabilityDisplay",
            Description: "SVG bubble cluster encoding pairwise consonance of 3+ notes; renders nothing below the threshold.",
            Group: LibraryGroup.Components,
            DemoType: typeof(HarmonicStabilityDisplayDemo),
            SourcePath: "Fretless.Web/Fretless.Web.Client/Components/HarmonicStabilityDisplay.razor"),
        new(
            Slug: "mood-legend",
            Title: "MoodLegend",
            Description: "Row of mood filter chips driven by MusicTheoryService.MoodColors.",
            Group: LibraryGroup.Components,
            DemoType: typeof(MoodLegendDemo),
            SourcePath: "Fretless.Web/Fretless.Web.Client/Components/MoodLegend.razor"),
        new(
            Slug: "tuning-selector",
            Title: "TuningSelector",
            Description: "Preset dropdown + collapsible per-string MIDI-note editor; Inline mode forces expanded with a Done button.",
            Group: LibraryGroup.Components,
            DemoType: typeof(TuningSelectorDemo),
            SourcePath: "Fretless.Web/Fretless.Web.Client/Components/TuningSelector.razor"),
        new(
            Slug: "scale-selector",
            Title: "ScaleSelector",
            Description: "Collapsible Key + Scale picker that emits a ScaleOverlay (root + scale definition + computed note set).",
            Group: LibraryGroup.Components,
            DemoType: typeof(ScaleSelectorDemo),
            SourcePath: "Fretless.Web/Fretless.Web.Client/Components/ScaleSelector.razor"),
        new(
            Slug: "chord-wheel",
            Title: "ChordWheel",
            Description: "Interactive circle-of-fifths wheel with diatonic chord drill-down; emits OnChordSelected when a chord card is tapped.",
            Group: LibraryGroup.Components,
            DemoType: typeof(ChordWheelDemo),
            SourcePath: "Fretless.Web/Fretless.Web.Client/Components/ChordWheel.razor"),
        new(
            Slug: "fretboard",
            Title: "Fretboard",
            Description: "12-fret SVG with audio, MIDI input, scale overlay, position selector, manual dots, and tuning controls.",
            Group: LibraryGroup.Components,
            DemoType: typeof(FretboardDemo),
            SourcePath: "Fretless.Web/Fretless.Web.Client/Components/Fretboard.razor"),
        new(
            Slug: "spiral-wheel",
            Title: "SpiralWheel",
            Description: "Self-contained spiral chart with key/scale, instrument overlays, ratios, and zoom; state persisted in localStorage and shared with the home page.",
            Group: LibraryGroup.Components,
            DemoType: typeof(SpiralWheelDemo),
            SourcePath: "Fretless.Web/Fretless.Web.Client/Components/SpiralWheel.razor"),
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
