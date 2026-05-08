// Code-behind for Components/SpiralWheel.razor.
// This is a verbatim move of the previous @code body so future edits can navigate
// without scrolling through 500+ lines of markup; markup state is unchanged.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fretless.Web.Client.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Fretless.Web.Client.Components;

public partial class SpiralWheel
{
    private const double SpiralA = 34.0;
    private const double SpiralB = 38.0;
    private const double FreqZ = SpiralB / (2.0 * Math.PI);
    private const int SpiralTurns = 8;
    private const double SurfaceBandThickness = 10.0;
    private const double ZoomedArcThickness = 24.0;
    private const double GapArcLength = 6.0;
    private const double CenterHubRadius = SpiralA * 2.2;
    private const double CenterGapWidth = GapArcLength;
    private const double OvertoneLevelGap = GapArcLength;
    private const double FullyZoomedOutZoom = 0.44;
    private const double OvertoneLayerRevealEndZoom = 0.95;
    private const double SurfaceTransitionStartZoom = 0.95;
    private const double SurfaceTransitionEndZoom = 1.12;
    private const double MinDegreeLabelSize = 20.0;
    private const double MinSurfaceLabelSize = 28.0;
    private const double MinOvertoneLabelSize = 18.0;
    private const double MinCenterLabelSize = 36.0;
    private const int MinSubdivisionCount = 3;
    private const int MaxSubdivisionCount = 48;
    private const int SettingsVersion = 1;
    private const string SettingsStorageKey = "fretless.spiralWheel.settings.v1";

    // Keep a local alias so razor expressions stay terse; the underlying data lives in MusicTheoryService.
    private static string[] TwelveToneLabels => MusicTheoryService.ChromaticNotesFlat;

    private static readonly string[] AvailableKeys = MusicTheoryService.ChromaticNotesFlat;
    private static readonly string[] AvailableScaleNames =
    [
        "Major",
        "Natural Minor",
        "Major Pentatonic",
        "Minor Pentatonic",
        "Blues",
        "Dorian",
        "Mixolydian",
        "Lydian",
        "Phrygian",
        "Harmonic Minor",
        "Melodic Minor",
        "Locrian",
        "Chromatic"
    ];
    private static readonly MoodTarget[] AvailableMoodTargets = Enum.GetValues<MoodTarget>();
    private static readonly int[] StandardGuitarOpenSemitones = [4, 9, 2, 7, 11, 4];
    private static readonly int[] PianoWhiteSemitones = [0, 2, 4, 5, 7, 9, 11, 0, 2, 4, 5, 7, 9, 11];
    private static readonly (double X, int Semitone)[] PianoBlackKeys =
    [
        (26, 1),
        (48, 3),
        (92, 6),
        (114, 8),
        (136, 10),
        (180, 1),
        (202, 3),
        (246, 6),
        (268, 8),
        (290, 10),
    ];
    private const int OverlayFretCount = 12;
    private const int OverlayPositionSpanFrets = 4;
    private const int OverlayScaleInstanceSpanFrets = 5;
    private static readonly string[] ScaleInstanceColors =
    [
        "#ff6b6b",
        "#4dabf7",
        "#51cf66",
        "#ffd43b",
        "#cc5de8",
        "#20c997",
        "#ff922b",
        "#74c0fc"
    ];

    private record KeyIndicatorNote(string Label, bool IsRoot, bool IsSelected);

    /// <summary>Returns scale notes ordered so the root is in the center, others fan left and right.</summary>
    private IReadOnlyList<KeyIndicatorNote>? BuildKeyIndicatorNotes()
    {
        if (string.IsNullOrWhiteSpace(_selectedKey))
        {
            return null;
        }

        var keySemitone = KeyLabelToChromaticSemitone(_selectedKey);
        if (keySemitone < 0)
        {
            return null;
        }

        var scaleNotes = GetSelectedScaleNotes();
        // Order non-root notes by ascending interval from the root (1..11)
        IReadOnlyList<int> ascending = scaleNotes is not null && scaleNotes.Count > 0
            ? scaleNotes.Where(s => s != keySemitone)
                        .OrderBy(s => (s - keySemitone + 12) % 12)
                        .ToList()
            : Enumerable.Range(1, 11).Select(i => (keySemitone + i) % 12).ToList();

        // Split: first half goes RIGHT of root, second half (reversed) goes LEFT
        var half = ascending.Count / 2;
        var rightNotes = ascending.Take(ascending.Count - half).ToList();   // smaller or equal half → right
        var leftNotes  = ascending.Skip(ascending.Count - half).Reverse().ToList(); // larger half reversed → left

        var selectedSemitones = GetInstrumentOverlaySemitones();

        var ordered = leftNotes
            .Concat([keySemitone])
            .Concat(rightNotes);

        return ordered
            .Select(s => new KeyIndicatorNote(
                ChromaticSemitoneLabel(s),
                s == keySemitone,
                selectedSemitones.Contains(s)))
            .ToList();
    }

    private enum InstrumentType { None, Guitar, Piano }

    private List<InstrumentType> InstrumentSlots { get; } =
    [
        InstrumentType.Guitar,
        InstrumentType.Piano,
        InstrumentType.None
    ];

    private void SetInstrumentSlot(int slot, InstrumentType instrument)
    {
        InstrumentSlots[slot] = instrument;
        PersistSettings();
    }

    private TuningPreset _currentTuning = TuningService.Standard;
    private bool _showTuningSetup = false;
    private int? _openInstrumentSettingsSlot;
    private bool _showGuitarNoteNames = true;
    private bool _showGuitarFretNumbers;
    private bool _showPianoNoteNames = true;
    private bool _settingsLoaded;
    private int? _currentInputMidi;
    private double? _currentInputFrequency;
    private double? _currentInputLevel;
    private string? _connectedInstrumentName;
    private ConnectedInstrumentSignal? _connectedInstrumentSignal;
    private bool _waveformCanvasAttached;

    private string CurrentInputNoteLabel => _currentInputMidi is int midi
        ? ChromaticSemitoneLabel(midi % 12)
        : "Listening";

    private string CurrentInputDetail => _currentInputFrequency is double frequency
        ? $"{frequency:0.0} Hz"
        : "Connect and play a single clear note.";

    private string CurrentSignalStatus => _currentInputLevel.GetValueOrDefault() > 0.006
        ? "Signal detected"
        : "No signal";

    private void ToggleInstrumentSettings(int slot)
    {
        _openInstrumentSettingsSlot = _openInstrumentSettingsSlot == slot ? null : slot;
        PersistSettings();
    }

    private void ToggleTuningSetup()
    {
        _showTuningSetup = !_showTuningSetup;
        PersistSettings();
    }

    private void OnTuningChanged(TuningPreset tuning)
    {
        _currentTuning = tuning;
        PersistSettings();
    }

    private void OnTuningDone()
    {
        _showTuningSetup = false;
        PersistSettings();
    }

    private void OnShowGuitarNoteNamesChanged(bool show)
    {
        _showGuitarNoteNames = show;
        PersistSettings();
    }

    private void OnShowGuitarFretNumbersChanged(bool show)
    {
        _showGuitarFretNumbers = show;
        PersistSettings();
    }

    private void OnShowPianoNoteNamesChanged(bool show)
    {
        _showPianoNoteNames = show;
        PersistSettings();
    }

    private string _selectedKey = "";
    private string _selectedScale = "";
    private int? _activeScalePlaybackSemitone;
    private int _scalePlaybackVersion;
    private bool CanPlaySelectedScale => !string.IsNullOrWhiteSpace(_selectedScale);

    private string SelectedKey
    {
        get => _selectedKey;
        set
        {
            if (_selectedKey != value)
            {
                _selectedKey = value;
                _selectedOverlayScaleDegree = null;
                InvalidateScaleCache();
                _targetGraphRotationDegrees = CalculateGraphRotationDegrees();
                PersistSettings();
                _ = AnimateGraphRotationAsync();
            }
        }
    }

    private string SelectedScale
    {
        get => _selectedScale;
        set
        {
            if (_selectedScale != value)
            {
                _selectedScale = value;
                _selectedOverlayScaleDegree = null;
                InvalidateScaleCache();
                PersistSettings();
                StateHasChanged();
            }
        }
    }

    private int? _selectedOverlayScaleDegree;

    private HashSet<int>? GetSelectedScaleNotes()
    {
        if (_cachedScaleNotesValid
            && _cachedScaleNotesKey == _selectedKey
            && _cachedScaleNotesScale == _selectedScale
            && _cachedScaleNotesSubdivisionCount == SubdivisionCount)
        {
            return _cachedScaleNotes;
        }

        _cachedScaleNotes = ComputeSelectedScaleNotes();
        _cachedScaleNotesKey = _selectedKey;
        _cachedScaleNotesScale = _selectedScale;
        _cachedScaleNotesSubdivisionCount = SubdivisionCount;
        _cachedScaleNotesValid = true;
        return _cachedScaleNotes;
    }

    private HashSet<int>? ComputeSelectedScaleNotes()
    {
        if (string.IsNullOrEmpty(_selectedScale))
        {
            return null;
        }

        var keySemitone = string.IsNullOrEmpty(_selectedKey)
            ? 0
            : KeyLabelToChromaticSemitone(_selectedKey);
        if (keySemitone < 0)
        {
            return null;
        }

        var scale = CommonScales.FirstOrDefault(s => s.Name == _selectedScale);
        if (scale is null)
        {
            return null;
        }

        var scaleNotes = new HashSet<int>();
        
        foreach (var interval in scale.Intervals)
        {
            scaleNotes.Add((keySemitone + interval) % 12);
        }

        return scaleNotes;
    }

    private bool IsNoteOutsideSelectedScale(int subdivisionIndex)
    {
        if (string.IsNullOrEmpty(_selectedScale))
        {
            return false;
        }

        var scaleNotes = GetSelectedScaleNotes();
        if (scaleNotes is null)
        {
            return false;
        }

        var noteSemitone = ToChromaticSemitone(subdivisionIndex);
        return !scaleNotes.Contains(noteSemitone);
    }

    private bool TryBuildScaleDegreeLabels(out IReadOnlyList<ScaleDegreeLabel> labels)
    {
        labels = [];

        if (string.IsNullOrWhiteSpace(_selectedKey) || string.IsNullOrWhiteSpace(_selectedScale))
        {
            return false;
        }

        var keySemitone = KeyLabelToChromaticSemitone(_selectedKey);
        if (keySemitone < 0)
        {
            return false;
        }

        var scale = CommonScales.FirstOrDefault(s => s.Name == _selectedScale);
        if (scale is null || scale.Intervals.Length == 0)
        {
            return false;
        }

        var outerRadius = SpiralA + SpiralTurns * SpiralB + SpiralB / 2.0;
        var labelRadius = outerRadius + 16.0;
        var slot = 2.0 * Math.PI / SubdivisionCount;
        var degreeLabels = new List<ScaleDegreeLabel>(scale.Intervals.Length);

        for (var i = 0; i < scale.Intervals.Length; i++)
        {
            var semitone = (keySemitone + scale.Intervals[i]) % 12;
            var subdivisionIndex = ToSubdivisionFromSemitone(semitone);
            var midAngle = (subdivisionIndex + 0.5) * slot;
            var (x, y) = PolarPoint(midAngle, labelRadius);
            degreeLabels.Add(new ScaleDegreeLabel(x, y, RomanNumeral(i + 1)));
        }

        labels = degreeLabels;
        return true;
    }

