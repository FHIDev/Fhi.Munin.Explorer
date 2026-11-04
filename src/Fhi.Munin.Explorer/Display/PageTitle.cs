namespace Fhi.Munin.Explorer.Display;

/// <summary>An item view's page title from the host's template (WCAG 2.4.2, Fhi.Metadata-mhqx0.14).</summary>
internal static class PageTitle
{
    internal const string NamePlaceholder = "{0}";

    // Null means the host page's own title: never a template with a hole in it («– Kildeutforsker»).
    internal static string? Compose(string? template, string? name)
    {
        if (string.IsNullOrWhiteSpace(template) || string.IsNullOrWhiteSpace(name)
            || !template.Contains(NamePlaceholder, StringComparison.Ordinal))
        {
            return null;
        }

        return template.Replace(NamePlaceholder, name.Trim(), StringComparison.Ordinal).Trim();
    }
}
