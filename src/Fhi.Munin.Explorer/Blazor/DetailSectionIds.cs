namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The <c>id</c> each section of a detail view carries.
/// </summary>
/// <remarks>
/// Fixed English literals, never a slug of the heading: the headings come from <see cref="Texts"/>,
/// so a derived id would differ between nb and en and change again with any rewording, and a link
/// into a section is a link a reader sends to another reader.
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

    /// <summary>
    /// The id of a section the catalogue placed, or nothing where its key anchors no fragment.
    /// </summary>
    /// <remarks>
    /// Stemmed on <see cref="Metadata"/>, so a curator's key can collide with neither the literals
    /// above nor a host's own. The key and never the heading, for the reason this whole type exists:
    /// the headings are bilingual and a link into a section is one reader's to send another.
    /// </remarks>
    internal static string? Placed(string? groupKey)
    {
        var slug = string.Concat((groupKey ?? "").ToLowerInvariant()
                                                .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '-'))
                         .Trim('-');

        return slug.Length == 0 ? null : $"{Metadata}-{slug}";
    }
}