    private static string RomanNumeral(int value) => value switch
    {
        1 => "i",
        2 => "ii",
        3 => "iii",
        4 => "iv",
        5 => "v",
        6 => "vi",
        7 => "vii",
        8 => "viii",
        9 => "ix",
        10 => "x",
        11 => "xi",
        12 => "xii",
        _ => value.ToString()
    };

    private bool TryGetSelectedScaleNoteLabels(out IReadOnlyList<string> notes)
    {
        notes = [];

        if (string.IsNullOrWhiteSpace(_selectedScale))
        {
            return false;
        }

        var keySemitone = string.IsNullOrWhiteSpace(_selectedKey)
            ? 0
            : KeyLabelToChromaticSemitone(_selectedKey);
        if (keySemitone < 0)
        {
            return false;
        }

        var scale = CommonScales.FirstOrDefault(s => s.Name == _selectedScale);
        if (scale is null || scale.Intervals.Length == 0)
        {
            return false;
        }

        notes = scale.Intervals
            .OrderBy(i => i)
            .Select(interval => ChromaticSemitoneLabel((keySemitone + interval) % 12))
            .ToList();
        return true;
    }

    private ElementReference _svgRef;
    private DotNetObjectReference<SpiralWheel>? _dotNetRef;
    private readonly HashSet<int> _midiActiveSemitones = [];
    private readonly HashSet<int> _activeInputMidiNotes = [];
    private double _zoom = FullyZoomedOutZoom;
    private double _targetZoom = FullyZoomedOutZoom;
    private double _panX;
    private double _panY;
    private bool _dragMoved;
    private bool _isPreviewing;
    private bool _jsZoomInitialized;
    private bool _isResizingRightPanel;
    private string? _rightPanelWidthCss;
    private double _graphRotationDegrees;
    private double _targetGraphRotationDegrees;
    private bool _isAnimatingGraphRotation;

    // Cached computed values — invalidated when key inputs change
    private IReadOnlyList<ScaleRecommendation>? _cachedScaleIdeas;
    private NoteChord? _cachedExplorationChord;
    private bool _cachedExplorationChordValid;
    private IReadOnlyList<ProgressionPath>? _cachedProgressionPaths;
    private HashSet<int>? _cachedScaleNotes;
    private bool _cachedScaleNotesValid;
    private int _cachedScaleNotesSubdivisionCount;
    private string _cachedScaleNotesKey = "";
    private string _cachedScaleNotesScale = "";

    private readonly List<SelectedNote> _selectedNotes = [];
    private MoodTarget SelectedMoodTarget { get; set; } = MoodTarget.Balanced;

    private int SubdivisionCount { get; set; } = 12;
    private IReadOnlyList<ScaleRecommendation> ScaleIdeas => _cachedScaleIdeas ??= BuildScaleRecommendations();
    private NoteChord? CurrentExplorationChord
    {
        get
        {
            if (!_cachedExplorationChordValid)
            {
                _cachedExplorationChord = BuildCurrentExplorationChord();
                _cachedExplorationChordValid = true;
            }
            return _cachedExplorationChord;
        }
    }
    private IReadOnlyList<ProgressionPath> ProgressionPaths => _cachedProgressionPaths ??= BuildProgressionPaths();

    private bool IsSurfaceView => _zoom < SurfaceTransitionStartZoom;
    private bool IsZoomedView => _zoom > SurfaceTransitionEndZoom;
    private int TotalSegments => SpiralTurns * SubdivisionCount;
    private int LabelStride => Math.Max(1, SubdivisionCount / 12);

    private double FirstRingInnerRadius => CenterHubRadius + CenterGapWidth;

    // Once JS takes ownership of the viewBox attribute, keep a stable frozen string so
    // Blazor re-renders never clobber the value that fretlessSpiralZoom is animating via rAF.
    private string? _frozenViewBox;
    private string _viewBox => _frozenViewBox
        ?? $"{_panX - 210 / _zoom} {_panY - 210 / _zoom} {420 / _zoom} {420 / _zoom}";

    private double SurfaceBlend => 1.0 - SmoothStep(
        SurfaceTransitionStartZoom,
        SurfaceTransitionEndZoom,
        _zoom);

    private double OvertoneLayerSeparatorBlend => SmoothStep(
        FullyZoomedOutZoom + 0.01,
        OvertoneLayerRevealEndZoom,
        _zoom);

    private string? RightPanelStyle => _rightPanelWidthCss is null
        ? null
        : $"--spiral-right-panel-width: {_rightPanelWidthCss};";

    private void BeginRightPanelResize(PointerEventArgs e)
    {
        _isResizingRightPanel = true;
        ResizeRightPanel(e);
    }

    private void ResizeRightPanel(PointerEventArgs e)
    {
        if (!_isResizingRightPanel)
        {
            return;
        }

        var dividerX = Math.Max(0, e.ClientX);
        _rightPanelWidthCss = $"clamp(22rem, calc(100vw - {dividerX.I()}px), 80vw)";
    }

    private void EndRightPanelResize(PointerEventArgs e)
    {
        if (!_isResizingRightPanel)
        {
            return;
        }

        ResizeRightPanel(e);
        _isResizingRightPanel = false;
    }

    protected override void OnInitialized()
    {
        var initialRotation = CalculateGraphRotationDegrees();
        _graphRotationDegrees = initialRotation;
        _targetGraphRotationDegrees = initialRotation;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await RestoreSettingsAsync();
            await JS.InvokeVoidAsync("fretlessSpiralZoom.init",
                _svgRef, _dotNetRef, _zoom, _panX, _panY);
            _jsZoomInitialized = true;
            // Freeze the viewBox binding so subsequent Blazor re-renders never overwrite
            // the attribute that fretlessSpiralZoom animates directly via requestAnimationFrame.
            _frozenViewBox = _viewBox;

            Midi.NoteOn  += OnMidiNoteOn;
            Midi.NoteOff += OnMidiNoteOff;
            AudioInput.NoteOn += OnAudioInputNoteOn;
            AudioInput.NoteOff += OnAudioInputNoteOff;
            AudioInput.PitchDetected += OnAudioInputPitchDetected;
            AudioInput.LevelDetected += OnAudioInputLevelDetected;
            InstrumentConnections.InstrumentConnected += OnInstrumentConnected;
            await Midi.InitAsync(JS);
            StateHasChanged();
        }

