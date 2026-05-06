using Fretless.Web.Client.Models;

namespace Fretless.Web.Client;

public static class MusicTheoryService
{
    // Circle of fifths order (major keys)
    public static readonly string[] CircleOfFifths =
    [
        "C", "G", "D", "A", "E", "B", "F#", "Db", "Ab", "Eb", "Bb", "F"
    ];

    public static readonly string[] ChromaticNotes =
    [
        "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"
    ];

    // Enharmonic display names aligned to circle of fifths
    private static readonly Dictionary<string, string> EnharmonicDisplay = new()
    {
        ["F#"] = "F#/Gb",
        ["Db"] = "Db/C#",
        ["Ab"] = "Ab/G#",
        ["Eb"] = "Eb/D#",
        ["Bb"] = "Bb/A#",
    };

    public static string DisplayName(string note) =>
        EnharmonicDisplay.TryGetValue(note, out var d) ? d : note;

    // Sharp-only spellings for notes that are normally stored as flats in CircleOfFifths
    private static readonly Dictionary<string, string> SharpOnlyNames = new()
    {
        ["Db"] = "C#", ["Ab"] = "G#", ["Eb"] = "D#", ["Bb"] = "A#",
    };

    // Flat-only spellings for notes stored as sharps in CircleOfFifths
    private static readonly Dictionary<string, string> FlatOnlyNames = new()
    {
        ["F#"] = "Gb",
    };

    /// <summary>Returns the notation-specific display name (single spelling, no slash).</summary>
    public static string DisplayName(string note, bool useFlats) =>
        useFlats
            ? (FlatOnlyNames.TryGetValue(note, out var f) ? f : note)
            : (SharpOnlyNames.TryGetValue(note, out var s) ? s : note);

    // ── Interval frequency ratios (just intonation approximations) ───────────
    // Index matches ChromaticNotes (semitone 0–11 from any root).
    // Used to give each note-segment a proportional arc length in ratio mode.
    public static readonly (string Label, double Ratio)[] SemitoneRatios =
    [
        ("1",      1.0),           // unison
        ("16/15",  16.0 / 15.0),  // minor 2nd
        ("9/8",    9.0  /  8.0),  // major 2nd
        ("6/5",    6.0  /  5.0),  // minor 3rd
        ("5/4",    5.0  /  4.0),  // major 3rd
        ("4/3",    4.0  /  3.0),  // perfect 4th
        ("45/32",  45.0 / 32.0),  // tritone
        ("3/2",    3.0  /  2.0),  // perfect 5th
        ("8/5",    8.0  /  5.0),  // minor 6th
        ("5/3",    5.0  /  3.0),  // major 6th
        ("9/5",    9.0  /  5.0),  // minor 7th
        ("15/8",   15.0 /  8.0),  // major 7th
    ];

    // Returns the arc-length weight for each semitone step in ratio mode.
    // Weight = ratio[i] - ratio[i-1] (the gap between successive ratios),
    // normalised so all 12 weights sum to 1.
    public static double[] RatioSegmentWeights()
    {
        var raw = new double[12];
        for (int i = 0; i < 12; i++)
        {
            var prev = i == 0 ? 0.0 : SemitoneRatios[i - 1].Ratio;
            raw[i] = SemitoneRatios[i].Ratio - prev;
        }
        var total = raw.Sum();
        return raw.Select(w => w / total).ToArray();
    }

    // CoF-order ratio weights: maps CoF index → proportional segment weight.
    // In ratio mode the notes are shown in CoF order on the spiral, so we need
    // the semitone distance from C for each CoF note.
    public static double[] CofRatioWeights()
    {
        var semitoneWeights = RatioSegmentWeights();
        // Semitone index of each CoF note (relative to C = 0)
        var cofSemitones = CircleOfFifths
            .Select(n => NoteIndex(n))
            .ToArray();
        // Sort CoF notes by their semitone index to assign weights in pitch order,
        // then map back to CoF order.
        // Each CoF note gets the weight of the semitone step it represents.
        return CircleOfFifths
            .Select(n => semitoneWeights[NoteIndex(n)])
            .ToArray();
    }

