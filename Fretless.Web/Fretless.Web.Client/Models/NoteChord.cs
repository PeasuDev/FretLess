namespace Fretless.Web.Client.Models;

public record NoteChord(
    string Root,
    ChordType Type
)
{
    public string DisplayName => Root + Type.Symbol;
    public int[] AbsoluteNotes => Type.Intervals
        .Select(i => (MusicTheoryService.NoteIndex(Root) + i) % 12)
        .ToArray();
}
