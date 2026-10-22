namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// What a heading or a control shows for a catalogue thing: its name in the reader's language
/// where the catalogue has one, its Norwegian name, its code, or <see cref="Texts.NotSpecified"/>.
/// </summary>
/// <param name="Text">The words to draw.</param>
/// <param name="Named">Whether <paramref name="Text"/> is a name rather than a code or the stand-in.</param>
/// <param name="Norwegian">Whether <paramref name="Text"/> is the catalogue's Norwegian, and so marked on an English page.</param>
internal readonly record struct CatalogueName(string Text, bool Named, bool Norwegian)
{
    /// <summary>The <c>lang</c> this name is marked with, or null where it is in the reader's language or nobody's.</summary>
    internal string? Lang(string reader) => CatalogueProperties.Foreign(Norwegian, reader);

    /// <summary>The shape <see cref="DetailTrail.Append"/> and the older call sites take.</summary>
    internal (string Text, bool Norwegian) Marked => (Text, Norwegian);
}