    public static readonly IReadOnlyList<ChordType> ChordTypes = new List<ChordType>
    {
        new("",     "Major",           "Bright and stable",           ChordMood.Bright,      [0, 4, 7],        1),
        new("m",    "Minor",           "Melancholic and introspective",ChordMood.Melancholic, [0, 3, 7],        2),
        new("7",    "Dominant 7th",    "Bluesy tension",              ChordMood.Tense,       [0, 4, 7, 10],    3),
        new("maj7", "Major 7th",       "Romantic and lush",           ChordMood.Romantic,    [0, 4, 7, 11],    4),
        new("m7",   "Minor 7th",       "Smooth and moody",            ChordMood.Smooth,      [0, 3, 7, 10],    5),
        new("sus2", "Suspended 2nd",   "Open and floating",           ChordMood.Open,        [0, 2, 7],        6),
        new("sus4", "Suspended 4th",   "Unresolved and airy",         ChordMood.Airy,        [0, 5, 7],        7),
        new("aug",  "Augmented",       "Dreamy and mysterious",       ChordMood.Mysterious,  [0, 4, 8],        8),
        new("dim",  "Diminished",      "Dark and unstable",           ChordMood.Dark,        [0, 3, 6],        9),
        new("dim7", "Diminished 7th",  "Sinister and tense",          ChordMood.Sinister,    [0, 3, 6, 9],     10),
    };

    public static readonly IReadOnlyDictionary<ChordMood, string> MoodColors = new Dictionary<ChordMood, string>
    {
        [ChordMood.Bright]      = "#FFD700",
        [ChordMood.Melancholic] = "#4A90D9",
        [ChordMood.Tense]       = "#E8792B",
        [ChordMood.Romantic]    = "#E84393",
        [ChordMood.Smooth]      = "#7B68EE",
        [ChordMood.Open]        = "#3CB371",
        [ChordMood.Airy]        = "#48D1CC",
        [ChordMood.Mysterious]  = "#9B59B6",
        [ChordMood.Dark]        = "#C0392B",
        [ChordMood.Sinister]    = "#2C0A0A",
    };

    public static string MoodColor(ChordMood mood) => MoodColors[mood];

    // ── Color wheel / circle of fifths mapping ───────────────────────────────
    // Each step around the CoF = 30° of hue, so the 12-note circle maps 1:1
    // onto the 360° color wheel.  C=0° (red), G=30° (orange), D=60° (yellow)…
    public static int NoteHue(int cofIndex) => cofIndex * 30;

    public static string NoteColor(int cofIndex, int saturation = 70, int lightness = 45) =>
        $"hsl({NoteHue(cofIndex)},{saturation}%,{lightness}%)";

    public static string NoteColorByName(string note)
    {
        var idx = Array.IndexOf(CircleOfFifths, note);
        return NoteColor(idx < 0 ? 0 : idx);
    }

    // Minimum number of steps between two notes on the circle of fifths (0–6).
    // 1 = adjacent (e.g. I→V), 6 = tritone (maximum harmonic distance).
    public static int CofDistance(string fromNote, string toNote)
    {
        var a = Array.IndexOf(CircleOfFifths, fromNote);
        var b = Array.IndexOf(CircleOfFifths, toNote);
        if (a < 0 || b < 0) return 6;
        var diff = Math.Abs(a - b);
        return Math.Min(diff, 12 - diff);
    }

    public static int NoteIndex(string note)
    {
        var idx = Array.IndexOf(ChromaticNotes, note);
        if (idx >= 0) return idx;
        // handle enharmonic spellings
        return note switch
        {
            "Db" => 1,
            "Eb" => 3,
            "Gb" => 6,
            "Ab" => 8,
            "Bb" => 10,
            _ => 0
        };
    }

    public static string NoteAtIndex(int index) => ChromaticNotes[index % 12];

    public static IReadOnlyList<NoteChord> ChordsForRoot(string root) =>
        ChordTypes.Select(ct => new NoteChord(root, ct)).ToList();

