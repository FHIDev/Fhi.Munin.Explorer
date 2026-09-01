using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The three pieces every detail view is built from: a heading at a level the caller picks, a
/// definition list of facts, and one group of the catalogue's own properties.
/// </summary>
/// <remarks>
/// Shared by <see cref="KildeView"/>, <see cref="VariableView"/> and <see cref="DatasamlingView"/>,
/// which drew it from three private copies. Level and class stay the caller's, as they are for
/// <see cref="StatisticsBlock"/>: the same block sits at three different depths.
/// </remarks>
internal static class DetailBlocks
{
    /// <summary>A heading at the given level, so a view nests wherever it is put.</summary>
    internal static RenderFragment Heading(int level, string text, string cssClass,
                                           string? id = null, string? language = null) => builder =>
    {
        builder.OpenElement(0, $"h{level}");
        builder.AddAttribute(1, "class", cssClass);
        builder.AddAttribute(2, "id", id);
        builder.AddAttribute(3, "lang", language);
        builder.AddContent(4, text);
        builder.CloseElement();
    };

    /// <summary>
    /// A definition list of label and value, skipping anything the catalogue has not filled in.
    /// </summary>
    /// <remarks>
    /// The emptiness question is answered here rather than at each call site, for the reason
    /// <see cref="StatisticsBlock"/> gives: a heading over an empty list passes any test written
    /// with rich data only.
    /// </remarks>
    internal static RenderFragment Facts(
        IReadOnlyList<(string Label, string? Value, bool Norwegian)> facts, string? language) => builder =>
    {
        var shown = facts.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToList();

        if (shown.Count == 0)
        {
            return;
        }

        var reader = ReaderLanguage.Of(language);

        builder.OpenElement(0, "dl");
        builder.AddAttribute(1, "class", "munin-explorer-meta__grid");

        var seq = 10;

        foreach (var (label, value, norwegian) in shown)
        {
            builder.OpenElement(seq, "div");

            builder.OpenElement(seq + 1, "dt");
            builder.AddAttribute(seq + 2, "class", "headline headline-xxs margin--none");
            builder.AddContent(seq + 3, label);
            builder.CloseElement();

            builder.OpenElement(seq + 4, "dd");
            builder.AddAttribute(seq + 5, "lang", norwegian ? CatalogueProperties.Foreign("no", reader) : null);
            builder.AddContent(seq + 6, value);
            builder.CloseElement();

            builder.CloseElement();
            seq += 10;
        }

        builder.CloseElement();
    };

    /// <summary>One metadata group: its name, then its rows.</summary>
    internal static RenderFragment Group(PropertyGroup group, int level, string? language) => builder =>
    {
        var reader = ReaderLanguage.Of(language);

        builder.OpenElement(0, $"h{level}");
        builder.AddAttribute(1, "class", "headline headline-xxs margin--none munin-explorer-group");
        builder.AddAttribute(2, "lang", CatalogueProperties.Foreign(group.NameLanguage, reader));
        builder.AddContent(3, group.Name);
        builder.CloseElement();

        builder.OpenElement(4, "dl");
        builder.AddAttribute(5, "class", "munin-explorer-meta__grid");

        var seq = 10;

        foreach (var row in group.Rows)
        {
            builder.OpenElement(seq, "div");

            builder.OpenElement(seq + 1, "dt");
            builder.AddAttribute(seq + 2, "class", "headline headline-xxs margin--none");
            builder.AddAttribute(seq + 3, "lang", CatalogueProperties.Foreign(row.LabelLanguage, reader));
            builder.AddContent(seq + 4, row.Label);
            builder.CloseElement();

            builder.OpenElement(seq + 5, "dd");
            builder.AddAttribute(seq + 6, "lang", CatalogueProperties.Foreign(row.ValueLanguage, reader));
            builder.AddContent(seq + 7, row.Value);
            builder.CloseElement();

            builder.CloseElement();
            seq += 10;
        }

        builder.CloseElement();
    };
}
