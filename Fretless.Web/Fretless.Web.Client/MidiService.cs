using Microsoft.JSInterop;

namespace Fretless.Web.Client;

/// <summary>
/// Singleton service that bridges the Web MIDI API to C# events.
/// Call <see cref="InitAsync"/> once after first user interaction to request MIDI access.
/// </summary>
public sealed class MidiService : IAsyncDisposable
{
    /// <summary>Fired on the Blazor synchronisation context when a MIDI Note On message arrives.</summary>
    public event Action<int>? NoteOn;

    /// <summary>Fired on the Blazor synchronisation context when a MIDI Note Off message arrives.</summary>
    public event Action<int>? NoteOff;

    private DotNetObjectReference<MidiService>? _dotNetRef;
    private IJSRuntime? _js;
    private bool _initialized;

    /// <summary>Requests MIDI access and begins listening. Safe to call multiple times.</summary>
    public async Task InitAsync(IJSRuntime js)
    {
        if (_initialized) return;
        _initialized = true;
        _js = js;
        _dotNetRef = DotNetObjectReference.Create(this);
        await js.InvokeVoidAsync("fretlessMidi.init", _dotNetRef);
    }

    /// <summary>Called by JS when a MIDI Note On message is received.</summary>
    [JSInvokable]
    public void OnMidiNoteOn(int midi) => NoteOn?.Invoke(midi);

    /// <summary>Called by JS when a MIDI Note Off message is received.</summary>
    [JSInvokable]
    public void OnMidiNoteOff(int midi) => NoteOff?.Invoke(midi);

    public async ValueTask DisposeAsync()
    {
        if (!_initialized) return;
        try
        {
            if (_js is not null)
            {
                await _js.InvokeVoidAsync("fretlessMidi.dispose");
            }
        }
        catch (JSException) { }
        catch (InvalidOperationException) { }
        finally
        {
            _dotNetRef?.Dispose();
            _dotNetRef = null;
            _js = null;
        }
    }
}