    // ── Scales ───────────────────────────────────────────────────────────────
    public static readonly IReadOnlyList<ScaleDefinition> Scales =
    [
        new("Major (Ionian)",          [0, 2, 4, 5, 7, 9, 11]),
        new("Natural Minor (Aeolian)", [0, 2, 3, 5, 7, 8, 10]),
        new("Pentatonic Major",        [0, 2, 4, 7, 9]),
        new("Pentatonic Minor",        [0, 3, 5, 7, 10]),
        new("Blues",                   [0, 3, 5, 6, 7, 10]),
        new("Dorian",                  [0, 2, 3, 5, 7, 9, 10]),
        new("Phrygian",                [0, 1, 3, 5, 7, 8, 10]),
        new("Lydian",                  [0, 2, 4, 6, 7, 9, 11]),
        new("Mixolydian",              [0, 2, 4, 5, 7, 9, 10]),
        new("Locrian",                 [0, 1, 3, 5, 6, 8, 10]),
        new("Harmonic Minor",          [0, 2, 3, 5, 7, 8, 11]),
        new("Melodic Minor",           [0, 2, 3, 5, 7, 9, 11]),
        new("Whole Tone",              [0, 2, 4, 6, 8, 10]),
        new("Diminished (HW)",         [0, 1, 3, 4, 6, 7, 9, 10]),
    ];

    // Degree names for labelling scale dots (semitone offset → degree symbol)
    private static readonly Dictionary<int, string> DegreeNames = new()
    {
        [0]  = "R",
        [2]  = "2",
        [3]  = "♭3",
        [4]  = "3",
        [5]  = "4",
        [6]  = "♭5",
        [7]  = "5",
        [8]  = "♭6",
        [9]  = "6",
        [10] = "♭7",
        [11] = "7",
    };

    /// <summary>Returns the scale-degree label for a given note in a scale (e.g. "R", "♭3", "5").</summary>
    public static string ScaleDegreeLabel(int noteIndex, int rootIndex)
    {
        var semitones = (noteIndex - rootIndex + 12) % 12;
        return DegreeNames.TryGetValue(semitones, out var label) ? label : "?";
    }

    // ── Chord pairing ────────────────────────────────────────────────────────
    // Returns chords that commonly sound good alongside the given chord,
    // each with a short label explaining the relationship.
    public static IReadOnlyList<ChordPairing> GetPairedChords(NoteChord chord)
    {
        var root = NoteIndex(chord.Root);
        var pairings = new List<ChordPairing>();

        // Diatonic scale degrees relative to the chord root, using the major scale
        // Degrees: I ii iii IV V vi vii°
        // Intervals from root: 0  2  4   5  7  9  11
        int[] majorScale = [0, 2, 4, 5, 7, 9, 11];

        // For each diatonic position, the conventional quality
        (int semitones, string symbol, string relationship)[] diatonic =
        [
            (0,  "",   "Tonic (I)"),
            (2,  "m",  "Supertonic (ii)"),
            (4,  "m",  "Mediant (iii)"),
            (5,  "",   "Subdominant (IV)"),
            (7,  "",   "Dominant (V)"),
            (9,  "m",  "Submediant (vi)"),
            (11, "dim","Leading tone (vii°)"),
        ];

        var major7th = ChordTypes.First(c => c.Symbol == "maj7");
        var minor7th = ChordTypes.First(c => c.Symbol == "m7");
        var dom7th   = ChordTypes.First(c => c.Symbol == "7");
        var majorT   = ChordTypes.First(c => c.Symbol == "");
        var minorT   = ChordTypes.First(c => c.Symbol == "m");
        var dimT     = ChordTypes.First(c => c.Symbol == "dim");
        var sus4T    = ChordTypes.First(c => c.Symbol == "sus4");

        // Always skip the tonic itself (degree 0 = the selected chord)
        foreach (var (semitones, sym, rel) in diatonic)
        {
            if (semitones == 0) continue;

            var noteIdx  = (root + semitones) % 12;
            var noteName = ChromaticNotes[noteIdx];

            // Pick the chord type for this diatonic position
            ChordType ct = sym switch
            {
                "m"   => minorT,
                "dim" => dimT,
                _     => majorT,
            };

            pairings.Add(new ChordPairing(new NoteChord(noteName, ct), rel, PairingStrength.Resolved));
        }

        // Relative major / minor
        if (chord.Type.Symbol == "m")
        {
            // Relative major is 3 semitones up from minor root
            var relMajorNote = ChromaticNotes[(root + 3) % 12];
            pairings.Add(new ChordPairing(new NoteChord(relMajorNote, majorT), "Relative major", PairingStrength.Resolved));
        }
        else if (chord.Type.Symbol == "")
        {
            // Relative minor is 9 semitones up (= 3 down)
            var relMinorNote = ChromaticNotes[(root + 9) % 12];
            pairings.Add(new ChordPairing(new NoteChord(relMinorNote, minorT), "Relative minor", PairingStrength.Resolved));
        }

        // V7 — dominant 7th one perfect fifth above
        var v7Note = ChromaticNotes[(root + 7) % 12];
        pairings.Add(new ChordPairing(new NoteChord(v7Note, dom7th), "Dominant 7th (V7)", PairingStrength.Resolved));

        // IV sus4 — creates tension before resolving
        var ivNote = ChromaticNotes[(root + 5) % 12];
        pairings.Add(new ChordPairing(new NoteChord(ivNote, sus4T), "Suspended IV", PairingStrength.Suspended));

        // bVII — borrowed from the parallel minor (very common in rock)
        var bviiNote = ChromaticNotes[(root + 10) % 12];
        pairings.Add(new ChordPairing(new NoteChord(bviiNote, majorT), "Borrowed ♭VII", PairingStrength.Suspended));

        // bIII — borrowed from parallel minor
        var biiiNote = ChromaticNotes[(root + 3) % 12];
        pairings.Add(new ChordPairing(new NoteChord(biiiNote, majorT), "Borrowed ♭III", PairingStrength.Suspended));

        // Deduplicate by chord display name, preserving first occurrence
        return pairings
            .DistinctBy(p => p.Chord.DisplayName)
            .ToList();
    }

