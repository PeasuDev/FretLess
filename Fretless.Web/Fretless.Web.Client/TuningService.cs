using Fretless.Web.Client.Models;

namespace Fretless.Web.Client;

public static class TuningService
{
    // MIDI note numbers: C0=0, C4=48 (middle C), standard guitar E2=40
    public static readonly IReadOnlyList<TuningPreset> Presets =
    [
        new("Standard (EADGBE)",         [40, 45, 50, 55, 59, 64]),
        new("Drop D (DADGBE)",           [38, 45, 50, 55, 59, 64]),
        new("Open G (DGDGBD)",           [38, 45, 50, 55, 59, 62]),
        new("Open D (DADF#AD)",          [38, 45, 50, 54, 57, 62]),
        new("Open E (EBEG#BE)",          [40, 47, 52, 56, 59, 64]),
        new("Open A (EAEAC#E)",          [40, 45, 52, 57, 61, 64]),
        new("DADGAD",                    [38, 45, 50, 55, 57, 62]),
        new("Half Step Down (Eb)",       [39, 44, 49, 54, 58, 63]),
        new("Full Step Down (D)",        [38, 43, 48, 53, 57, 62]),
        new("7-String Standard",         [35, 40, 45, 50, 55, 59, 64]),
        new("7-String Drop A",           [33, 40, 45, 50, 55, 59, 64]),
        new("8-String Standard",         [28, 35, 40, 45, 50, 55, 59, 64]),
        new("9-String Standard",         [23, 28, 35, 40, 45, 50, 55, 59, 64]),
        new("Bass (EADG 4-string)",      [28, 33, 38, 43]),
        new("Bass (BEADG 5-string)",     [23, 28, 33, 38, 43]),
        new("Custom",                    [40, 45, 50, 55, 59, 64]),
    ];

    public static TuningPreset Standard => Presets[0];

    // All note names across octaves for the per-string dropdown (MIDI 16–84)
    public static IReadOnlyList<(int Midi, string Label)> AllNotes { get; } =
        Enumerable.Range(16, 69)
            .Select(m => (m, MidiLabel(m)))
            .ToList();

    public static string MidiLabel(int midi)
    {
        var noteNames = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        var octave = (midi / 12) - 1;
        var name = noteNames[midi % 12];
        return $"{name}{octave}";
    }
}
