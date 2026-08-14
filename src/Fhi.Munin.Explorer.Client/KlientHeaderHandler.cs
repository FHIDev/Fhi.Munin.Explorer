using System.Reflection;

namespace Fhi.Munin.Explorer.Client;

/// <summary>
/// Stamps every outgoing request with the header that identifies this client to Munin.
/// </summary>
/// <remarks>
/// Munin's Explorer API is public and anonymous, so a request carries nothing that says who made
/// it. The header is what lets Munin split its observability by consumer — "how much of the
/// Explorer traffic is the embedded component on helsedata.no, and which version of it" — and the
/// same signal is what load will be attributed by if the API is ever rate-limited per consumer.
/// <para>
/// It is a <see cref="DelegatingHandler"/> rather than a default request header on the
/// <c>HttpClient</c> so it applies to every request the pipeline makes, including retries a host
/// adds its own handler for, and so a host that constructs its own client cannot forget it.
/// </para>
/// </remarks>
internal sealed class KlientHeaderHandler : DelegatingHandler
{
    /// <summary>The header Munin's dashboards group by.</summary>
    internal const string Header = "X-Munin-Explorer-Client";

    /// <summary>
    /// Names the kind of consumer, not the package: several packages may ship out of this repo,
    /// but from Munin's side they are one Blazor component embedded in a host.
    /// </summary>
    private const string Klient = "blazor";

    private static readonly string Verdi = $"{Klient}/{LesVersjon()}";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A retried request already carries the header; adding it twice would send two values.
        if (!request.Headers.Contains(Header))
        {
            // Without validation: the value is sanitised below, and a malformed version string
            // must not be the reason a page fails to load its data.
            request.Headers.TryAddWithoutValidation(Header, Verdi);
        }

        return base.SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Reads the assembly's informational version — the one the build stamps from
    /// <c>VersionPrefix</c>, and the release pipeline from the tag.
    /// </summary>
    private static string LesVersjon()
    {
        var assembly = typeof(KlientHeaderHandler).Assembly;

        var versjon = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString();

        if (string.IsNullOrWhiteSpace(versjon))
        {
            return "ukjent";
        }

        // SourceLink appends "+<commit sha>". Keeping it would give Munin a new label value per
        // commit, which is exactly the cardinality explosion that makes a dashboard useless.
        var pluss = versjon.IndexOf('+');
        if (pluss >= 0)
        {
            versjon = versjon[..pluss];
        }

        return Rensk(versjon);
    }

    /// <summary>Keeps the value to characters that are safe in a header token.</summary>
    private static string Rensk(string versjon)
    {
        var rene = versjon.Where(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_').ToArray();

        return rene.Length == 0 ? "ukjent" : new string(rene);
    }
}