        if (!_waveformCanvasAttached && !string.IsNullOrWhiteSpace(_connectedInstrumentName))
        {
            await JS.InvokeVoidAsync("fretlessAudioInput.attachWaveformCanvas", _connectedInstrumentSignal?.WaveformCanvas);
            _waveformCanvasAttached = true;
        }
    }

    /// <summary>Called by JS when a smooth zoom animation settles to sync Blazor state.</summary>
    [JSInvokable]
    public void OnJsZoomSettled(double zoom, double panX, double panY)
    {
        _zoom = zoom;
        _panX = panX;
        _panY = panY;
        PersistSettings();
        // Trigger a single re-render now that zoom has changed (updates blend values, labels etc.)
        InvokeAsync(StateHasChanged);
    }

    private CancellationTokenSource? _persistDebounceCts;
    private const int PersistDebounceMs = 300;

    private void PersistSettings()
    {
        if (!_settingsLoaded) return;

        var newCts = new CancellationTokenSource();
        var previous = System.Threading.Interlocked.Exchange(ref _persistDebounceCts, newCts);
        previous?.Cancel();
        previous?.Dispose();

        _ = PersistSettingsDebouncedAsync(newCts.Token);
    }

    private async Task PersistSettingsDebouncedAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(PersistDebounceMs, token);
            await PersistSettingsAsync();
        }
        catch (TaskCanceledException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PersistSettingsAsync()
    {
        try
        {
            var settings = CaptureSettings();
            var json = JsonSerializer.Serialize(settings);
            await JS.InvokeVoidAsync("localStorage.setItem", SettingsStorageKey, json);
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private async Task RestoreSettingsAsync()
    {
        try
        {
            var json = await JS.InvokeAsync<string?>("localStorage.getItem", SettingsStorageKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var settings = JsonSerializer.Deserialize<SpiralWheelSettings>(json);
                ApplySettings(settings);
            }
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (JsonException)
        {
        }
        finally
        {
            _settingsLoaded = true;
        }
    }

    private SpiralWheelSettings CaptureSettings() => new(
        SettingsVersion,
        _selectedKey,
        _selectedScale,
        InstrumentSlots.Select(static i => i.ToString()).ToArray(),
        new TuningSettings(_currentTuning.Name, _currentTuning.MidiNotes),
        _showTuningSetup,
        _openInstrumentSettingsSlot,
        _showGuitarNoteNames,
        _showGuitarFretNumbers,
        _showPianoNoteNames,
        _selectedOverlayScaleDegree,
        SubdivisionCount,
        _selectedNotes.Select(static n => new SelectedNoteSettings(n.SubdivisionIndex, n.Cycle)).ToArray(),
        _zoom,
        _panX,
        _panY);

    private void ApplySettings(SpiralWheelSettings? settings)
    {
        if (settings is null || settings.Version != SettingsVersion)
        {
            return;
        }

        if (IsAvailableKey(settings.SelectedKey))
        {
            _selectedKey = settings.SelectedKey;
        }

        if (IsAvailableScale(settings.SelectedScale))
        {
            _selectedScale = settings.SelectedScale;
        }

        if (settings.InstrumentSlots is { Length: > 0 })
        {
            for (var i = 0; i < Math.Min(InstrumentSlots.Count, settings.InstrumentSlots.Length); i++)
            {
                if (Enum.TryParse<InstrumentType>(settings.InstrumentSlots[i], out var instrument))
                {
                    InstrumentSlots[i] = instrument;
                }
            }
        }

        if (settings.Tuning is { MidiNotes.Length: > 0 } tuning
            && tuning.MidiNotes.All(static midi => midi is >= TuningService.MinAudibleMidi and <= TuningService.MaxAudibleMidi))
        {
            var name = string.IsNullOrWhiteSpace(tuning.Name) ? "Custom" : tuning.Name;
            _currentTuning = new TuningPreset(name, [.. tuning.MidiNotes]);
        }

        _showTuningSetup = settings.ShowTuningSetup;
        _openInstrumentSettingsSlot = settings.OpenInstrumentSettingsSlot is >= 0 and < 3
            ? settings.OpenInstrumentSettingsSlot
            : null;
        _showGuitarNoteNames = settings.ShowGuitarNoteNames;
        _showGuitarFretNumbers = settings.ShowGuitarFretNumbers;
        _showPianoNoteNames = settings.ShowPianoNoteNames;
        SubdivisionCount = Math.Clamp(settings.SubdivisionCount, MinSubdivisionCount, MaxSubdivisionCount);
        _selectedOverlayScaleDegree = settings.SelectedOverlayScaleDegree is > 0
            ? settings.SelectedOverlayScaleDegree
            : null;

        _selectedNotes.Clear();
        if (settings.SelectedNotes is not null)
        {
            foreach (var note in settings.SelectedNotes)
            {
                if (note.SubdivisionIndex >= 0
                    && note.SubdivisionIndex < SubdivisionCount
                    && (note.Cycle is null || note.Cycle is >= 0 and < SpiralTurns))
                {
                    _selectedNotes.Add(new SelectedNote(note.SubdivisionIndex, note.Cycle));
                }
            }
        }

        _zoom = FullyZoomedOutZoom;
        _targetZoom = FullyZoomedOutZoom;
        _panX = 0;
        _panY = 0;

        InvalidateNoteCache();
        InvalidateScaleCache();
        SyncGraphRotationToCurrentState();
    }

    private static bool IsAvailableKey(string? key) => string.IsNullOrWhiteSpace(key) || AvailableKeys.Contains(key);

    private static bool IsAvailableScale(string? scale) => string.IsNullOrWhiteSpace(scale) || AvailableScaleNames.Contains(scale);

    private void InvalidateNoteCache()
    {
        _cachedScaleIdeas = null;
        _cachedExplorationChord = null;
        _cachedExplorationChordValid = false;
        _cachedProgressionPaths = null;
    }

    private void InvalidateScaleCache()
    {
        _cachedScaleNotes = null;
        _cachedScaleNotesValid = false;
    }

    private void OnMidiNoteOn(int midi)
    {
        _activeInputMidiNotes.Add(midi);
        _midiActiveSemitones.Add(midi % 12);
        InvokeAsync(StateHasChanged);
    }

    private bool IsSemitonePlaying(int semitone) =>
        _activeScalePlaybackSemitone == semitone || _midiActiveSemitones.Contains(semitone);

    private void OnMidiNoteOff(int midi)
    {
        _activeInputMidiNotes.Remove(midi);
        _midiActiveSemitones.Remove(midi % 12);
        InvokeAsync(StateHasChanged);
    }

    private void OnAudioInputNoteOn(int midi)
    {
        _currentInputMidi = midi;
        _activeInputMidiNotes.Add(midi);
        _midiActiveSemitones.Add(midi % 12);
        InvokeAsync(StateHasChanged);
    }

    private void OnAudioInputNoteOff(int midi)
    {
        _activeInputMidiNotes.Remove(midi);
        _midiActiveSemitones.Remove(midi % 12);
        _currentInputLevel = null;
        InvokeAsync(StateHasChanged);
    }

    private bool IsGuitarPositionPlaying(int openMidi, int fret, int semitone) =>
        _activeScalePlaybackSemitone == semitone || _activeInputMidiNotes.Contains(openMidi + fret);

    private void OnAudioInputPitchDetected(int midi, double frequency, double rms)
    {
        _currentInputMidi = midi;
        _currentInputFrequency = frequency;
        _currentInputLevel = rms;
        InvokeAsync(StateHasChanged);
    }

    private void OnAudioInputLevelDetected(double rms)
    {
        _currentInputLevel = rms;
        InvokeAsync(StateHasChanged);
    }

    private void OnInstrumentConnected(ConnectedInstrument connected)
    {
        _connectedInstrumentName = ConnectedInstrumentDisplayName(connected.Instrument);
        _waveformCanvasAttached = false;

        var requestedInstrument = string.Equals(connected.Instrument, "Voice", StringComparison.OrdinalIgnoreCase)
            ? InstrumentType.Guitar
            : Enum.TryParse<InstrumentType>(connected.Instrument, out var parsedInstrument)
                ? parsedInstrument
                : InstrumentType.None;

        if (requestedInstrument != InstrumentType.None)
        {
            AddInstrumentWidget(requestedInstrument);
        }

        InvokeAsync(StateHasChanged);
    }

    private static string ConnectedInstrumentDisplayName(string instrument) => instrument switch
    {
        "Guitar" => "Electric Guitar",
        _ => instrument
    };

    private void AddInstrumentWidget(InstrumentType instrument)
    {
        if (instrument == InstrumentType.None || InstrumentSlots.Contains(instrument))
        {
            return;
        }

        var emptySlot = InstrumentSlots.IndexOf(InstrumentType.None);
        if (emptySlot < 0)
        {
            return;
        }

        SetInstrumentSlot(emptySlot, instrument);
    }

    public async ValueTask DisposeAsync()
    {
        Midi.NoteOn  -= OnMidiNoteOn;
        Midi.NoteOff -= OnMidiNoteOff;
        AudioInput.NoteOn -= OnAudioInputNoteOn;
        AudioInput.NoteOff -= OnAudioInputNoteOff;
        AudioInput.PitchDetected -= OnAudioInputPitchDetected;
        AudioInput.LevelDetected -= OnAudioInputLevelDetected;
        InstrumentConnections.InstrumentConnected -= OnInstrumentConnected;

        // Cancel any pending debounced persist and flush a final write so the latest state lands in localStorage.
        var pending = System.Threading.Interlocked.Exchange(ref _persistDebounceCts, null);
        if (pending is not null)
        {
            pending.Cancel();
            pending.Dispose();
            if (_settingsLoaded)
            {
                await PersistSettingsAsync();
            }
        }

        if (_jsZoomInitialized)
        {
            await JS.InvokeVoidAsync("fretlessSpiralZoom.dispose");
            await JS.InvokeVoidAsync("fretlessSpiralRotation.dispose");
        }
        _dotNetRef?.Dispose();
    }

    private int KeySubdivisionIndex
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_selectedKey) || SubdivisionCount <= 0)
            {
                return -1;
            }

            var keySemitone = KeyLabelToChromaticSemitone(_selectedKey);
            if (keySemitone < 0)
            {
                return -1;
            }

            if (SubdivisionCount == 12)
            {
                return Array.IndexOf(TwelveToneLabels, _selectedKey);
            }

            return Enumerable.Range(0, SubdivisionCount)
                .OrderBy(i => CircularDistance(ToChromaticSemitone(i), keySemitone, 12))
                .ThenBy(i => i)
                .First();
        }
    }

    private double GraphRotationDegrees
    {
        get
        {
            if (SubdivisionCount <= 0)
            {
                return 0;
            }

            var keyIndex = KeySubdivisionIndex;
            if (keyIndex < 0)
            {
                return 0;
            }

            var sliceAngleDegrees = 360.0 / SubdivisionCount;
            return -(keyIndex + 0.5) * sliceAngleDegrees;
        }
    }

    private void ZoomIn() => SetZoom(_zoom * 1.25);

    private void ZoomOut() => SetZoom(_zoom / 1.25);

    private void ResetZoom()
    {
        _zoom = FullyZoomedOutZoom;
        _targetZoom = FullyZoomedOutZoom;
        _panX = 0;
        _panY = 0;
        if (_jsZoomInitialized)
        {
            _ = JS.InvokeVoidAsync("fretlessSpiralZoom.setTarget", _zoom, _panX, _panY);
        }
    }

    private void ShowSurface() => SetZoom(FullyZoomedOutZoom);

    private void ShowZoomedIn() => SetZoom(1.65);

    private async Task PreviewShapeShrinkAsync()
    {
        if (_isPreviewing)
        {
            return;
        }

        _isPreviewing = true;
        try
        {
            await AnimateZoomAsync(FullyZoomedOutZoom, 1.65, 700);
            await Task.Delay(450);
            await AnimateZoomAsync(1.65, FullyZoomedOutZoom, 700);
        }
        finally
        {
            _isPreviewing = false;
        }
    }

    private async Task AnimateZoomAsync(double from, double to, int durationMs)
    {
        // JS drives the per-frame viewBox update via rAF; we just kick it off and wait.
        _zoom = from;
        _panX = 0;
        _panY = 0;
        if (_jsZoomInitialized)
        {
            await JS.InvokeVoidAsync("fretlessSpiralZoom.init", _svgRef, _dotNetRef, from, 0.0, 0.0);
            await JS.InvokeVoidAsync("fretlessSpiralZoom.setTarget", to, 0.0, 0.0);
        }
        // Allow the rAF animation time to run (smoothing 0.30 converges in ~durationMs)
        await Task.Delay(durationMs + 100);
        _zoom = to;
    }

    private void IncreaseSubdivision()
    {
        SubdivisionCount = Math.Min(SubdivisionCount + 1, MaxSubdivisionCount);
        SyncGraphRotationToCurrentState();
    }

    private void DecreaseSubdivision()
    {
        SubdivisionCount = Math.Max(SubdivisionCount - 1, MinSubdivisionCount);
        SyncGraphRotationToCurrentState();
    }

    private void UpdateSubdivisionCountFromSelectedScale()
    {
        if (SubdivisionCount < MinSubdivisionCount || SubdivisionCount > MaxSubdivisionCount)
        {
            SubdivisionCount = Math.Clamp(SubdivisionCount, MinSubdivisionCount, MaxSubdivisionCount);
            _selectedNotes.RemoveAll(n => n.SubdivisionIndex >= SubdivisionCount);
            SyncGraphRotationToCurrentState();
        }
    }

    private void SyncGraphRotationToCurrentState()
    {
        var rotation = CalculateGraphRotationDegrees();
        _graphRotationDegrees = rotation;
        _targetGraphRotationDegrees = rotation;
    }

    private double CalculateGraphRotationDegrees()
    {
        if (SubdivisionCount <= 0)
        {
            return 0;
        }

        var keyIndex = KeySubdivisionIndex;
        if (keyIndex < 0)
        {
            return 0;
        }

        var sliceAngleDegrees = 360.0 / SubdivisionCount;
        return -(keyIndex + 0.5) * sliceAngleDegrees;
    }

    private async Task AnimateGraphRotationAsync()
    {
        if (_isAnimatingGraphRotation)
        {
            return;
        }

        _isAnimatingGraphRotation = true;
        const int frameMs = 16;
        const int durationMs = 280;

        try
        {
            while (true)
            {
                var start = _graphRotationDegrees;
                var target = _targetGraphRotationDegrees;
                var delta = ShortestAngularDelta(start, target);

                if (Math.Abs(delta) < 0.05)
                {
                    _graphRotationDegrees = NormalizeDegrees(target);
                    await InvokeAsync(StateHasChanged);
                    break;
                }

                var destination = start + delta;
                var frames = Math.Max(1, durationMs / frameMs);

                for (var frame = 1; frame <= frames; frame++)
                {
                    var progress = SmoothStep(0.0, 1.0, (double)frame / frames);
                    _graphRotationDegrees = Lerp(start, destination, progress);
                    await InvokeAsync(StateHasChanged);
                    await Task.Delay(frameMs);

                    if (Math.Abs(ShortestAngularDelta(_graphRotationDegrees, _targetGraphRotationDegrees)) > 0.1
                        && !AreAnglesNear(target, _targetGraphRotationDegrees))
                    {
                        break;
                    }
                }
            }
        }
        finally
        {
            _isAnimatingGraphRotation = false;
        }
    }

    private static bool AreAnglesNear(double left, double right)
    {
        return Math.Abs(ShortestAngularDelta(left, right)) < 0.05;
    }

    private static double ShortestAngularDelta(double fromDegrees, double toDegrees)
    {
        return ((toDegrees - fromDegrees + 540.0) % 360.0) - 180.0;
    }

    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360.0;
        if (normalized <= -180.0)
        {
            normalized += 360.0;
        }
        else if (normalized > 180.0)
        {
            normalized -= 360.0;
        }

        return normalized;
    }

    private void SetZoom(double zoom)
    {
        var oldZoom = _zoom;
        _zoom = Math.Clamp(zoom, FullyZoomedOutZoom, SurfaceTransitionEndZoom);
        _targetZoom = _zoom;
        _panX = _panX * oldZoom / _zoom;
        _panY = _panY * oldZoom / _zoom;
    }

    private bool ToggleNoteSelection(int subdivisionIndex, int? cycle)
    {
        if (_dragMoved)
        {
            return false;
        }

        var selectedNote = new SelectedNote(subdivisionIndex, cycle);

        if (cycle is null)
        {
            var removed = _selectedNotes.RemoveAll(n => n.SubdivisionIndex == subdivisionIndex) > 0;
            if (removed)
            {
                return false;
            }

            _selectedNotes.Add(selectedNote);
            return true;
        }

        var existingIndex = _selectedNotes.FindIndex(n => n.SubdivisionIndex == subdivisionIndex && n.Cycle == cycle);
        if (existingIndex >= 0)
        {
            _selectedNotes.RemoveAt(existingIndex);
            return false;
        }

        _selectedNotes.RemoveAll(n => n.SubdivisionIndex == subdivisionIndex && n.Cycle is null);
        _selectedNotes.Add(selectedNote);
        return true;
    }

    private async Task HandleNoteClickAsync(int subdivisionIndex, int? cycle)
    {
        if (_dragMoved)
        {
            return;
        }

        var isOutsideSelectedScale = IsNoteOutsideSelectedScale(subdivisionIndex);
        var alreadySelected = cycle is null
            ? IsSubdivisionSelected(subdivisionIndex)
            : IsSegmentSelected(subdivisionIndex, cycle.Value);

        if (isOutsideSelectedScale && !alreadySelected)
        {
            return;
        }

        var wasAdded = ToggleNoteSelection(subdivisionIndex, cycle);

        // Note list changed — invalidate recommendation caches and re-render all instruments
        InvalidateNoteCache();
        if (!wasAdded)
        {
            StateHasChanged();
            return;
        }

        var octave = cycle is int c
            ? Math.Clamp(c, -1, 10)
            : 2;
        var note = BuildAudioNoteName(subdivisionIndex, octave);
        await JS.InvokeVoidAsync("fretlessAudio.playOvertones", note, 20000);
    }

    private async Task PlaySelectedScaleAsync()
    {
        if (!TryGetSelectedScaleAudioNotes(out var notes)
            || !TryGetSelectedScalePlaybackSemitones(out var semitones))
        {
            return;
        }

        var playbackVersion = ++_scalePlaybackVersion;
        await JS.InvokeVoidAsync("fretlessAudio.playScale", (object)notes);

        try
        {
            for (var i = 0; i < semitones.Count; i++)
            {
                if (playbackVersion != _scalePlaybackVersion)
                {
                    return;
                }

                _activeScalePlaybackSemitone = semitones[i];
                await InvokeAsync(StateHasChanged);
                await Task.Delay(i == semitones.Count - 1 ? 450 : 320);
            }
        }
        finally
        {
            if (playbackVersion == _scalePlaybackVersion)
            {
                _activeScalePlaybackSemitone = null;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    private bool TryGetSelectedScaleAudioNotes(out IReadOnlyList<string> notes)
    {
        notes = [];

        if (string.IsNullOrWhiteSpace(_selectedScale))
        {
            return false;
        }

        var keySemitone = string.IsNullOrWhiteSpace(_selectedKey)
            ? 0
            : KeyLabelToChromaticSemitone(_selectedKey);
        if (keySemitone < 0)
        {
            return false;
        }

        var scale = CommonScales.FirstOrDefault(s => s.Name == _selectedScale);
        if (scale is null || scale.Intervals.Length == 0)
        {
            return false;
        }

        notes = scale.Intervals
            .OrderBy(i => i)
            .Select(interval => $"{ChromaticSemitoneLabel((keySemitone + interval) % 12)}4")
            .Append($"{ChromaticSemitoneLabel(keySemitone)}5")
            .ToList();
        return true;
    }

    private bool TryGetSelectedScalePlaybackSemitones(out IReadOnlyList<int> semitones)
    {
        semitones = [];

        if (string.IsNullOrWhiteSpace(_selectedScale))
        {
            return false;
        }

        var keySemitone = string.IsNullOrWhiteSpace(_selectedKey)
            ? 0
            : KeyLabelToChromaticSemitone(_selectedKey);
        if (keySemitone < 0)
        {
            return false;
        }

        var scale = CommonScales.FirstOrDefault(s => s.Name == _selectedScale);
        if (scale is null || scale.Intervals.Length == 0)
        {
            return false;
        }

        semitones = scale.Intervals
            .OrderBy(i => i)
            .Select(interval => (keySemitone + interval) % 12)
            .Append(keySemitone)
            .ToList();
        return true;
    }

    private string BuildAudioNoteName(int subdivisionIndex, int octave)
    {
        var semitone = ToChromaticSemitone(subdivisionIndex);
        var note = ChromaticSemitoneLabel(semitone);
        return $"{note}{octave}";
    }

    private bool IsSubdivisionSelected(int subdivisionIndex)
    {
        return _selectedNotes.Any(n => n.SubdivisionIndex == subdivisionIndex);
    }

    private bool IsSegmentSelected(int subdivisionIndex, int cycle)
    {
        return _selectedNotes.Any(n =>
            n.SubdivisionIndex == subdivisionIndex
            && (n.Cycle is null || n.Cycle == cycle));
    }

    private string SelectedNoteLabel(SelectedNote note)
    {
        return note.Cycle is int cycle
            ? SubdivisionLabel(note.SubdivisionIndex, cycle)
            : SubdivisionLabel(note.SubdivisionIndex);
    }

    private IReadOnlyList<int> SelectedSubdivisionIndices()
    {
        return _selectedNotes
            .Select(n => n.SubdivisionIndex)
            .Distinct()
            .ToList();
    }

    private IReadOnlyList<NotePoolProfile> BuildNotePoolProfiles()
    {
        if (_selectedNotes.Count == 0)
        {
            return [];
        }

        if (_selectedNotes.Count == 1)
        {
            return
            [
                new NotePoolProfile(
                    SelectedNoteLabel(_selectedNotes[0]),
                    "1:1",
                    1.0,
                    1.0,
                    "#ffffff")
            ];
        }

        var harmonyScores = new List<double>(_selectedNotes.Count);

        foreach (var note in _selectedNotes)
        {
            var otherNotes = _selectedNotes.Where(n => n != note).ToList();
            var avgHarmony = otherNotes.Average(other => HarmonyScore(note.SubdivisionIndex, other.SubdivisionIndex));
            harmonyScores.Add(Math.Clamp(avgHarmony, 0.01, 0.99));
        }

        var profiles = new List<NotePoolProfile>(_selectedNotes.Count);

        for (var i = 0; i < _selectedNotes.Count; i++)
        {
            var harmony = harmonyScores[i];
            var dissonance = 1.0 - harmony;
            var ratio = BuildHarmonyDissonanceRatio(harmony, dissonance);
            var fontSizeRem = 0.8 + harmony * 1.05;
            var visualOpacity = 0.55 + harmony * 0.45;
            var accentColor = HarmonyAccentColor(harmony);
            profiles.Add(new NotePoolProfile(
                SelectedNoteLabel(_selectedNotes[i]),
                ratio,
                fontSizeRem,
                visualOpacity,
                accentColor));
        }

        return profiles;
    }

    private static string HarmonyAccentColor(double harmony)
    {
        var clamped = Math.Clamp(harmony, 0.0, 1.0);
        var contrastAdjusted = Math.Clamp(Math.Pow(clamped, 0.82), 0.0, 1.0);
        return HeatmapColor(contrastAdjusted);
    }

    private static string HeatmapColor(double value)
    {
        var clamped = Math.Clamp(value, 0.0, 1.0);
        var palette = new (double Stop, int R, int G, int B)[]
        {
            (0.00, 139, 0, 0),
            (0.20, 212, 43, 26),
            (0.40, 255, 140, 0),
            (0.60, 255, 215, 0),
            (0.80, 84, 196, 66),
            (1.00, 15, 148, 56)
        };

        for (var i = 0; i < palette.Length - 1; i++)
        {
            var from = palette[i];
            var to = palette[i + 1];
            if (clamped <= to.Stop)
            {
                var t = (clamped - from.Stop) / (to.Stop - from.Stop);
                var r = (int)Math.Round(Lerp(from.R, to.R, t));
                var g = (int)Math.Round(Lerp(from.G, to.G, t));
                var b = (int)Math.Round(Lerp(from.B, to.B, t));
                return $"rgb({r},{g},{b})";
            }
        }

        var final = palette[^1];
        return $"rgb({final.R},{final.G},{final.B})";
    }

    private DiadAnalysis BuildDiadAnalysis(SelectedNote left, SelectedNote right)
    {
        var leftChromatic = ToChromaticIndex(left.SubdivisionIndex);
        var rightChromatic = ToChromaticIndex(right.SubdivisionIndex);
        var intervalSemitones = CircularDistance(leftChromatic, rightChromatic, 12);
        var fifthDistance = CircularDistance(left.SubdivisionIndex, right.SubdivisionIndex, SubdivisionCount);
        var harmony = HarmonyScore(left.SubdivisionIndex, right.SubdivisionIndex);
        var dissonance = 1.0 - harmony;
        var (leftLinePercent, rightLinePercent) = BuildDiadLinePercentages(intervalSemitones);
        var harmonyPercent = $"{(harmony * 100.0).I()}%";
        var dissonancePercent = $"{(dissonance * 100.0).I()}%";
        var ratio = BuildHarmonyDissonanceRatio(harmony, dissonance);
        var (moodLabel, moodColor) = DiadMoodFromRatio(harmony, dissonance);

        return new DiadAnalysis(
            SelectedNoteLabel(left),
            SelectedNoteLabel(right),
            intervalSemitones,
            fifthDistance,
            harmonyPercent,
            dissonancePercent,
            ratio,
            leftLinePercent,
            rightLinePercent,
            NoteAccentColor(left.SubdivisionIndex),
            NoteAccentColor(right.SubdivisionIndex),
            moodLabel,
            moodColor);
    }

    private static (string Label, string Color) DiadMoodFromRatio(double harmony, double dissonance)
    {
        var ratio = harmony / Math.Max(0.0001, dissonance);

        if (ratio >= 3.5) return ("Resonant", "#66e38f");
        if (ratio >= 2.0) return ("Warm", "#9be46a");
        if (ratio >= 1.1) return ("Balanced", "#e7d66a");
        if (ratio >= 0.75) return ("Tense", "#f2a45f");
        return ("Dissonant", "#f06a6a");
    }

    private static (double LeftPercent, double RightPercent) BuildDiadLinePercentages(int intervalSemitones)
    {
        var safeInterval = Math.Clamp(intervalSemitones, 0, MusicTheoryService.SemitoneRatios.Length - 1);
        var ratioLabel = MusicTheoryService.SemitoneRatios[safeInterval].Label;

        var leftUnits = 1.0;
        var rightUnits = 1.0;

        if (ratioLabel.Contains('/'))
        {
            var parts = ratioLabel.Split('/');
            if (parts.Length == 2
                && double.TryParse(parts[0], out var numerator)
                && double.TryParse(parts[1], out var denominator)
                && numerator > 0
                && denominator > 0)
            {
                leftUnits = denominator;
                rightUnits = numerator;
            }
        }

        var total = leftUnits + rightUnits;
        if (total <= 0)
        {
            return (50, 50);
        }

        return (leftUnits / total * 100.0, rightUnits / total * 100.0);
    }

    private TriadAnalysis BuildTriadAnalysis(IReadOnlyList<SelectedNote> triadNotes)
    {
        var a = triadNotes[0].SubdivisionIndex;
        var b = triadNotes[1].SubdivisionIndex;
        var c = triadNotes[2].SubdivisionIndex;

        var abHarmony = HarmonyScore(a, b);
        var bcHarmony = HarmonyScore(b, c);
        var caHarmony = HarmonyScore(c, a);
        var avgHarmony = Math.Clamp((abHarmony + bcHarmony + caHarmony) / 3.0, 0.0, 1.0);
        var avgDissonance = 1.0 - avgHarmony;

        var pairRatios = string.Join(" · ",
            BuildHarmonyDissonanceRatio(abHarmony, 1.0 - abHarmony),
            BuildHarmonyDissonanceRatio(bcHarmony, 1.0 - bcHarmony),
            BuildHarmonyDissonanceRatio(caHarmony, 1.0 - caHarmony));

        var (qualityLabel, qualityColor) = TriadQualityFromNotes(a, b, c);

        return new TriadAnalysis(
            $"{SelectedNoteLabel(triadNotes[0])} + {SelectedNoteLabel(triadNotes[1])} + {SelectedNoteLabel(triadNotes[2])}",
            $"{(avgHarmony * 100.0).I()}%",
            $"{(avgDissonance * 100.0).I()}%",
            pairRatios,
            qualityLabel,
            qualityColor);
    }

    private (string Label, string Color) TriadQualityFromNotes(int a, int b, int c)
    {
        var chromatic = new[]
        {
            ToChromaticIndex(a),
            ToChromaticIndex(b),
            ToChromaticIndex(c)
        }
        .Distinct()
        .ToArray();

        if (chromatic.Length != 3)
        {
            return ("Cluster", "#f06a6a");
        }

        foreach (var root in chromatic)
        {
            var intervals = chromatic
                .Select(n => (n - root + 12) % 12)
                .OrderBy(i => i)
                .ToArray();

            if (intervals.SequenceEqual([0, 4, 7])) return ("Major", "#78d86f");
            if (intervals.SequenceEqual([0, 3, 7])) return ("Minor", "#95d06f");
            if (intervals.SequenceEqual([0, 3, 6])) return ("Diminished", "#f2a45f");
            if (intervals.SequenceEqual([0, 4, 8])) return ("Augmented", "#f58f6a");
            if (intervals.SequenceEqual([0, 2, 7])) return ("Suspended 2", "#6ecfd9");
            if (intervals.SequenceEqual([0, 5, 7])) return ("Suspended 4", "#7eb8e8");
        }

        return ("Complex", "#e7d66a");
    }

    private string NoteAccentColor(int subdivisionIndex)
    {
        var hue = 360.0 * (((subdivisionIndex % SubdivisionCount) + SubdivisionCount) % SubdivisionCount) / SubdivisionCount;
        return $"hsl({hue.I()},78%,68%)";
    }

    private double HarmonyScore(int leftIndex, int rightIndex)
    {
        var leftChromatic = ToChromaticIndex(leftIndex);
        var rightChromatic = ToChromaticIndex(rightIndex);
        var semitoneDistance = CircularDistance(leftChromatic, rightChromatic, 12);
        return semitoneDistance switch
        {
            0 => 0.95,
            1 => 0.12,
            2 => 0.36,
            3 => 0.62,
            4 => 0.8,
            5 => 0.74,
            6 => 0.2,
            _ => 0.5
        };
    }

    private static int KeyLabelToChromaticSemitone(string key) =>
        MusicTheoryService.TryNoteIndex(key, out var index) ? index : -1;

    private static string ChromaticSemitoneLabel(int semitone) =>
        MusicTheoryService.ChromaticLabel(semitone, useFlats: true);

    private HashSet<int> GetInstrumentOverlaySemitones()
    {
        var scaleNotes = GetSelectedScaleNotes();
        if (scaleNotes is not null && scaleNotes.Count > 0)
        {
            return scaleNotes;
        }

        if (_selectedNotes.Count == 0)
        {
            return [];
        }

        return _selectedNotes
            .Select(n => ToChromaticSemitone(n.SubdivisionIndex))
            .ToHashSet();
    }

    private int GetOverlayRootSemitone()
    {
        if (!string.IsNullOrWhiteSpace(_selectedKey) && !string.IsNullOrWhiteSpace(_selectedScale))
        {
            return KeyLabelToChromaticSemitone(_selectedKey);
        }

        return -1;
    }

    private bool TryGetSelectedScaleDegrees(out IReadOnlyList<OverlayScalePosition> degrees)
    {
        degrees = [];

        if (string.IsNullOrWhiteSpace(_selectedKey) || string.IsNullOrWhiteSpace(_selectedScale))
        {
            return false;
        }

        var keySemitone = KeyLabelToChromaticSemitone(_selectedKey);
        if (keySemitone < 0)
        {
            return false;
        }

        var scale = CommonScales.FirstOrDefault(s => s.Name == _selectedScale);
        if (scale is null || scale.Intervals.Length == 0)
        {
            return false;
        }

        // Build positions based on where the root note appears on low E and A strings
        var positions = new List<OverlayScalePosition>();
        var tuning = _currentTuning.MidiNotes;
        
        // Check low E string (string 0) and A string (string 1) for root notes
        for (var stringIndex = 0; stringIndex < Math.Min(2, tuning.Length); stringIndex++)
        {
            var openMidi = tuning[stringIndex];
            var openSemitone = openMidi % 12;

            // Find all frets where the root appears on this string up to fret 12
            for (var fret = 0; fret <= OverlayFretCount; fret++)
            {
                var fretSemitone = (openSemitone + fret) % 12;
                
                if (fretSemitone == keySemitone)
                {
                    // Found a root note - build a position around it
                    // Position spans 5 frets with root in middle-ish area
                    var startFret = Math.Max(0, fret - 1);
                    var endFret = Math.Min(OverlayFretCount, startFret + 4);
                    
                    // Adjust if we're at the start or end of the fretboard
                    if (endFret == OverlayFretCount && startFret > 0)
                    {
                        startFret = Math.Max(0, OverlayFretCount - 4);
                    }
                    
                    // Avoid duplicate overlapping positions
                    var existingPosition = positions.FirstOrDefault(p => 
                        Math.Abs(p.StartFret - startFret) <= 2);
                    
                    if (existingPosition is null)
                    {
                        var degree = positions.Count + 1;
                        positions.Add(new OverlayScalePosition(degree, keySemitone, startFret, endFret));
                    }
                }
            }
        }

        // If we found fewer than 5 positions (common for pentatonics and short scales),
        // fill in gaps by adding positions at strategic fret markers: 3, 5, 7, 9, 12
        var strategicFrets = new[] { 3, 5, 7, 9, 12 };
        foreach (var anchorFret in strategicFrets)
        {
            if (positions.Count >= 7) break; // Don't overcrowd
            
            var startFret = Math.Max(0, anchorFret - 2);
            var endFret = Math.Min(OverlayFretCount, startFret + 4);
            
            // Only add if no nearby position exists
            var hasNearby = positions.Any(p => 
                (p.StartFret <= anchorFret && p.EndFret >= anchorFret) ||
                Math.Abs(p.StartFret - startFret) <= 2);
            
            if (!hasNearby && anchorFret <= OverlayFretCount)
            {
                var degree = positions.Count + 1;
                positions.Add(new OverlayScalePosition(degree, keySemitone, startFret, endFret));
            }
        }

        // Sort by starting fret
        positions = positions.OrderBy(p => p.StartFret).ToList();
        
        // Renumber degrees sequentially
        degrees = positions.Select((p, index) => 
            new OverlayScalePosition(index + 1, p.Semitone, p.StartFret, p.EndFret))
            .ToList();

        return degrees.Count > 0;
    }

    private void SelectOverlayScalePosition(int? degree)
    {
        _selectedOverlayScaleDegree = degree;
    }

    private bool TryGetSelectedOverlayPositionRange(out int startFret, out int endFret)
    {
        startFret = 0;
        endFret = OverlayFretCount;

        if (_selectedOverlayScaleDegree is not int selectedDegree)
        {
            return false;
        }

        if (!TryGetSelectedScaleDegrees(out var positions))
        {
            return false;
        }

        var selected = positions.FirstOrDefault(p => p.Degree == selectedDegree);
        if (selected is null)
        {
            return false;
        }

        startFret = selected.StartFret;
        endFret = selected.EndFret;
        return true;
    }

    private static int FindFirstFretForSemitone(int openStringSemitone, int targetSemitone)
    {
        var diff = (targetSemitone - openStringSemitone + 12) % 12;
        return diff;
    }

    private bool TryGetSelectedScaleStepPattern(out IReadOnlyList<string> stepPattern)
    {
        stepPattern = [];

        if (string.IsNullOrWhiteSpace(_selectedScale))
        {
            return false;
        }

        var scale = CommonScales.FirstOrDefault(s => s.Name == _selectedScale);
        if (scale is null || scale.Intervals.Length == 0)
        {
            return false;
        }

        var intervals = scale.Intervals
            .OrderBy(i => i)
            .ToArray();

        var steps = new List<string>(intervals.Length);
        for (var i = 0; i < intervals.Length; i++)
        {
            var current = intervals[i];
            var next = i == intervals.Length - 1 ? intervals[0] + 12 : intervals[i + 1];
            var semitoneStep = next - current;
            steps.Add(SemitoneStepLabel(semitoneStep));
        }

        stepPattern = steps;
        return true;
    }

    private static string SemitoneStepLabel(int semitones) => semitones switch
    {
        1 => "h",
        2 => "W",
        3 => "W+h",
        4 => "W+W",
        _ => $"{semitones}h"
    };

    private sealed record OverlayScalePosition(int Degree, int Semitone, int StartFret, int EndFret);

    private sealed record ScaleInstanceNote(double X, double Y, int Semitone);

    private sealed record ScaleInstance(int StartFret, int EndFret, string Color, IReadOnlyList<ScaleInstanceNote> Notes);

    private bool TryGetSelectedScaleInstances(IReadOnlyList<int> tuningStrings, out IReadOnlyList<ScaleInstance> instances)
    {
        instances = [];

        var scaleNotes = GetSelectedScaleNotes();
        if (scaleNotes is null || scaleNotes.Count == 0 || tuningStrings.Count == 0)
        {
            return false;
        }

        var result = new List<ScaleInstance>();
        var maxStart = Math.Max(0, OverlayFretCount - OverlayScaleInstanceSpanFrets + 1);

        for (var startFret = 0; startFret <= maxStart; startFret++)
        {
            var endFret = Math.Min(OverlayFretCount, startFret + OverlayScaleInstanceSpanFrets - 1);
            var notes = BuildScaleInstanceNotes(tuningStrings, scaleNotes, startFret, endFret);

            if (scaleNotes.All(scaleNote => notes.Any(note => note.Semitone == scaleNote)))
            {
                var color = ScaleInstanceColors[result.Count % ScaleInstanceColors.Length];
                result.Add(new ScaleInstance(startFret, endFret, color, notes));
            }
        }

        instances = result;
        return instances.Count > 0;
    }

    private static IReadOnlyList<ScaleInstanceNote> BuildScaleInstanceNotes(
        IReadOnlyList<int> tuningStrings,
        IReadOnlySet<int> scaleNotes,
        int startFret,
        int endFret)
    {
        var notes = new List<ScaleInstanceNote>();

        for (var stringIndex = 0; stringIndex < tuningStrings.Count; stringIndex++)
        {
            var y = 22 + (tuningStrings.Count - 1 - stringIndex) * 16;
            var openSemitone = tuningStrings[stringIndex] % 12;

            for (var fret = startFret; fret <= endFret; fret++)
            {
                var semitone = (openSemitone + fret) % 12;
                if (!scaleNotes.Contains(semitone))
                {
                    continue;
                }

                var x = fret == 0 ? 10 : 20 + (fret - 0.5) * 24;
                notes.Add(new ScaleInstanceNote(x, y, semitone));
            }
        }

        return notes;
    }

    private IReadOnlyList<NextMoveRecommendation> BuildNextMoveRecommendations()
    {
        if (_selectedNotes.Count == 0)
        {
            return [];
        }

        var selectedIndices = SelectedSubdivisionIndices();
        var targetHarmony = MoodTargetHarmonyCenter(SelectedMoodTarget);
        var currentHarmony = AggregateHarmony(selectedIndices);
        var currentDistance = Math.Abs(currentHarmony - targetHarmony);
        var recommendations = new List<NextMoveRecommendation>();

        for (var candidate = 0; candidate < SubdivisionCount; candidate++)
        {
            if (selectedIndices.Contains(candidate))
            {
                continue;
            }

            var candidateSelection = selectedIndices.Append(candidate).ToArray();
            var candidateHarmony = AggregateHarmony(candidateSelection);
            var candidateDistance = Math.Abs(candidateHarmony - targetHarmony);
            var progress = Math.Clamp(currentDistance - candidateDistance, -1.0, 1.0);
            var fit = 1.0 - Math.Clamp(candidateDistance, 0.0, 1.0);
            var score = fit * 0.7 + Math.Max(0.0, progress) * 0.3;

            recommendations.Add(new NextMoveRecommendation(
                candidate,
                SubdivisionLabel(candidate),
                MoodLabelFromHarmony(candidateHarmony),
                $"{(progress * 100.0).I()}%",
                score));
        }

        return recommendations
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.NoteLabel)
            .Take(3)
            .ToList();
    }

    private IReadOnlyList<ScaleRecommendation> BuildScaleRecommendations()
    {
        if (_selectedNotes.Count == 0)
        {
            return [];
        }

        var selectedSemitones = _selectedNotes
            .Select(n => ToChromaticSemitone(n.SubdivisionIndex))
            .Distinct()
            .ToHashSet();

        var selectedKeySemitone = KeySubdivisionIndex >= 0
            ? ToChromaticSemitone(KeySubdivisionIndex)
            : -1;

        var candidates = new List<ScaleRecommendation>();

        foreach (var root in Enumerable.Range(0, 12))
        {
            foreach (var scale in CommonScales)
            {
                var scaleSemitones = scale.Intervals
                    .Select(interval => (root + interval) % 12)
                    .ToHashSet();

                if (!selectedSemitones.IsSubsetOf(scaleSemitones))
                {
                    continue;
                }

                var missingCount = scaleSemitones.Count - selectedSemitones.Count;
                var rootBias = selectedKeySemitone == root ? 0 : 1;
                var score = missingCount * 10 + rootBias;

                var missingNotes = scaleSemitones
                    .Except(selectedSemitones)
                    .OrderBy(n => n)
                    .Select(ChromaticSemitoneLabel)
                    .ToArray();

                var summary = missingNotes.Length == 0
                    ? "Complete match"
                    : $"Missing {string.Join(", ", missingNotes)}";

                candidates.Add(new ScaleRecommendation(
                    $"{ChromaticSemitoneLabel(root)} {scale.Name}",
                    summary,
                    score));
            }
        }

        return candidates
            .OrderBy(c => c.Score)
            .ThenBy(c => c.Label)
            .Take(4)
            .ToList();
    }

    private NoteChord? BuildCurrentExplorationChord()
    {
        var selectedSemitones = _selectedNotes
            .Select(n => ToChromaticSemitone(n.SubdivisionIndex))
            .Distinct()
            .ToHashSet();

        if (selectedSemitones.Count < 3)
        {
            return null;
        }

        var triadTypes = MusicTheoryService.ChordTypes
            .Where(t => t.Intervals.Length == 3)
            .ToArray();

        foreach (var root in Enumerable.Range(0, 12))
        {
            foreach (var triadType in triadTypes)
            {
                var chordNotes = triadType.Intervals
                    .Select(interval => (root + interval) % 12)
                    .ToHashSet();

                if (!chordNotes.IsSubsetOf(selectedSemitones))
                {
                    continue;
                }

                return new NoteChord(ChromaticSemitoneLabel(root), triadType);
            }
        }

        return null;
    }

    private IReadOnlyList<ProgressionPath> BuildProgressionPaths()
    {
        var current = CurrentExplorationChord;
        if (current is null)
        {
            return [];
        }

        var pairings = MusicTheoryService.GetPairedChords(current)
            .Select(pairing => new ProgressionMove(
                pairing.Chord,
                pairing.Relationship,
                MusicTheoryService.CofDistance(NormalizeToCofNote(current.Root), NormalizeToCofNote(pairing.Chord.Root)),
                pairing.Strength))
            .ToList();

        var safeMoves = pairings
            .Where(p => p.Strength == PairingStrength.Resolved && p.CofDistance <= 2)
            .OrderBy(p => p.CofDistance)
            .ThenBy(p => p.Chord.DisplayName)
            .Take(3)
            .ToList();

        var colorMoves = pairings
            .Where(p => p.Strength == PairingStrength.Suspended && p.CofDistance <= 2)
            .OrderBy(p => p.CofDistance)
            .ThenBy(p => p.Chord.DisplayName)
            .Take(3)
            .ToList();

        var adventurousMoves = pairings
            .Where(p => p.CofDistance >= 3)
            .OrderByDescending(p => p.CofDistance)
            .ThenBy(p => p.Chord.DisplayName)
            .Take(3)
            .ToList();

        var paths = new List<ProgressionPath>();

        if (safeMoves.Count > 0)
        {
            paths.Add(new ProgressionPath("Safe resolution", safeMoves));
        }

        if (colorMoves.Count > 0)
        {
            paths.Add(new ProgressionPath("Color and tension", colorMoves));
        }

        if (adventurousMoves.Count > 0)
        {
            paths.Add(new ProgressionPath("Adventure branch", adventurousMoves));
        }

        return paths;
    }

    private async Task ApplyProgressionMoveAsync(ProgressionMove move)
    {
        _selectedNotes.Clear();
        foreach (var semitone in move.Chord.AbsoluteNotes.Distinct())
        {
            _selectedNotes.Add(new SelectedNote(ToSubdivisionFromSemitone(semitone), null));
        }

        var notes = move.Chord.AbsoluteNotes
            .Select(n => $"{MusicTheoryService.NoteAtIndex(n)}4")
            .ToArray();
        await JS.InvokeVoidAsync("fretlessAudio.playChord", (object)notes);
    }

    private int ToSubdivisionFromSemitone(int semitone)
    {
        if (SubdivisionCount == 12)
        {
            for (var index = 0; index < 12; index++)
            {
                if (ToChromaticSemitone(index) == semitone)
                {
                    return index;
                }
            }

            return 0;
        }

        var nearest = Enumerable.Range(0, SubdivisionCount)
            .OrderBy(i => CircularDistance(ToChromaticSemitone(i), semitone, 12))
            .First();

        return nearest;
    }

    private static string NormalizeToCofNote(string note) => note switch
    {
        "C#" => "Db",
        "D#" => "Eb",
        "G#" => "Ab",
        "A#" => "Bb",
        _ => note
    };

    private double AggregateHarmony(IReadOnlyList<int> notes)
    {
        if (notes.Count <= 1)
        {
            return 0.5;
        }

        var total = 0.0;
        var pairs = 0;

        for (var i = 0; i < notes.Count; i++)
        {
            for (var j = i + 1; j < notes.Count; j++)
            {
                total += HarmonyScore(notes[i], notes[j]);
                pairs++;
            }
        }

        return pairs == 0 ? 0.5 : total / pairs;
    }

    private static string MoodLabelFromHarmony(double harmony)
    {
        var dissonance = 1.0 - harmony;
        return DiadMoodFromRatio(harmony, dissonance).Label;
    }

    private static double MoodTargetHarmonyCenter(MoodTarget target) => target switch
    {
        MoodTarget.Resonant => 0.88,
        MoodTarget.Warm => 0.74,
        MoodTarget.Balanced => 0.56,
        MoodTarget.Tense => 0.38,
        MoodTarget.Dissonant => 0.2,
        _ => 0.56
    };

    private static string MoodTargetLabel(MoodTarget target) => target switch
    {
        MoodTarget.Resonant => "Resonant",
        MoodTarget.Warm => "Warm",
        MoodTarget.Balanced => "Balanced",
        MoodTarget.Tense => "Tense",
        MoodTarget.Dissonant => "Dissonant",
        _ => "Balanced"
    };

    private int ToChromaticIndex(int subdivisionIndex)
    {
        if (SubdivisionCount == 12)
        {
            return ((subdivisionIndex % 12) + 12) % 12;
        }

        var mapped = (int)Math.Round(subdivisionIndex * 12.0 / SubdivisionCount);
        return ((mapped % 12) + 12) % 12;
    }

    private int ToChromaticSemitone(int subdivisionIndex)
    {
        if (SubdivisionCount != 12)
        {
            return ToChromaticIndex(subdivisionIndex);
        }

        var note = TwelveToneLabels[((subdivisionIndex % 12) + 12) % 12];
        return note switch
        {
            "C" => 0,
            "Db" => 1,
            "D" => 2,
            "Eb" => 3,
            "E" => 4,
            "F" => 5,
            "F#" => 6,
            "G" => 7,
            "Ab" => 8,
            "A" => 9,
            "Bb" => 10,
            "B" => 11,
            _ => 0
        };
    }

    /// <summary>Maps a chromatic semitone (0–11) back to the nearest subdivision index.</summary>
    private int SemitoneToSubdivisionIndex(int semitone)
    {
        var normalized = ((semitone % 12) + 12) % 12;
        if (SubdivisionCount == 12)
        {
            return normalized;
        }

        // Find the subdivision whose chromatic mapping is closest
        var best = 0;
        var bestDist = int.MaxValue;
        for (var i = 0; i < SubdivisionCount; i++)
        {
            var mapped = ToChromaticSemitone(i);
            var dist = Math.Abs(mapped - normalized);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }
        return best;
    }

    private static int CircularDistance(int leftIndex, int rightIndex, int divisions)
    {
        var safeDivisions = Math.Max(1, divisions);
        var left = ((leftIndex % safeDivisions) + safeDivisions) % safeDivisions;
        var right = ((rightIndex % safeDivisions) + safeDivisions) % safeDivisions;
        var diff = Math.Abs(left - right);
        return Math.Min(diff, safeDivisions - diff);
    }

    private static string BuildHarmonyDissonanceRatio(double harmony, double dissonance)
    {
        var harmonyUnits = (int)Math.Round(harmony * 100.0);
        var dissonanceUnits = (int)Math.Round(dissonance * 100.0);

        if (dissonanceUnits == 0)
        {
            return $"{harmonyUnits}:0";
        }

        var gcd = GreatestCommonDivisor(harmonyUnits, dissonanceUnits);
        return $"{harmonyUnits / gcd}:{dissonanceUnits / gcd}";
    }

    private static int GreatestCommonDivisor(int a, int b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return Math.Max(1, a);
    }

    // Render-time geometry caches. Keys cover the inputs that actually change the path-string output.
    // BuildSolidSurfaceSlices depends only on SubdivisionCount + scale-coloring inputs (selectedScale, scaleNotes).
    // BuildSegments depends additionally on zoom and layerSeparatorBlend (we bucket those to avoid cache thrash).
    private IReadOnlyList<SpiralSegment>? _cachedSegments;
    private (int Subdivisions, int ZoomBucket, int BlendBucket, string Key, string Scale) _segmentsKey;
    private IReadOnlyList<SolidSurfaceSlice>? _cachedSurfaceSlices;
    private (int Subdivisions, string Key, string Scale) _slicesKey;

    private static int Bucket(double v, double precision) => (int)Math.Round(v / precision);

    private IReadOnlyList<SpiralSegment> BuildSegments(double collapseProgress, double layerSeparatorBlend)
    {
        var key = (
            SubdivisionCount,
            Bucket(_zoom, 0.01),
            Bucket(layerSeparatorBlend, 0.02),
            _selectedKey,
            _selectedScale);

        if (_cachedSegments is not null && _segmentsKey == key)
        {
            return _cachedSegments;
        }

        var result = new List<SpiralSegment>(TotalSegments);
        var slot = 2.0 * Math.PI / SubdivisionCount;
        var halfGap = GapArcLength / 2.0;
        var minSpiralGap = 1.0 / _zoom;
        var levelGap = Lerp(minSpiralGap, OvertoneLevelGap, layerSeparatorBlend);
        var ringBandThickness = SpiralB - levelGap;
        var baseInnerRadius = FirstRingInnerRadius;
        var segmentIndex = 0;

        for (var cycle = 0; cycle < SpiralTurns; cycle++)
        {
            // Reversed: cycle 0 (fundamental/lowest) is outermost; highest overtones are innermost.
            var innerRadius = baseInnerRadius + (SpiralTurns - 1 - cycle) * SpiralB;
            var outerRadius = innerRadius + ringBandThickness;

            for (var subdivisionIndex = 0; subdivisionIndex < SubdivisionCount; subdivisionIndex++)
            {
                var startDivider = subdivisionIndex * slot;
                var endDivider = (subdivisionIndex + 1) * slot;
                var midAngle = (startDivider + endDivider) / 2.0;
                var labelRadius = (innerRadius + outerRadius) / 2.0;
                var (midX, midY) = PolarPoint(midAngle, labelRadius);
                var label = SubdivisionLabel(subdivisionIndex, cycle);
                var color = SubdivisionColor(subdivisionIndex);
                var wedgePath = AnnularWedgePath(startDivider, endDivider, innerRadius, outerRadius, halfGap);

                result.Add(new SpiralSegment(
                    subdivisionIndex,
                    cycle,
                    segmentIndex++,
                    label,
                    color,
                    wedgePath,
                    wedgePath,
                    midX,
                    midY));
            }
        }

        _cachedSegments = result;
        _segmentsKey = key;
        return result;
    }

    private string SubdivisionLabel(int subdivisionIndex, int? cycle = null)
    {
        var root = SubdivisionCount == 12
            ? TwelveToneLabels[((subdivisionIndex % 12) + 12) % 12]
            : $"S{subdivisionIndex + 1}";

        return cycle is null ? root : $"{root}{cycle.Value + 1}";
    }

    private string SubdivisionRootLabel(int subdivisionIndex, int? cycle = null)
    {
        var root = SubdivisionCount == 12
            ? TwelveToneLabels[((subdivisionIndex % 12) + 12) % 12]
            : $"S{subdivisionIndex + 1}";

        return cycle is null ? root : $"{root}{cycle.Value + 1}";
    }

    private string SubdivisionColor(int subdivisionIndex)
    {
        var scaleNotes = GetSelectedScaleNotes();

        if (!string.IsNullOrEmpty(_selectedScale) && scaleNotes != null)
        {
            var noteSemitone = ToChromaticSemitone(subdivisionIndex);
            if (scaleNotes.Contains(noteSemitone))
            {
                return "#ffffff";
            }

            return "#4a4a4a";
        }

        return "#ffffff";
    }

    private IReadOnlyList<SolidSurfaceSlice> BuildSolidSurfaceSlices()
    {
        var key = (SubdivisionCount, _selectedKey, _selectedScale);
        if (_cachedSurfaceSlices is not null && _slicesKey == key)
        {
            return _cachedSurfaceSlices;
        }

        var outerRadius = SpiralA + SpiralTurns * SpiralB + SpiralB / 2.0;
        var innerRadius = FirstRingInnerRadius;
        var slot = 2.0 * Math.PI / SubdivisionCount;
        var halfGap = GapArcLength / 2.0;
        var result = new List<SolidSurfaceSlice>(SubdivisionCount);

        for (var subdivisionIndex = 0; subdivisionIndex < SubdivisionCount; subdivisionIndex++)
        {
            result.Add(new SolidSurfaceSlice(
                subdivisionIndex,
                SubdivisionColor(subdivisionIndex),
                AnnularWedgePath(subdivisionIndex * slot, (subdivisionIndex + 1) * slot, innerRadius, outerRadius, halfGap)));
        }

        _cachedSurfaceSlices = result;
        _slicesKey = key;
        return result;
    }

    private void InvalidateGeometryCache()
    {
        _cachedSegments = null;
        _cachedSurfaceSlices = null;
    }

    private IEnumerable<string> BuildSurfaceSliceDividers()
    {
        var outerRadius = SpiralA + SpiralTurns * SpiralB + SpiralB / 2.0;
        var slot = 2.0 * Math.PI / SubdivisionCount;

        for (var subdivisionIndex = 0; subdivisionIndex < SubdivisionCount; subdivisionIndex++)
        {
            yield return RadialLinePath(subdivisionIndex * slot, 0.0, outerRadius);
        }
    }

    private static string AnnularWedgePath(double startDivider, double endDivider, double innerRadius, double outerRadius, double halfGap = 0.0)
    {
        // asin(halfGap / r) gives the angular inset at radius r whose perpendicular
        // distance from the divider equals halfGap — must be computed per-radius so the
        // gap width is constant rather than tapering with r.
        var outerInset = outerRadius > 0 ? Math.Asin(Math.Clamp(halfGap / outerRadius, -1.0, 1.0)) : 0.0;
        var innerInset = innerRadius > 0 ? Math.Asin(Math.Clamp(halfGap / innerRadius, -1.0, 1.0)) : 0.0;

        var startOuter = startDivider + outerInset;
        var endOuter   = endDivider   - outerInset;
        var startInner = startDivider + innerInset;
        var endInner   = endDivider   - innerInset;

        var (outerStartX, outerStartY) = PolarPoint(startOuter, outerRadius);
        var (outerEndX,   outerEndY)   = PolarPoint(endOuter,   outerRadius);
        var largeArc = endOuter - startOuter > Math.PI ? 1 : 0;

        if (innerRadius <= 0)
        {
            return $"M {outerStartX.I()} {outerStartY.I()} A {outerRadius.I()} {outerRadius.I()} 0 {largeArc} 1 {outerEndX.I()} {outerEndY.I()} L 0 0 Z";
        }

        var (innerEndX,   innerEndY)   = PolarPoint(endInner,   innerRadius);
        var (innerStartX, innerStartY) = PolarPoint(startInner, innerRadius);

        return $"M {outerStartX.I()} {outerStartY.I()} A {outerRadius.I()} {outerRadius.I()} 0 {largeArc} 1 {outerEndX.I()} {outerEndY.I()} L {innerEndX.I()} {innerEndY.I()} A {innerRadius.I()} {innerRadius.I()} 0 {largeArc} 0 {innerStartX.I()} {innerStartY.I()} Z";
    }

    private static string RadialLinePath(double theta, double innerRadius, double outerRadius)
    {
        var (innerX, innerY) = PolarPoint(theta, innerRadius);
        var (outerX, outerY) = PolarPoint(theta, outerRadius);

        return $"M {innerX.I()} {innerY.I()} L {outerX.I()} {outerY.I()}";
    }

    private IEnumerable<SurfaceLabel> BuildSurfaceLabels(IReadOnlyList<SpiralSegment> segments)
    {
        var outerRadius = SpiralA + SpiralTurns * SpiralB + SpiralB / 2.0;
        // Sector centroid ≈ 2R·sin(α)/(3α) where α = half-angle; for 12 equal slices this ≈ 0.66R.
        var labelRadius = outerRadius * 0.62;
        var slot = 2.0 * Math.PI / SubdivisionCount;

        for (var subdivisionIndex = 0; subdivisionIndex < SubdivisionCount; subdivisionIndex++)
        {
            var midAngle = (subdivisionIndex + 0.5) * slot;
            var (x, y) = PolarPoint(midAngle, labelRadius);
            yield return new SurfaceLabel(subdivisionIndex, x, y, SubdivisionLabel(subdivisionIndex));
        }
    }

    private (double X, double Y) SpiralPoint(double theta, double radialOffset = 0.0)
    {
        var (hx, hy, hz) = HelixPoint3D(theta, radialOffset);
        return Project(hx, hy, hz);
    }

    private static (double X, double Y, double Z) HelixPoint3D(double theta, double radialOffset = 0.0)
    {
        var radius = SpiralA + SpiralB * (theta / (2.0 * Math.PI)) + radialOffset;
        return (Math.Sin(theta) * radius, -Math.Cos(theta) * radius, FreqZ * theta);
    }

    private static (double Tx, double Ty, double Tz) HelixTangent3D(double theta, double radialOffset = 0.0)
    {
        var radius = SpiralA + SpiralB * (theta / (2.0 * Math.PI)) + radialOffset;
        var dr = SpiralB / (2.0 * Math.PI);
        var tx = Math.Cos(theta) * radius + Math.Sin(theta) * dr;
        var ty = Math.Sin(theta) * radius - Math.Cos(theta) * dr;
        var tz = FreqZ;
        var length = Math.Sqrt(tx * tx + ty * ty + tz * tz);
        return (tx / length, ty / length, tz / length);
    }

    private static (double X, double Y) Project(double helixX, double helixY, double helixZ) => (helixX, helixY);

    private string BezierArcPath(double tStart, double tEnd, double radialOffset = 0.0)
    {
        var (p0x, p0y) = SpiralPoint(tStart, radialOffset);
        var (p3x, p3y) = SpiralPoint(tEnd, radialOffset);
        var chord = Math.Sqrt((p3x - p0x) * (p3x - p0x) + (p3y - p0y) * (p3y - p0y));
        var k = chord / 3.0;

        var (t0hx, t0hy, t0hz) = HelixTangent3D(tStart, radialOffset);
        var (t1hx, t1hy, t1hz) = HelixTangent3D(tEnd, radialOffset);
        var (t0sx, t0sy) = Project(t0hx, t0hy, t0hz);
        var (t1sx, t1sy) = Project(t1hx, t1hy, t1hz);
        Normalise(ref t0sx, ref t0sy);
        Normalise(ref t1sx, ref t1sy);

        var c1x = p0x + k * t0sx;
        var c1y = p0y + k * t0sy;
        var c2x = p3x - k * t1sx;
        var c2y = p3y - k * t1sy;

        return $"M {p0x.I()} {p0y.I()} C {c1x.I()} {c1y.I()} {c2x.I()} {c2y.I()} {p3x.I()} {p3y.I()}";
    }

    private string InsetRoundedCellPath(double t0, double t1, double halfBand, double cornerRoundness)
    {
        var left = Math.Min(t0, t1);
        var right = Math.Max(t0, t1);
        var top = halfBand;
        var bottom = -halfBand;
        var width = right - left;
        var roundness = Math.Clamp(cornerRoundness, 0.0, 1.0);
        var thetaRadius = Math.Min(width * 0.42, 2.0 * Math.PI / SubdivisionCount * 0.18) * roundness;
        var offsetRadius = Math.Min(halfBand * 0.35, thetaRadius) * roundness;
        var edgeSteps = Math.Max(5, (int)Math.Ceiling(width / (2.0 * Math.PI) * SubdivisionCount * 3.0));
        const int cornerSteps = 5;
        var points = new List<(double X, double Y)>(edgeSteps * 2 + cornerSteps * 4 + 8);

        AddThetaEdge(points, left + thetaRadius, right - thetaRadius, top, edgeSteps);
        AddTopRightCorner(points, right, top, thetaRadius, offsetRadius, cornerSteps);
        AddOffsetEdge(points, right, top - offsetRadius, bottom + offsetRadius, cornerSteps);
        AddBottomRightCorner(points, right, bottom, thetaRadius, offsetRadius, cornerSteps);
        AddThetaEdge(points, right - thetaRadius, left + thetaRadius, bottom, edgeSteps);
        AddBottomLeftCorner(points, left, bottom, thetaRadius, offsetRadius, cornerSteps);
        AddOffsetEdge(points, left, bottom + offsetRadius, top - offsetRadius, cornerSteps);
        AddTopLeftCorner(points, left, top, thetaRadius, offsetRadius, cornerSteps);

        return PolygonPath(points);
    }

    private void AddThetaEdge(List<(double X, double Y)> points, double fromTheta, double toTheta, double offset, int steps)
    {
        for (var step = 0; step <= steps; step++)
        {
            var theta = Lerp(fromTheta, toTheta, (double)step / steps);
            points.Add(OffsetPoint(theta, offset));
        }
    }

    private void AddOffsetEdge(List<(double X, double Y)> points, double theta, double fromOffset, double toOffset, int steps)
    {
        for (var step = 1; step <= steps; step++)
        {
            var offset = Lerp(fromOffset, toOffset, (double)step / steps);
            points.Add(OffsetPoint(theta, offset));
        }
    }

    private void AddTopRightCorner(List<(double X, double Y)> points, double right, double top, double thetaRadius, double offsetRadius, int steps)
    {
        for (var step = 1; step <= steps; step++)
        {
            var angle = Math.PI * 0.5 * step / steps;
            points.Add(OffsetPoint(
                right - thetaRadius + thetaRadius * Math.Sin(angle),
                top - offsetRadius + offsetRadius * Math.Cos(angle)));
        }
    }

    private void AddBottomRightCorner(List<(double X, double Y)> points, double right, double bottom, double thetaRadius, double offsetRadius, int steps)
    {
        for (var step = 1; step <= steps; step++)
        {
            var angle = Math.PI * 0.5 * step / steps;
            points.Add(OffsetPoint(
                right - thetaRadius + thetaRadius * Math.Cos(angle),
                bottom + offsetRadius - offsetRadius * Math.Sin(angle)));
        }
    }

    private void AddBottomLeftCorner(List<(double X, double Y)> points, double left, double bottom, double thetaRadius, double offsetRadius, int steps)
    {
        for (var step = 1; step <= steps; step++)
        {
            var angle = Math.PI * 0.5 * step / steps;
            points.Add(OffsetPoint(
                left + thetaRadius - thetaRadius * Math.Sin(angle),
                bottom + offsetRadius - offsetRadius * Math.Cos(angle)));
        }
    }

    private void AddTopLeftCorner(List<(double X, double Y)> points, double left, double top, double thetaRadius, double offsetRadius, int steps)
    {
        for (var step = 1; step <= steps; step++)
        {
            var angle = Math.PI * 0.5 * step / steps;
            points.Add(OffsetPoint(
                left + thetaRadius - thetaRadius * Math.Cos(angle),
                top - offsetRadius + offsetRadius * Math.Sin(angle)));
        }
    }

    private (double X, double Y) OffsetPoint(double theta, double offset)
    {
        var frame = GetSpiralFrame(theta);
        return (frame.X + frame.Nx * offset, frame.Y + frame.Ny * offset);
    }

    private static (double X, double Y) PolarPoint(double theta, double radius) =>
        (Math.Sin(theta) * radius, -Math.Cos(theta) * radius);

    private static (double X, double Y) RotatePoint(double x, double y, double degrees)
    {
        var radians = degrees * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return (x * cos - y * sin, x * sin + y * cos);
    }

    private SpiralGeometryFrame GetSpiralFrame(double theta)
    {
        var (x, y) = SpiralPoint(theta);
        var (thx, thy, thz) = HelixTangent3D(theta);
        var (tx, ty) = Project(thx, thy, thz);
        Normalise(ref tx, ref ty);

        var nx = -ty;
        var ny = tx;
        if (nx * x + ny * y < 0)
        {
            nx = -nx;
            ny = -ny;
        }

        return new SpiralGeometryFrame(x, y, tx, ty, nx, ny);
    }

    private static string PolygonPath(IReadOnlyList<(double X, double Y)> points)
    {
        if (points.Count == 0)
        {
            return string.Empty;
        }

        var path = new System.Text.StringBuilder($"M {points[0].X.I()} {points[0].Y.I()}");
        for (var i = 1; i < points.Count; i++)
        {
            path.Append($" L {points[i].X.I()} {points[i].Y.I()}");
        }

        path.Append(" Z");
        return path.ToString();
    }

    private static void Normalise(ref double x, ref double y)
    {
        var length = Math.Sqrt(x * x + y * y);
        if (length < 1e-9)
        {
            x = 1;
            y = 0;
            return;
        }

        x /= length;
        y /= length;
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        if (edge0 >= edge1)
        {
            return value >= edge1 ? 1.0 : 0.0;
        }

        var t = Math.Clamp((value - edge0) / (edge1 - edge0), 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }

    private static double Lerp(double start, double end, double progress) => start + (end - start) * progress;

    private static MarkupString SvgText(double x, double y, double fontSize, string style, string? content) =>
        new($"<text x=\"{x.I()}\" y=\"{y.I()}\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-size=\"{fontSize.I()}\" style=\"{style}\">{HtmlEncoder.Default.Encode(content ?? string.Empty)}</text>");

    // Counter-rotates text so it stays upright when its parent group is rotated.
    private static MarkupString SvgTextUpright(double x, double y, double fontSize, string style, string? content, double counterRotationDegrees) =>
        new($"<text x=\"{x.I()}\" y=\"{y.I()}\" text-anchor=\"middle\" dominant-baseline=\"middle\" font-size=\"{fontSize.I()}\" style=\"{style}\" transform=\"rotate({counterRotationDegrees.I()},{x.I()},{y.I()})\">{HtmlEncoder.Default.Encode(content ?? string.Empty)}</text>");

    private double ReadableWheelLabelSize(double baseSize, double minimumSize) =>
        Math.Max(baseSize / _zoom, minimumSize);

    private sealed record SpiralSegment(
        int SubdivisionIndex,
        int Cycle,
        int SegmentIndex,
        string Label,
        string Color,
        string ArcPath,
        string MorphPath,
        double MidX,
        double MidY);

    private sealed record SolidSurfaceSlice(int SubdivisionIndex, string Color, string Path);

    private sealed record SurfaceLabel(int SubdivisionIndex, double X, double Y, string Text);

    private sealed record ScaleDegreeLabel(double X, double Y, string Text);

    private sealed record SelectedNote(int SubdivisionIndex, int? Cycle);

    private sealed record SpiralWheelSettings(
        int Version,
        string SelectedKey,
        string SelectedScale,
        string[] InstrumentSlots,
        TuningSettings Tuning,
        bool ShowTuningSetup,
        int? OpenInstrumentSettingsSlot,
        bool ShowGuitarNoteNames,
        bool ShowGuitarFretNumbers,
        bool ShowPianoNoteNames,
        int? SelectedOverlayScaleDegree,
        int SubdivisionCount,
        SelectedNoteSettings[] SelectedNotes,
        double Zoom,
        double PanX,
        double PanY);

    private sealed record TuningSettings(string Name, int[] MidiNotes);

    private sealed record SelectedNoteSettings(int SubdivisionIndex, int? Cycle);

    private sealed record NotePoolProfile(
        string Label,
        string HarmonyToDissonanceRatio,
        double FontSizeRem,
        double VisualOpacity,
        string AccentColor);

    private sealed record DiadAnalysis(
        string LeftLabel,
        string RightLabel,
        int IntervalSemitones,
        int FifthDistance,
        string HarmonyPercent,
        string DissonancePercent,
        string HarmonyToDissonanceRatio,
        double LeftLinePercent,
        double RightLinePercent,
        string LeftColor,
        string RightColor,
        string MoodLabel,
        string MoodColor);

    private sealed record TriadAnalysis(
        string NotesLabel,
        string HarmonyPercent,
        string DissonancePercent,
        string PairRatios,
        string QualityLabel,
        string QualityColor);

    private sealed record NextMoveRecommendation(
        int SubdivisionIndex,
        string NoteLabel,
        string ResultMoodLabel,
        string ProgressPercent,
        double Score);

    private sealed record ScaleTemplate(string Name, int[] Intervals);

    private sealed record ScaleRecommendation(
        string Label,
        string MatchSummary,
        int Score);

    private sealed record ProgressionMove(
        NoteChord Chord,
        string Relationship,
        int CofDistance,
        PairingStrength Strength);

    private sealed record ProgressionPath(
        string Title,
        IReadOnlyList<ProgressionMove> Moves);

    private static readonly ScaleTemplate[] CommonScales =
    [
        new("Chromatic", [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]),
        new("Major", [0, 2, 4, 5, 7, 9, 11]),
        new("Natural Minor", [0, 2, 3, 5, 7, 8, 10]),
        new("Harmonic Minor", [0, 2, 3, 5, 7, 8, 11]),
        new("Melodic Minor", [0, 2, 3, 5, 7, 9, 11]),
        new("Dorian", [0, 2, 3, 5, 7, 9, 10]),
        new("Phrygian", [0, 1, 3, 5, 7, 8, 10]),
        new("Lydian", [0, 2, 4, 6, 7, 9, 11]),
        new("Mixolydian", [0, 2, 4, 5, 7, 9, 10]),
        new("Locrian", [0, 1, 3, 5, 6, 8, 10]),
        new("Major Pentatonic", [0, 2, 4, 7, 9]),
        new("Minor Pentatonic", [0, 3, 5, 7, 10]),
        new("Blues", [0, 3, 5, 6, 7, 10])
    ];

    private enum MoodTarget
    {
        Resonant,
        Warm,
        Balanced,
        Tense,
        Dissonant
    }

    private sealed record SpiralGeometryFrame(double X, double Y, double Tx, double Ty, double Nx, double Ny);

    private static class EventUtil
    {
        public static Action<TValue> AsNonRenderingEventHandler<TValue>(Action<TValue> callback) =>
            new SyncReceiver<TValue>(callback).Invoke;

        private sealed record SyncReceiver<TValue>(Action<TValue> Callback) : ReceiverBase
        {
            public void Invoke(TValue arg) => Callback(arg);
        }

        private abstract record ReceiverBase : IHandleEvent
        {
            public Task HandleEventAsync(EventCallbackWorkItem callback, object? arg) => callback.InvokeAsync(arg);
        }
    }
}
