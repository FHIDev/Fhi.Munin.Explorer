namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// One step of a detail page's breadcrumb: where it goes, and the words it goes by.
/// </summary>
/// <remarks>
/// <para>
/// <c>Href</c> is nullable because this package cannot invent one. There is no router here and
/// helsedata's addresses are not ours, so a target is something the surface above hands down — the
/// shape <see cref="KildeExplorer"/> already uses for the drill-in link it builds off the host's
/// own address. Null means nobody supplied a target, and the step is then drawn as plain text
/// rather than as a link that goes nowhere.
/// </para>
/// <para>
/// The last step of a trail is the page the reader is on. It is never drawn as a link whatever it
/// carries here — <see cref="DetailTrail"/> marks it <c>aria-current="page"</c> and drops any
/// <c>Href</c>, so the rule cannot be broken one call site at a time.
/// </para>
/// </remarks>
/// <param name="Label">The step's words, in whichever language the value is really in.</param>
/// <param name="Href">Where the step goes, or null when the surface above supplied no target.</param>
/// <param name="Lang">
/// A <c>lang</c> for the label when it is not in the reader's language — the catalogue stores its
/// names in Norwegian, and an unmarked one is read to an English reader with English phonetics.
/// Null leaves the label inheriting the host's own language.
/// </param>
public sealed record DetailTrailStep(string Label, string? Href, string? Lang = null);