    // ── Key relationships on the circle of fifths ────────────────────────────
    // For a given root (CoF index), returns a dictionary keyed by CoF index
    // describing each note's diatonic role.  The 5 chromatic non-diatonic notes
    // are absent from the result.
    //
    // CoF adjacency encodes diatonic relationships directly:
    //   +1 step  = V  (dominant)
    //   -1 step  = IV (subdominant)
    //   +2 steps = II (supertonic minor)
    //   -2 steps = VI (submediant minor)
    //   +3 steps = III (mediant minor) — also relative minor of V
    //   -3 steps = VII (leading tone dim)
    //    0       = I  (tonic)

    // Maps each ChordMood to the closest modal association for display.
    public static readonly IReadOnlyDictionary<ChordMood, string> MoodModeLabels = new Dictionary<ChordMood, string>
    {
        [ChordMood.Bright]      = "Ionian",
        [ChordMood.Melancholic] = "Aeolian",
        [ChordMood.Tense]       = "Mixolydian",
        [ChordMood.Romantic]    = "Lydian",
        [ChordMood.Smooth]      = "Dorian",
        [ChordMood.Open]        = "Suspended",
        [ChordMood.Airy]        = "Phrygian",
        [ChordMood.Mysterious]  = "Whole Tone",
        [ChordMood.Dark]        = "Locrian",
        [ChordMood.Sinister]    = "Diminished",
    };

    // Mode holds the diatonic mode associated with that scale degree
    // (e.g. V = Mixolydian, ii = Dorian).
    public record NoteRole(string Roman, string Label, string Color, int Priority, string Mode = "");

    private static readonly NoteRole RoleI    = new("I",    "Home",      "#FFD700", 1, "Ionian");
    private static readonly NoteRole RoleV    = new("V",    "Tension",   "#FF6B35", 2, "Mixolydian");
    private static readonly NoteRole RoleIV   = new("IV",   "Lift",      "#4CAF50", 2, "Lydian");
    private static readonly NoteRole RoleII   = new("ii",   "Movement",  "#4A90D9", 3, "Dorian");
    private static readonly NoteRole RoleVI   = new("vi",   "Shadow",    "#9B59B6", 3, "Aeolian");
    private static readonly NoteRole RoleIII  = new("iii",  "Color",     "#26C6DA", 4, "Phrygian");
    private static readonly NoteRole RoleVII  = new("vii°", "Edge",      "#E84393", 4, "Locrian");

    public static IReadOnlyDictionary<int, NoteRole> GetKeyRelationships(int cofRootIndex)
    {
        return new Dictionary<int, NoteRole>
        {
            [(cofRootIndex + 0)  % 12] = RoleI,
            [(cofRootIndex + 1)  % 12] = RoleV,
            [(cofRootIndex + 11) % 12] = RoleIV,
            [(cofRootIndex + 2)  % 12] = RoleII,
            [(cofRootIndex + 10) % 12] = RoleVI,
            [(cofRootIndex + 3)  % 12] = RoleIII,
            [(cofRootIndex + 9)  % 12] = RoleVII,
        };
    }

