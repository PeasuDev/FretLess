namespace Fretless.Web.Client.Models;

public enum PairingStrength { Resolved, Suspended }

public record ChordPairing(
    NoteChord Chord,
    string Relationship,
    PairingStrength Strength
);
