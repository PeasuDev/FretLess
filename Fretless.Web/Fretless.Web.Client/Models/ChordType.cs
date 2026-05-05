namespace Fretless.Web.Client.Models;

public record ChordType(
    string Symbol,
    string Name,
    string Description,
    ChordMood Mood,
    int[] Intervals,   // semitone intervals from root
    int Commonality    // 1 = most common, higher = less common
);
