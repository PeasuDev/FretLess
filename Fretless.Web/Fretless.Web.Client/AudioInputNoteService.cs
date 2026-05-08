using Microsoft.JSInterop;

namespace Fretless.Web.Client;

/// <summary>
/// Singleton service that bridges browser audio-input pitch detection to C# note events.
/// </summary>
public sealed class AudioInputNoteService : IAsyncDisposable
{
    /// <summary>Fired on the Blazor synchronisation context when audio input detects a stable note.</summary>
    public event Action<int>? NoteOn;

    /// <summary>Fired on the Blazor synchronisation context when audio input stops detecting the active note.</summary>
    public event Action<int>? NoteOff;

    /// <summary>Fired when the detector estimates a pitch frame.</summary>
    public event Action<int, double, double>? PitchDetected;

    /// <summary>Fired when the detector measures an input signal frame.</summary>
    public event Action<double>? LevelDetected;

    private DotNetObjectReference<AudioInputNoteService>? _dotNetRef;
    private IJSRuntime? _js;
    private bool _started;

    /// <summary>Requests audio input access and begins lightweight monophonic pitch detection.</summary>
    public async Task<bool> StartAsync(IJSRuntime js, string recordingMethod = "Microphone")
    {
        _js = js;
        _dotNetRef ??= DotNetObjectReference.Create(this);
        _started = await js.InvokeAsync<bool>("fretlessAudioInput.start", _dotNetRef, recordingMethod);
        return _started;
    }

    /// <summary>Stops audio input detection and releases the browser audio stream.</summary>
    public async Task StopAsync()
    {
        if (_js is null)
        {
            _started = false;
            return;
        }

        await _js.InvokeVoidAsync("fretlessAudioInput.stop");
        _started = false;
    }

    /// <summary>Called by JS when audio input detects a stable note.</summary>
    [JSInvokable]
    public void OnAudioInputNoteOn(int midi) => NoteOn?.Invoke(midi);

    /// <summary>Called by JS when audio input stops detecting the active note.</summary>
    [JSInvokable]
    public void OnAudioInputNoteOff(int midi) => NoteOff?.Invoke(midi);

    /// <summary>Called by JS when audio input estimates a pitch frame.</summary>
    [JSInvokable]
    public void OnAudioInputPitchDetected(int midi, double frequency, double rms) => PitchDetected?.Invoke(midi, frequency, rms);

    /// <summary>Called by JS when audio input measures a signal frame.</summary>
    [JSInvokable]
    public void OnAudioInputLevelDetected(double rms) => LevelDetected?.Invoke(rms);

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_started && _js is not null)
            {
                await _js.InvokeVoidAsync("fretlessAudioInput.dispose");
            }
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        finally
        {
            _started = false;
            _dotNetRef?.Dispose();
            _dotNetRef = null;
            _js = null;
        }
    }
}