    // Returns the 7 diatonic chords for a key rooted at cofRootIndex,
    // each paired with its scale-degree role and correct chord quality.
    public record DiatonicChord(NoteRole Role, NoteChord Chord, string ScaleDegreeDescription);

    public static IReadOnlyList<DiatonicChord> GetDiatonicChords(int cofRootIndex)
    {
        var major = ChordTypes.First(c => c.Symbol == "");
        var minor = ChordTypes.First(c => c.Symbol == "m");
        var dim   = ChordTypes.First(c => c.Symbol == "dim");

        // CoF offset, role, quality, plain-English description for a guitarist
        (int offset, NoteRole role, ChordType type, string desc)[] degrees =
        [
            (0,  RoleI,   major, "Home base — start and end here"),
            (11, RoleIV,  major, "Moves away from home, relaxed tension"),
            (1,  RoleV,   major, "Strongest pull back to I — use to resolve"),
            (2,  RoleII,  minor, "Leads naturally to V"),
            (10, RoleVI,  minor, "Relative minor — darker version of I"),
            (3,  RoleIII, minor, "Bridges I and IV smoothly"),
            (9,  RoleVII, dim,   "Maximum tension — resolves strongly to I"),
        ];

        return degrees.Select(d =>
        {
            var note  = CircleOfFifths[(cofRootIndex + d.offset) % 12];
            return new DiatonicChord(d.role, new NoteChord(note, d.type), d.desc);
        }).ToList();
    }

    // ── Harmonic Stability ───────────────────────────────────────────────────
    // Consonance score for a semitone interval (0 = most consonant, 1 = most dissonant).
    // Based on classical just-intonation ranking of intervals.
    private static readonly double[] IntervalConsonance =
    [
        1.00,  // 0  – unison
        0.17,  // 1  – minor 2nd   (very dissonant)
        0.33,  // 2  – major 2nd
        0.72,  // 3  – minor 3rd
        0.78,  // 4  – major 3rd
        0.85,  // 5  – perfect 4th
        0.10,  // 6  – tritone     (most dissonant)
        0.95,  // 7  – perfect 5th (most consonant after unison)
        0.70,  // 8  – minor 6th
        0.75,  // 9  – major 6th
        0.38,  // 10 – minor 7th
        0.25,  // 11 – major 7th
    ];

    /// <summary>
    /// Returns a consonance score in [0,1] for the interval between two semitone indices.
    /// 1.0 = perfectly consonant, 0.0 = maximally dissonant.
    /// </summary>
    public static double IntervalConsonanceScore(int noteIndexA, int noteIndexB)
    {
        var semitones = Math.Abs(noteIndexA - noteIndexB) % 12;
        // Fold intervals above a tritone down to their mirror (e.g. minor 7th = major 2nd inverted)
        return IntervalConsonance[semitones];
    }

    /// <summary>
    /// For each note in <paramref name="noteNames"/>, computes its average consonance
    /// with all other notes in the set. Returns a list of (NoteName, AverageConsonance)
    /// pairs in the same order as the input. Each value is in [0,1].
    /// </summary>
    public static IReadOnlyList<(string Note, double Consonance)> ComputeHarmonicStability(
        IReadOnlyList<string> noteNames)
    {
        var indices = noteNames.Select(NoteIndex).ToArray();
        var result  = new (string Note, double Consonance)[noteNames.Count];

        for (int i = 0; i < noteNames.Count; i++)
        {
            if (noteNames.Count == 1)
            {
                result[i] = (noteNames[i], 1.0);
                continue;
            }

            double sum = 0;
            int count  = 0;
            for (int j = 0; j < noteNames.Count; j++)
            {
                if (i == j) continue;
                sum += IntervalConsonanceScore(indices[i], indices[j]);
                count++;
            }
            result[i] = (noteNames[i], count > 0 ? sum / count : 1.0);
        }
        return result;
    }

    /// <summary>
    /// Overall harmonic stability of a chord (average of all pairwise consonance scores).
    /// Returns a value in [0,1] where 1 = perfectly consonant.
    /// </summary>
    public static double OverallHarmonicStability(IReadOnlyList<string> noteNames)
    {
        if (noteNames.Count < 2) return 1.0;
        var scores = ComputeHarmonicStability(noteNames);
        return scores.Average(s => s.Consonance);
    }
}
