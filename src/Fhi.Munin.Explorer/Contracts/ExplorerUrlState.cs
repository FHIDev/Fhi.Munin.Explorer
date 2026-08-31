using System.Text;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>
/// Everything about a variable search that belongs in a host's address bar, in one value.
/// </summary>
/// <remarks>
/// <para>
/// The package owns no URL. A host does, and until now each one rebuilt this from scratch: parsing
/// five scalars beside <see cref="VariableFilter"/>, restating the component's own page-size default
/// to know what to omit, and getting <c>?sort=999</c> wrong in the same way each time. This is that
/// work, once, beside the filter that already round-trips itself.
/// </para>
/// <para>
/// What it deliberately does not do is touch the address bar. Reading the incoming query, knowing
/// the path the component is mounted on, and writing history entries stay the host's: only the host
/// knows them, and a package that guessed would be wrong behind a reverse proxy.
/// </para>
/// </remarks>
public sealed record ExplorerUrlState
{
    /// <summary>The state of an explorer nobody has touched. Produces an empty query string.</summary>
    public static readonly ExplorerUrlState None = new();

    /// <summary>The facet selection, which carries its own query format both ways.</summary>
    public VariableFilter Filter { get; init; } = VariableFilter.None;

    /// <summary>The free-text search, or null when the reader has not searched.</summary>
    public string? Search { get; init; }

    /// <summary>The column the reader sorted on.</summary>
    public SortField Sort { get; init; } = SortField.Default;

    /// <summary>Which way that column is sorted.</summary>
    public SortDirection Direction { get; init; } = SortDirection.Ascending;

    /// <summary>The page the reader is on, one-based.</summary>
    public int Page { get; init; } = 1;

    /// <summary>How many rows a page holds.</summary>
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>
    /// The page size a reader who has chosen nothing gets, and so the one value omitted from a URL.
    /// </summary>
    /// <remarks>
    /// Here rather than in the host because a host cannot read a component parameter's default, and
    /// every host that guessed it kept a second copy of the number to hold in step. Must match
    /// <c>VariableExplorer.PageSize</c>; a test asserts it does, since the two are far apart and
    /// nothing else would notice them diverging.
    /// </remarks>
    public const int DefaultPageSize = 20;

    /// <summary>How long a search term may be before <see cref="Parse"/> drops it.</summary>
    /// <remarks>
    /// The same reasoning as <see cref="VariableFilter"/>'s own caps: this parses whatever a public,
    /// unauthenticated URL carried, and the result is held for the life of a circuit and written
    /// onto every outbound API call. Far above any search a reader would type.
    /// </remarks>
    private const int MaxSearchLength = 200;

    /// <summary>The largest page number <see cref="Parse"/> will read.</summary>
    /// <remarks>
    /// Not a correctness bound — the component clamps against the real result count and reports back
    /// where it landed, so a page past the end is already handled. This only stops a crafted number
    /// from arriving as something the component has to reason about.
    /// </remarks>
    private const int MaxPage = 1_000_000;

    /// <summary>How many parameters <see cref="Parse"/> reads before ignoring the rest.</summary>
    /// <remarks>
    /// Bounds the parse itself and not only what it keeps, the same reasoning as
    /// <see cref="VariableFilter"/>'s own cap. Well above the five keys here plus a host's own.
    /// </remarks>
    private const int MaxParameters = 200;

    /// <summary>
    /// This state as an escaped query string with no leading <c>?</c>, empty when nothing is set.
    /// </summary>
    /// <remarks>
    /// Only what differs from a default is written. A link carries the parts of the view someone
    /// chose, not a transcript of every setting the component holds — so a plain search stays short
    /// enough to read, and an untouched explorer produces nothing at all.
    /// </remarks>
    public string ToQueryString()
    {
        var query = new StringBuilder(Filter.ToQueryString());

        Append(query, "search", Search);

        if (Sort != SortField.Default)
        {
            Append(query, "sort", Sort.ToString());
        }

        if (Direction != SortDirection.Ascending)
        {
            Append(query, "sortDir", Direction.ToString());
        }

        if (Page > 1)
        {
            Append(query, "page", Page.ToString());
        }

        if (PageSize != DefaultPageSize)
        {
            Append(query, "pageSize", PageSize.ToString());
        }

        return query.ToString();
    }

