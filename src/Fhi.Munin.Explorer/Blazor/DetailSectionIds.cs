namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The <c>id</c> each section of a detail view carries.
/// </summary>
/// <remarks>
/// Fixed English literals, never a slug of the heading: the headings come from <see cref="Texts"/>,
/// so a derived id would differ between nb and en and change again with any rewording, and a link
/// into a section is a link a reader sends to another reader. A section the catalogue placed
/// anchors at <see cref="ReserveGroupId"/> instead, which is a key and so keeps that promise.
/// <para>
/// Global, which is the price of that promise and why a document holds ONE detail view — see
/// <see cref="DetailSection.Id"/>, where a host developer reads it.
/// </para>
/// </remarks>
internal static class DetailSectionIds
{
    internal const string Metadata = "metadata";

    internal const string Criteria = "criteria";

    internal const string Source = "source";

    internal const string Placement = "placement";

    internal const string Statistics = "statistics";

    internal const string DataCollections = "datacollections";

    internal const string Versions = "versions";

    internal const string DataPeriod = "dataperiod";

    internal const string DataType = "datatype";

    internal const string VariableGroups = "variablegroups";

    // The four below are the explorers' own, handed to a view as DetailNamedSection. The view's ids
    // and these share one document, which is why they are listed together.
    internal const string Variables = "variables";

    internal const string AccessCriteria = "accesscriteria";

    internal const string Prices = "prices";

    internal const string CodeLists = "codelists";

    /// <summary>What every id derived from a catalogue group key starts with.</summary>
    internal const string GroupPrefix = "section-";

    /// <summary>
    /// The <c>id</c> a section the catalogue placed anchors at, built from its group key and
    /// reserved in <paramref name="taken"/> so the next caller cannot be handed it again.
    /// </summary>
    /// <remarks>
    /// Prefixed because the keys are an open set a curator mints where the words above are a closed
    /// one, and stripped of what a fragment link cannot address, which no key is checked for at the
    /// source (Fhi.Metadata-35w0p.22). Two keys can strip alike, so a repeat is numbered apart
    /// rather than left anchoring two sections at once — which of them keeps the unnumbered id
    /// follows the order they are asked in (Fhi.Metadata-lr6yh).
    /// </remarks>
    internal static string ReserveGroupId(string key, ISet<string> taken)
    {
        var stem = GroupPrefix + new string([
            .. key.Select(character =>
                char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-'),
        ]);

        var id = stem;

        for (var n = 2; !taken.Add(id); n++)
        {
            id = $"{stem}-{n}";
        }

        return id;
    }
}
