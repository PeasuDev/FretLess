namespace Fretless.Web.Client.Models;

public record TuningPreset(string Name, int[] MidiNotes)
{
    public int StringCount => MidiNotes.Length;
}
