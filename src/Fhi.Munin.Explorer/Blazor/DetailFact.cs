namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// One fact in a detail page's hero row: a label, a value, and an optional second line under it.
/// </summary>
/// <remarks>
/// <para>
/// <c>Note</c> is the qualifier that makes the value honest rather than a decoration —
/// <c>630 variabler</c> alone is a different claim from <c>630 variabler i 6 datasamlinger</c>.
/// Null or blank draws no element at all, so a fact without one costs no empty
/// <c>&lt;small&gt;</c>, and a <c>NoteLabel</c> with nothing to name goes with it.
/// </para>
/// <para>
/// A fact whose <c>Value</c> is null or blank is dropped by <see cref="DetailFacts"/> rather than
/// drawn empty — unlike the fact lists further down the page, where <see cref="DetailBlocks"/> draws
/// it as a muted "Ingen". A page can therefore lead with fewer cells than it named, and the hero row
/// is the summary of what this record actually has.
/// </para>
/// </remarks>
/// <param name="Label">The field's name in the reader's language.</param>
/// <param name="Value">The value as the reader should see it, or null to drop the fact.</param>
/// <param name="Lang">
/// A <c>lang</c> for the value when it is not in the reader's language — the catalogue stores its
/// words in Norwegian, and an unmarked one is read to an English reader with English phonetics.
/// Null leaves it inheriting the host's own language.
/// </param>
/// <param name="NoteLabel">
/// The field <paramref name="Note"/> is a value of, drawn before it as <c>Telleenhet: Pasient</c>,
/// or null where the note names itself. A bare qualifier says nothing: <c>2006–</c> under a data
/// period reads as a second period rather than as the register's own validity.
/// </param>
/// <param name="Note">The second line's value, or null when the field has no qualifier to add.</param>
/// <param name="NoteLang">
/// The same as <paramref name="Lang"/> for <paramref name="Note"/>, and separate from it because
/// the two need not be in one language: a count is in no language at all while the unit it is
/// counted in is catalogue free text stored only in Norwegian. It marks the note's value alone and
/// never <paramref name="NoteLabel"/>, which is this package's own word and is already in the
/// reader's language.
/// </param>
public sealed record DetailFact(
    string Label,
    string? Value,
    string? Lang = null,
    string? NoteLabel = null,
    string? Note = null,
    string? NoteLang = null);
