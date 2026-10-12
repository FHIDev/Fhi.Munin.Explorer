using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The catalogue's «Variabelreferanse»: where a kilde's or a datasamling's variables are documented
/// when that is somewhere else than Munin (Fhi.Metadata-ot99a).
/// </summary>
internal static class VariableReference
{
    /// <summary>The additional-property key, as the API spells it.</summary>
    public const string Key = "Variabelreferanse";

    /// <summary>The address, or null when there is none or it is not an http(s) one.</summary>
    /// <remarks>Never inherited: a datasamling without its own key has none, whatever its kilde has.</remarks>
    public static string? Of(IReadOnlyDictionary<string, string?>? properties)
    {
        if (properties is null || !properties.TryGetValue(Key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var address = value.Trim();

        return Uri.TryCreate(address, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? address
            : null;
    }

    /// <summary>One sentence ending in the address as a link, the way Lovverk shows its own.</summary>
    public static RenderFragment Sentence(string lead, string address) => builder =>
    {
        builder.OpenElement(0, "p");
        builder.AddContent(1, lead);
        builder.AddContent(2, " ");
        builder.OpenElement(3, "a");
        builder.AddAttribute(4, "href", address);
        builder.AddAttribute(5, "rel", "noopener noreferrer");
        builder.AddContent(6, address);
        builder.CloseElement();
        builder.CloseElement();
    };
}
