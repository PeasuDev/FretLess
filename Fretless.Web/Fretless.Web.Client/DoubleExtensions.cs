namespace Fretless.Web.Client;

internal static class DoubleExtensions
{
    // Formats a double using invariant culture for SVG attribute values
    internal static string ToInvariant(this double value) =>
        value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture);

    // Alias for use inside interpolated SVG paths
    internal static string I(this double value) => value.ToInvariant();
}
