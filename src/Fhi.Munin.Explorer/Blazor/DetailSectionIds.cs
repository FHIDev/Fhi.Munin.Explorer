using System.Globalization;
using System.Text;

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
    /// Every id above, for seeding a reservation set so a derived one cannot land on one of them.
    /// </summary>
    /// <remarks>
    /// The prefix separates the two namespaces today; this is what keeps it true, since
    /// <see cref="ReserveGroupId"/> would otherwise be reserving against half the page's ids.
    /// </remarks>
    internal static IReadOnlySet<string> Fixed { get; } = new HashSet<string>(
        [
            Metadata, Criteria, Source, Placement, Statistics, DataCollections, Versions,
            DataPeriod, DataType, VariableGroups, Variables, AccessCriteria, Prices, CodeLists,
        ],
        StringComparer.Ordinal);

    /// <summary>
    /// The <c>id</c> a section the catalogue placed anchors at, built from its group key and
    /// reserved in <paramref name="taken"/> so the next caller cannot be handed it again.
    /// </summary>
    /// <remarks>
    /// Prefixed because the keys are an open set a curator mints where the words above are a closed
    /// one, and stripped of what a fragment link cannot address, which no key is checked for at the
    /// source (Fhi.Metadata-35w0p.22). Two keys can strip alike, so the one the stripping rewrote
    /// carries a digest of itself rather than a number: numbered, which key holds the plain id
    /// would follow the order the page asks in, and a section added above an older one would move
    /// the deep link a reader had already shared. <paramref name="taken"/> is the backstop for a
    /// collision the digest did not separate (Fhi.Metadata-lr6yh).
    /// </remarks>
    internal static string ReserveGroupId(string key, ISet<string> taken)
    {
        var stripped = new string([
            .. key.Select(character =>
                char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-'),
        ]);

        var stem = GroupPrefix + stripped;
        var id = string.Equals(stripped, key, StringComparison.Ordinal) ? stem : $"{stem}-{Digest(key)}";
        var unique = id;

        for (var n = 2; !taken.Add(unique); n++)
        {
            unique = $"{id}-{n}";
        }

        return unique;
    }

    /// <summary>A short stable digest of a key, for telling apart two that strip to one stem.</summary>
    /// <remarks>
    /// FNV-1a rather than <see cref="string.GetHashCode()"/>, which is salted per process: an id
    /// that differed between two runs would resolve for the reader who sent the link and nobody else.
    /// </remarks>
    private static string Digest(string key)
    {
        var hash = 2166136261;

        unchecked
        {
            foreach (var b in Encoding.UTF8.GetBytes(key))
            {
                hash = (hash ^ b) * 16777619;
            }
        }

        return hash.ToString("x8", CultureInfo.InvariantCulture)[..4];
    }
}
