using System.Net;

namespace Fhi.Munin.Explorer.Contracts;

/// <summary>Which of Munin's per-person list quotas refused a write.</summary>
public enum ListQuotaKind
{
    /// <summary>At the most lists Munin allows a person (<c>list-limit</c>); no list was made.</summary>
    Lists = 1,

    /// <summary>Past the rows Munin allows a person's lists together (<c>row-limit</c>); nothing was added.</summary>
    Rows
}

/// <summary>A list create or add Munin refused at a <see cref="ListQuotaKind"/> quota: HTTP 409 with its code.</summary>
/// <remarks>Other 409s stay a plain <see cref="HttpRequestException"/>, which this derives from, so a host catching that still does.
/// Not a fault: the reader must delete a list or remove rows, and trying again changes nothing.</remarks>
public sealed class MuninExplorerListQuotaException(ListQuotaKind kind, int limit, string? apiText)
    : HttpRequestException(
        $"The Munin Explorer API refused a list write at the {kind switch { ListQuotaKind.Lists => "list-limit", ListQuotaKind.Rows => "row-limit", _ => kind.ToString() }} quota of {limit}.",
        null,
        HttpStatusCode.Conflict)
{
    /// <summary>Which quota the write met.</summary>
    public ListQuotaKind Kind { get; } = kind;

    /// <summary>The quota's ceiling as Munin sent it: lists, or rows.</summary>
    public int Limit { get; } = limit;

    /// <summary>Munin's sentence, in its default language since these writes send no <c>Accept-Language</c>; null when it sent none.</summary>
    public string? ApiText { get; } = apiText;
}
