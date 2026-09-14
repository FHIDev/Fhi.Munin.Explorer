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

    internal const string Statistics = "statistics";

    internal const string DataCollections = "datacollections";

    internal const string Versions = "versions";

    internal const string DataPeriod = "dataperiod";

    internal const string DataType = "datatype";

    internal const string VariableGroups = "variablegroups";
}