    /// <summary>
    /// Reads back what <see cref="ToQueryString"/> writes, keeping only what it can make sense of.
    /// </summary>
    /// <param name="queryString">
    /// A query string with or without its leading <c>?</c>. A host hands over whatever the request
    /// carried; anything unreadable is dropped rather than rejected, so a mangled link opens a
    /// working explorer instead of an error.
    /// </param>
    public static ExplorerUrlState Parse(string? queryString)
    {
        var state = new ExplorerUrlState { Filter = VariableFilter.Parse(queryString) };

        if (string.IsNullOrWhiteSpace(queryString))
        {
            return state;
        }

        // Split by hand, the way VariableFilter does and for its reasons: the package cannot take a
        // dependency on Microsoft.AspNetCore.WebUtilities, which a host gets for free and an RCL
        // does not. The parameter cap bounds the parse itself, not only what it keeps.
        var read = 0;
        foreach (var pair in queryString.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            if (++read > MaxParameters)
            {
                break;
            }

            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var name = Decode(pair[..separator]);
            var value = Decode(pair[(separator + 1)..]);

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            state = Apply(state, name, value);
        }

        return state;
    }

    private static ExplorerUrlState Apply(ExplorerUrlState state, string name, string value)
    {
        // Case-insensitive by comparison rather than by folding the name, as VariableFilter does:
        // a lowercased copy is a copy that can drift from what ToQueryString writes.
        if (Is(name, "search"))
        {
            // Dropped rather than truncated: half a search term is not what anyone asked for, and a
            // string this long was not typed by a reader.
            return value.Length <= MaxSearchLength ? state with { Search = value } : state;
        }

        if (Is(name, "sort"))
        {
            return Named<SortField>(value) is { } sort ? state with { Sort = sort } : state;
        }

        if (Is(name, "sortDir"))
        {
            return Named<SortDirection>(value) is { } direction ? state with { Direction = direction } : state;
        }

        if (Is(name, "page"))
        {
            return int.TryParse(value, out var page)
                ? state with { Page = Math.Clamp(page, 1, MaxPage) }
                : state;
        }

        // Not clamped to the component's 1-100: it holds every size to that range on its way to the
        // API, and a second copy of the rule here would be one more thing to keep in step.
        if (Is(name, "pageSize"))
        {
            return int.TryParse(value, out var size) ? state with { PageSize = size } : state;
        }

        return state;
    }

    private static bool Is(string name, string expected) =>
        string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);

    private static string Decode(string value) =>
        Uri.UnescapeDataString(value.Replace('+', ' '));

    /// <summary>The keys this type reads and writes, so a host can tell them from its own.</summary>
    /// <remarks>
    /// A host that mounts more than one thing on a page needs to know which parameters are ours
    /// before it decides what to do with the rest. It is also the answer to "which of these may I
    /// leave in the URL": everything not named here survives untouched, because
    /// <see cref="ToQueryString"/> never writes it.
    /// </remarks>
    public static IReadOnlyList<string> QueryKeys { get; } =
        ["search", "sort", "sortDir", "page", "pageSize"];

    private static void Append(StringBuilder query, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        query.Append(query.Length == 0 ? "" : "&")
             .Append(name)
             .Append('=')
             .Append(Uri.EscapeDataString(value));
    }

    /// <summary>An enum value the query string actually names, or null.</summary>
    /// <remarks>
    /// <see cref="Enum.TryParse{TEnum}(string?, out TEnum)"/> alone is not enough: it accepts any
    /// number, so <c>?sort=999</c> succeeds and yields a value no case covers, which then travels
    /// into the component and out to the API as a sort nobody defined.
    /// </remarks>
    private static TEnum? Named<TEnum>(string? value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : null;
}
