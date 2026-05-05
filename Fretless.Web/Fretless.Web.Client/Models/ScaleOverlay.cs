namespace Fretless.Web.Client.Models;

/// <summary>A resolved scale: root + definition → set of note indices 0–11.</summary>
public record ScaleOverlay(string Root, ScaleDefinition Scale)
{
    /// <summary>Semitone indices (0–11) that belong to this scale.</summary>
    public HashSet<int> NoteSet { get; } = Scale.Intervals
        .Select(i => (MusicTheoryService.NoteIndex(Root) + i) % 12)
        .ToHashSet();

    /// <summary>The root note index (0–11).</summary>
    public int RootIndex => MusicTheoryService.NoteIndex(Root);

    public string DisplayName => $"{Root} {Scale.Name}";
}
