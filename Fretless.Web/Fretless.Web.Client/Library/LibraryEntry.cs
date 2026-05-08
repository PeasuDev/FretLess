namespace Fretless.Web.Client.Library;

/// <summary>
/// One entry in the component library sidebar. <c>DemoType</c> is rendered via
/// <c>&lt;DynamicComponent&gt;</c> in the right pane.
/// </summary>
public sealed record LibraryEntry(
    string Slug,
    string Title,
    string Description,
    LibraryGroup Group,
    Type DemoType,
    string SourcePath);
