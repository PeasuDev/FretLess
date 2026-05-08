namespace Fretless.Web.Client;

/// <summary>
/// Tracks the instrument selected by the connect-instrument flow.
/// </summary>
public sealed class InstrumentConnectionService
{
    /// <summary>Fired when a user connects an instrument from the layout modal.</summary>
    public event Action<ConnectedInstrument>? InstrumentConnected;

    /// <summary>Publishes the selected instrument and recording method to interested UI components.</summary>
    public void Connect(string instrument, string recordingMethod)
    {
        if (string.IsNullOrWhiteSpace(instrument))
        {
            throw new ArgumentException("Instrument is required.", nameof(instrument));
        }

        if (string.IsNullOrWhiteSpace(recordingMethod))
        {
            throw new ArgumentException("Recording method is required.", nameof(recordingMethod));
        }

        InstrumentConnected?.Invoke(new ConnectedInstrument(instrument, recordingMethod));
    }
}

/// <summary>Describes a connected analog instrument source.</summary>
public sealed record ConnectedInstrument(string Instrument, string RecordingMethod);
