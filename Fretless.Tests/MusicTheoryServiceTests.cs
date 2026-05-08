using Fretless.Web.Client;
using Fretless.Web.Client.Models;
using Xunit;

namespace Fretless.Tests;

public class MusicTheoryServiceTests
{
    // ── NoteIndex / TryNoteIndex ─────────────────────────────────────────────

    [Fact]
    public void NoteIndex_SharpAndFlatSpellings_ReturnSameIndex()
    {
        Assert.Equal(6, MusicTheoryService.NoteIndex("F#"));
        Assert.Equal(6, MusicTheoryService.NoteIndex("Gb"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("H")]
    [InlineData("Xb")]
    public void TryNoteIndex_UnrecognisedInput_ReturnsFalse(string? input)
    {
        var ok = MusicTheoryService.TryNoteIndex(input, out var index);
        Assert.False(ok);
        Assert.Equal(-1, index);
    }

    // ── ChromaticLabel ───────────────────────────────────────────────────────

    [Fact]
    public void ChromaticLabel_NegativeSemitone_WrapsAround()
    {
        Assert.Equal("B", MusicTheoryService.ChromaticLabel(-1));
    }

    [Fact]
    public void ChromaticLabel_OverTwelve_WrapsToCsharp()
    {
        Assert.Equal("C#", MusicTheoryService.ChromaticLabel(13));
    }

    [Fact]
    public void ChromaticLabel_OverTwelveWithFlats_WrapsToDb()
    {
        Assert.Equal("Db", MusicTheoryService.ChromaticLabel(13, useFlats: true));
    }

    [Theory]
    [InlineData(0, "C")]
    [InlineData(1, "C#")]
    [InlineData(6, "F#")]
    [InlineData(11, "B")]
    public void ChromaticLabel_InRangeSharp_MatchesChromaticNotes(int semitone, string expected)
    {
        Assert.Equal(expected, MusicTheoryService.ChromaticLabel(semitone));
        Assert.Equal(MusicTheoryService.ChromaticNotes[semitone], MusicTheoryService.ChromaticLabel(semitone));
    }

    [Theory]
    [InlineData(0, "C")]
    [InlineData(1, "Db")]
    [InlineData(3, "Eb")]
    [InlineData(8, "Ab")]
    public void ChromaticLabel_InRangeFlat_MatchesChromaticNotesFlat(int semitone, string expected)
    {
        Assert.Equal(expected, MusicTheoryService.ChromaticLabel(semitone, useFlats: true));
        Assert.Equal(
            MusicTheoryService.ChromaticNotesFlat[semitone],
            MusicTheoryService.ChromaticLabel(semitone, useFlats: true));
    }

    // ── NoteHue ──────────────────────────────────────────────────────────────

    [Fact]
    public void NoteHue_C_IsZeroDegrees()
    {
        Assert.Equal(0, MusicTheoryService.NoteHue(0));
    }

    [Fact]
    public void NoteHue_TwelfthStep_RoundsTripTo360()
    {
        Assert.Equal(360, MusicTheoryService.NoteHue(12));
    }

    // ── CofDistance ──────────────────────────────────────────────────────────

    [Fact]
    public void CofDistance_AdjacentOnCircle_IsOne()
    {
        // C and G are adjacent on the circle of fifths.
        Assert.Equal(1, MusicTheoryService.CofDistance("C", "G"));
    }

    [Fact]
    public void CofDistance_Tritone_IsSix()
    {
        // C and F# are diametrically opposed.
        Assert.Equal(6, MusicTheoryService.CofDistance("C", "F#"));
    }

    [Fact]
    public void CofDistance_SameNote_IsZero()
    {
        Assert.Equal(0, MusicTheoryService.CofDistance("C", "C"));
    }

    [Fact]
    public void CofDistance_UnknownNote_ReturnsSix()
    {
        Assert.Equal(6, MusicTheoryService.CofDistance("C", "Q"));
    }

    // ── IntervalConsonanceScore ──────────────────────────────────────────────

    [Fact]
    public void IntervalConsonanceScore_PerfectFifth_IsHighestNonUnison()
    {
        var fifth = MusicTheoryService.IntervalConsonanceScore(0, 7);

        for (int s = 1; s < 12; s++)
        {
            if (s == 7) continue;
            var other = MusicTheoryService.IntervalConsonanceScore(0, s);
            Assert.True(
                other < fifth,
                $"Expected semitone {s} ({other}) to be less consonant than perfect 5th ({fifth}).");
        }
    }

    [Fact]
    public void IntervalConsonanceScore_Tritone_IsLowest()
    {
        var tritone = MusicTheoryService.IntervalConsonanceScore(0, 6);

        for (int s = 0; s < 12; s++)
        {
            if (s == 6) continue;
            var other = MusicTheoryService.IntervalConsonanceScore(0, s);
            Assert.True(
                other > tritone,
                $"Expected semitone {s} ({other}) to be more consonant than tritone ({tritone}).");
        }
    }

    // ── OverallHarmonicStability ─────────────────────────────────────────────

    [Fact]
    public void OverallHarmonicStability_SingleNote_IsOne()
    {
        Assert.Equal(1.0, MusicTheoryService.OverallHarmonicStability(new[] { "C" }));
    }

    [Fact]
    public void OverallHarmonicStability_MajorTriad_IsMoreStableThanTritonePair()
    {
        var major = MusicTheoryService.OverallHarmonicStability(new[] { "C", "E", "G" });
        var tritone = MusicTheoryService.OverallHarmonicStability(new[] { "C", "F#" });
        Assert.True(major > tritone, $"C major ({major}) should be more stable than tritone ({tritone}).");
    }

    // ── RatioSegmentWeights / CofRatioWeights ────────────────────────────────

    [Fact]
    public void RatioSegmentWeights_ReturnsTwelveWeightsSummingToOne()
    {
        var weights = MusicTheoryService.RatioSegmentWeights();
        Assert.Equal(12, weights.Length);
        Assert.Equal(1.0, weights.Sum(), precision: 9);
    }

    [Fact]
    public void CofRatioWeights_ReturnsOneWeightPerCircleOfFifthsNote()
    {
        var weights = MusicTheoryService.CofRatioWeights();
        Assert.Equal(12, weights.Length);
        Assert.Equal(MusicTheoryService.CircleOfFifths.Length, weights.Length);
    }

    // ── Scales ───────────────────────────────────────────────────────────────

    [Fact]
    public void Scales_EveryScaleStartsWithRoot()
    {
        foreach (var scale in MusicTheoryService.Scales)
        {
            Assert.True(scale.Intervals.Length > 0, $"Scale {scale.Name} has no intervals.");
            Assert.Equal(0, scale.Intervals[0]);
        }
    }

    [Fact]
    public void Scales_AllIntervalsAreInRangeZeroToEleven()
    {
        foreach (var scale in MusicTheoryService.Scales)
        {
            foreach (var interval in scale.Intervals)
            {
                Assert.InRange(interval, 0, 11);
            }
        }
    }

    [Fact]
    public void Scales_AllIntervalsAreStrictlyAscendingAndUnique()
    {
        foreach (var scale in MusicTheoryService.Scales)
        {
            for (int i = 1; i < scale.Intervals.Length; i++)
            {
                Assert.True(
                    scale.Intervals[i] > scale.Intervals[i - 1],
                    $"Scale {scale.Name} intervals are not strictly ascending at position {i}.");
            }
        }
    }

    // ── GetDiatonicChords ────────────────────────────────────────────────────

    [Fact]
    public void GetDiatonicChords_CMajor_ReturnsSevenChords()
    {
        var chords = MusicTheoryService.GetDiatonicChords(0);
        Assert.Equal(7, chords.Count);
    }

    [Fact]
    public void GetDiatonicChords_CMajor_ContainsAllExpectedRomanNumerals()
    {
        var chords = MusicTheoryService.GetDiatonicChords(0);
        var romans = chords.Select(c => c.Role.Roman).ToHashSet();

        Assert.Contains("I", romans);
        Assert.Contains("IV", romans);
        Assert.Contains("V", romans);
        Assert.Contains("ii", romans);
        Assert.Contains("vi", romans);
        Assert.Contains("iii", romans);
        Assert.Contains("vii°", romans);
    }

    [Fact]
    public void GetDiatonicChords_CMajor_TonicIsCMajor()
    {
        var chords = MusicTheoryService.GetDiatonicChords(0);
        var tonic = chords.Single(c => c.Role.Roman == "I");

        Assert.Equal("C", tonic.Chord.Root);
        Assert.Equal("", tonic.Chord.Type.Symbol);
        Assert.Equal("Major", tonic.Chord.Type.Name);
    }

    [Fact]
    public void GetDiatonicChords_CMajor_DominantIsGMajor()
    {
        var chords = MusicTheoryService.GetDiatonicChords(0);
        var dominant = chords.Single(c => c.Role.Roman == "V");

        Assert.Equal("G", dominant.Chord.Root);
        Assert.Equal("", dominant.Chord.Type.Symbol);
    }

    [Fact]
    public void GetDiatonicChords_CMajor_LeadingToneIsDiminished()
    {
        var chords = MusicTheoryService.GetDiatonicChords(0);
        var vii = chords.Single(c => c.Role.Roman == "vii°");

        Assert.Equal("dim", vii.Chord.Type.Symbol);
    }

    // ── GetKeyRelationships ──────────────────────────────────────────────────

    [Fact]
    public void GetKeyRelationships_CMajor_HasSevenEntries()
    {
        var rels = MusicTheoryService.GetKeyRelationships(0);
        Assert.Equal(7, rels.Count);
    }

    [Fact]
    public void GetKeyRelationships_CMajor_ContainsTonicAtCofZero()
    {
        var rels = MusicTheoryService.GetKeyRelationships(0);
        Assert.True(rels.ContainsKey(0));
        Assert.Equal("I", rels[0].Roman);
    }

    [Fact]
    public void GetKeyRelationships_CMajor_DominantIsAtCofPlusOne()
    {
        var rels = MusicTheoryService.GetKeyRelationships(0);
        Assert.True(rels.ContainsKey(1));
        Assert.Equal("V", rels[1].Roman);
    }

    [Fact]
    public void GetKeyRelationships_CMajor_SubdominantIsAtCofMinusOne()
    {
        var rels = MusicTheoryService.GetKeyRelationships(0);
        // -1 wraps to 11
        Assert.True(rels.ContainsKey(11));
        Assert.Equal("IV", rels[11].Roman);
    }

    // ── GetPairedChords ──────────────────────────────────────────────────────

    [Fact]
    public void GetPairedChords_CMajor_IncludesV7OnG()
    {
        var major = MusicTheoryService.ChordTypes.First(t => t.Symbol == "");
        var c = new NoteChord("C", major);

        var pairings = MusicTheoryService.GetPairedChords(c);
        var v7 = pairings.Single(p => p.Relationship == "Dominant 7th (V7)");

        Assert.Equal("G", v7.Chord.Root);
        Assert.Equal("7", v7.Chord.Type.Symbol);
    }

    [Fact]
    public void GetPairedChords_CMinor_IncludesRelativeMajorAtMinorThirdAbove()
    {
        var minor = MusicTheoryService.ChordTypes.First(t => t.Symbol == "m");
        var cm = new NoteChord("C", minor);

        var pairings = MusicTheoryService.GetPairedChords(cm);
        var rel = pairings.Single(p => p.Relationship == "Relative major");

        // Relative major sits a minor 3rd (3 semitones) above the minor root.
        // GetPairedChords spells that note from MusicTheoryService.ChromaticNotes,
        // which uses sharp spellings — index 3 is "D#" rather than "Eb".
        Assert.Equal("D#", rel.Chord.Root);
        Assert.Equal("", rel.Chord.Type.Symbol);
    }

    [Fact]
    public void GetPairedChords_DeduplicatesByDisplayName()
    {
        var major = MusicTheoryService.ChordTypes.First(t => t.Symbol == "");
        var c = new NoteChord("C", major);

        var pairings = MusicTheoryService.GetPairedChords(c);
        var displayNames = pairings.Select(p => p.Chord.DisplayName).ToList();

        Assert.Equal(displayNames.Count, displayNames.Distinct().Count());
    }

    // ── ScaleDegreeLabel ─────────────────────────────────────────────────────

    [Fact]
    public void ScaleDegreeLabel_MajorThirdInCRoot_ReturnsThree()
    {
        Assert.Equal("3", MusicTheoryService.ScaleDegreeLabel(noteIndex: 4, rootIndex: 0));
    }

    [Fact]
    public void ScaleDegreeLabel_PerfectFifthInCRoot_ReturnsFive()
    {
        Assert.Equal("5", MusicTheoryService.ScaleDegreeLabel(noteIndex: 7, rootIndex: 0));
    }

    [Fact]
    public void ScaleDegreeLabel_NonScaleSemitoneInCRoot_ReturnsQuestionMark()
    {
        // Semitone 1 (C# / minor 2nd) is not in the DegreeNames map.
        Assert.Equal("?", MusicTheoryService.ScaleDegreeLabel(noteIndex: 1, rootIndex: 0));
    }

    // ── MoodColor / MoodModeLabels ───────────────────────────────────────────

    [Fact]
    public void MoodColor_EveryChordMoodHasAColor()
    {
        foreach (ChordMood mood in Enum.GetValues<ChordMood>())
        {
            var color = MusicTheoryService.MoodColor(mood);
            Assert.False(string.IsNullOrWhiteSpace(color), $"Mood {mood} has no color.");
        }
    }

    [Fact]
    public void MoodModeLabels_EveryChordMoodHasAModeLabel()
    {
        foreach (ChordMood mood in Enum.GetValues<ChordMood>())
        {
            Assert.True(
                MusicTheoryService.MoodModeLabels.ContainsKey(mood),
                $"Mood {mood} has no mode label.");
            Assert.False(
                string.IsNullOrWhiteSpace(MusicTheoryService.MoodModeLabels[mood]),
                $"Mood {mood} has empty mode label.");
        }
    }
}
