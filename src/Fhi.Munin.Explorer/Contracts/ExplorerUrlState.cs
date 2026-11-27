using System.Text;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Parsing;

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

    /// <summary>
    /// The order the reader chose; <see cref="SortField.Default"/>, the curated opening order with no
    /// active column, when none.
    /// </summary>
    public SortField Sort { get; init; } = SortField.Default;

    /// <summary>Which way that column is sorted.</summary>
    public SortDirection Direction { get; init; } = SortDirection.Ascending;

    /// <summary>The page the reader is on, one-based.</summary>
    public int Page { get; init; } = 1;

    /// <summary>How many rows a page holds.</summary>
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>The variable whose detail panel is open, or null when none is.</summary>
    /// <remarks>
    /// The part of the view a reader is likeliest to want to hand on: without it a link opens the
    /// search the sender was looking at, but not the variable they were looking at in it.
    /// </remarks>
    public Guid? SelectedVariableId { get; init; }

    /// <summary>
    /// The open variable's code, written as <c>variabel</c> in place of <c>variabelId</c>: a code is the same in every
    /// environment, where an id is not (Fhi.Metadata-l9l2n.182). <see cref="Blazor.VariableExplorer"/> resolves an arriving code
    /// through Munin before it opens anything; a code that names no variable opens as a search for the code.
    /// </summary>
    /// <remarks>
    /// Written whenever it is set, with <see cref="SelectedDatasamlingCode"/> beside it, or
    /// <see cref="SelectedDatasamlingId"/> where no code is set. A link with <c>variabelId</c> keeps working, and wins
    /// when both arrive.
    /// </remarks>
    public string? SelectedVariableCode { get; init; }

    /// <summary>
    /// The datasamling of the open row, written as <c>datasamlingId</c> beside <c>variabelId</c>, or beside
    /// <c>variabel</c> when <see cref="SelectedDatasamlingCode"/> is not set: a variable has a row per datasamling, so
    /// the variable alone does not name the row. A link without either opens the variable's first row, except where the
    /// host declines <c>datasamlingId</c> and the explorer already knows the variable's code: a navigation there opens
    /// the primary row, as <c>?variabel=</c> does. Never written without <see cref="SelectedVariableId"/> or
    /// <see cref="SelectedVariableCode"/>.
    /// </summary>
    public Guid? SelectedDatasamlingId { get; init; }

    /// <summary>
    /// The open row's datasamling by its code, written as <c>datasamling</c> beside <c>variabel</c> in place of
    /// <c>datasamlingId</c>, since a code is the same in every environment (Fhi.Metadata-l9l2n.204).
    /// </summary>
    /// <remarks>
    /// Read and written only beside <see cref="SelectedVariableCode"/>. <see cref="Blazor.VariableExplorer"/> matches it
    /// among the variable's datasamlinger as Munin's list import matches a datasamling code, so a «K_» is not left
    /// optional as the kilde page leaves it. When both arrive, the row this names opens, else the one <c>datasamlingId</c>
    /// names, else the primary row, which is said when the variable has more than one datasamling.
    /// </remarks>
    public string? SelectedDatasamlingCode { get; init; }

    /// <summary>The instrument whose page is open, or null when the reader is not on one.</summary>
    /// <remarks>
    /// An instrument opens as a view in place of the search, the same drill-in move a whole
    /// variable makes, so it belongs in a link for the same reason: without it a shared address
    /// reopens the search the sender came through rather than the page they were reading.
    /// <para>
    /// It wins over <see cref="SelectedVariableId"/> when a link carries both, because that is the
    /// order they are reached in: the instrument is opened from the variable, and the variable is
    /// what the page underneath goes back to.
    /// </para>
    /// </remarks>
    public Guid? SelectedInstrumentId { get; init; }

    /// <summary>
    /// The code of a shared variable list to open, upper-cased, or null. Written as <c>delekode</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="Parse"/> drops a value that is not 6 or 10 ASCII letters or digits, since the API
    /// cannot have minted it. See <see cref="SharedList"/> for what the code opens.
    /// </remarks>
    public string? ShareCode { get; init; }

    /// <summary>
    /// One of the signed-in reader's own variable lists to open, by the id <c>GET api/explorer/my/lists</c> gives it, or
    /// null. Written as <c>listeId</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a host page that lists a reader's lists and links back to each, such as an application form. Unlike
    /// <see cref="ShareCode"/> it opens the live list, notes included, and only for the reader who owns it.
    /// </para>
    /// <para>
    /// Parsed and written whatever the host's settings, since this type is a contract. <see cref="Blazor.VariableExplorer"/>
    /// reads it only with <c>MuninExplorer:Features:ListLink</c> on, and drops it from the address once it has opened the
    /// list or found it missing, so an address copied afterwards does not carry a private list's id.
    /// </para>
    /// </remarks>
    public Guid? OwnListId { get; init; }

    /// <summary>
    /// The page size a reader who has chosen nothing gets, and so the one value omitted from a URL.
    /// </summary>
    /// <remarks>
    /// Here rather than in the host because a host cannot read a component parameter's default, and
    /// every host that guessed it kept a second copy of the number to hold in step. Must match
    /// <c>VariableSearch.PageSize</c>; a test asserts it does, since the two are far apart and
    /// nothing else would notice them diverging.
    /// </remarks>
    public const int DefaultPageSize = 20;

    /// <summary>The smallest page size the explorer will request.</summary>
    public const int MinPageSize = 1;

    /// <summary>The largest page size the explorer will request.</summary>
    /// <remarks>
    /// Here rather than only inside the component because a size outside this range cannot be
    /// honoured, and a URL that names one would describe a page the reader is not looking at: the
    /// component clamps what it sends to the API but reports the raw value back, so an unclamped
    /// <c>?pageSize=99999</c> would be written back into the address bar over a hundred-row page.
    /// <see cref="Parse"/> drops such a value instead, and <c>VariableSearch</c> clamps against
    /// these same two constants, so there is one range rather than two.
    /// </remarks>
    public const int MaxPageSize = 100;

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
    /// <see cref="VariableFilter"/>'s own cap. Well above the eight keys here plus a host's own.
    /// </remarks>
    internal const int MaxParameters = 200;

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

        if (SelectedVariableCode is { } code)
        {
            Append(query, "variabel", code);

            if (!string.IsNullOrWhiteSpace(SelectedDatasamlingCode))
            {
                Append(query, "datasamling", SelectedDatasamlingCode.Trim());
            }
            else if (SelectedDatasamlingId is { } datasamling)
            {
                Append(query, "datasamlingId", datasamling.ToString());
            }
        }
        else if (SelectedVariableId is { } selected)
        {
            Append(query, "variabelId", selected.ToString());

            if (SelectedDatasamlingId is { } datasamling)
            {
                Append(query, "datasamlingId", datasamling.ToString());
            }
        }

        if (SelectedInstrumentId is { } instrument)
        {
            Append(query, "instrumentId", instrument.ToString());
        }

        Append(query, "delekode", SharedList.NormalizeCode(ShareCode));

        if (OwnListId is { } list)
        {
            Append(query, "listeId", list.ToString());
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

        foreach (var (_, name, value) in QueryPairs.Split(queryString, MaxParameters))
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            state = Apply(state, name, value);
        }

        // An id names the variable outright, so a code beside it is not read: ToQueryString would write the code instead.
        // A datasamling's code is read only beside a variable's, the one place ToQueryString writes it.
        return state.SelectedVariableId is null && state.SelectedVariableCode is not null
            ? state
            : state with { SelectedVariableCode = null, SelectedDatasamlingCode = null };
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

        // Dropped rather than clamped, and for the same reason the search is: a size outside the
        // range cannot be honoured, and silently substituting one would put a number in the URL
        // that describes a different page from the one on screen.
        if (Is(name, "pageSize"))
        {
            return int.TryParse(value, out var size) && size is >= MinPageSize and <= MaxPageSize
                ? state with { PageSize = size }
                : state;
        }

        // An id in a URL is whatever a stranger typed, and nothing is validated beyond the format:
        // one that names no variable the first page holds is dropped by the component itself, which
        // then reports the selection back as null.
        if (Is(name, "variabelId"))
        {
            return Guid.TryParse(value, out var variable) ? state with { SelectedVariableId = variable } : state;
        }

        // A code is checked for shape only; VariableExplorer asks Munin what it names (Fhi.Metadata-l9l2n.182).
        if (Is(name, "variabel"))
        {
            return VariableCodes.IsLinkCode(value) ? state with { SelectedVariableCode = value.Trim() } : state;
        }

        if (Is(name, "datasamling"))
        {
            return DatasamlingCodes.IsLinkCode(value) ? state with { SelectedDatasamlingCode = value.Trim() } : state;
        }

        // On the same terms as the variable; a datasamling it has no row in leaves the row closed.
        if (Is(name, "datasamlingId"))
        {
            return Guid.TryParse(value, out var datasamling) ? state with { SelectedDatasamlingId = datasamling } : state;
        }

        // Read on the same terms, and dropped the same way: an instrument the API does not publish
        // is reported back as null by the component once it has asked.
        if (Is(name, "instrumentId"))
        {
            return Guid.TryParse(value, out var instrument)
                ? state with { SelectedInstrumentId = instrument }
                : state;
        }

        if (Is(name, "delekode"))
        {
            return SharedList.NormalizeCode(value) is { } code ? state with { ShareCode = code } : state;
        }

        // Format only: whether the reader owns the list is the list view's question, asked of Munin.
        if (Is(name, "listeId"))
        {
            return Guid.TryParse(value, out var list) ? state with { OwnListId = list } : state;
        }

        return state;
    }

    private static bool Is(string name, string expected) =>
        string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);

    /// <summary>The keys this type reads and writes itself, beside the filter's own.</summary>
    /// <remarks>
    /// Separate from <see cref="QueryKeys"/> because these are the ones a host can plausibly
    /// collide with — <c>page</c> and <c>search</c> are anyone's parameter names, where
    /// <c>variabelgruppeIds</c> is nobody's. It is therefore the set a host may ask an explorer
    /// component to leave alone. <c>variabelId</c> and <c>instrumentId</c> are in here for that
    /// reason too: a host with a variable or instrument page of its own plausibly already means
    /// something by either, and so is <c>delekode</c>, which a host may already use for sharing
    /// of its own, and <c>listeId</c>, for the same reason. <c>variabel</c> is the variable's code: declining it keeps codes out of the address, which then names the
    /// open variable by <c>variabelId</c> as before. Declining <c>variabelId</c> leaves the open variable out altogether,
    /// its code and datasamling with it. Declining <c>datasamling</c> names the open row's datasamling by
    /// <c>datasamlingId</c> as before, and declining <c>datasamlingId</c> leaves out only that key.
    /// </remarks>
    public static IReadOnlySet<string> ScalarQueryKeys { get; } =
        new HashSet<string>(
            ["search", "sort", "sortDir", "page", "pageSize", "variabelId", "variabel", "datasamling", "datasamlingId", "instrumentId", "delekode", "listeId"],
            StringComparer.OrdinalIgnoreCase);

    /// <summary>The keys this type reads and writes, so a host can tell them from its own.</summary>
    /// <remarks>
    /// <para>
    /// A host that mounts more than one thing on a page needs to know which parameters are ours
    /// before it decides what to do with the rest. It is also the answer to "which of these may I
    /// leave in the URL": everything not named here survives untouched, because
    /// <see cref="ToQueryString"/> never writes it.
    /// </para>
    /// <para>
    /// Case-insensitive, because <see cref="Parse"/> is: a host testing membership against an
    /// ordinal list would miss <c>?Search=</c>, keep it as one of its own, and end up with the
    /// parameter twice in the URL it rebuilds.
    /// </para>
    /// <para>
    /// <see cref="VariableFilter.QueryKeys"/> is in here too, and was missing until
    /// <c>Fhi.Metadata-zrcf4</c>: this type writes the facets through
    /// <see cref="VariableFilter.ToQueryString"/>, so a host that read the five scalars as the whole
    /// answer kept <c>?kildeIds=</c> as one of its own and had it written twice. Declared below
    /// <see cref="ScalarQueryKeys"/> because a static initialiser only sees the fields above it.
    /// </para>
    /// </remarks>
    public static IReadOnlySet<string> QueryKeys { get; } =
        new HashSet<string>(
            [.. ScalarQueryKeys, .. VariableFilter.QueryKeys], StringComparer.OrdinalIgnoreCase);

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
