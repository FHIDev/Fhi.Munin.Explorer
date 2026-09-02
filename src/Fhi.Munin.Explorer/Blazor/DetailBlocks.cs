using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

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

    /// <summary>
    /// A row's value as one <c>dd</c> per language the catalogue holds it in, and the sequence
    /// number the caller continues from.
    /// </summary>
    /// <remarks>
    /// Named only where there is more than one: <c>lang</c> speaks to a screen reader alone. The
    /// name sits outside the marked span so it is not announced in the language it names, and is a
    /// <c>p</c> so a host with no rule for the class still gets one language per line.
    /// </remarks>
    internal static int Values(RenderTreeBuilder builder, int seq, PropertyRow row, string reader, Texts text)
    {
        foreach (var slot in row.Values)
        {
            builder.OpenElement(seq, "dd");

            if (row.Values.Count == 1)
            {
                builder.AddAttribute(seq + 1, "lang", CatalogueProperties.Foreign(slot.Language, reader));
                Text(builder, seq + 2, slot.Text, row.Href);
            }
            else
            {
                builder.OpenElement(seq + 10, "p");
                builder.AddAttribute(seq + 11, "class", "munin-explorer-meta__language");
                builder.AddContent(seq + 12, text.LanguageName(slot.Language));
                builder.CloseElement();

                builder.OpenElement(seq + 13, "span");
                builder.AddAttribute(seq + 14, "lang", CatalogueProperties.Foreign(slot.Language, reader));
                Text(builder, seq + 15, slot.Text, row.Href);
                builder.CloseElement();
            }

            builder.CloseElement();
            seq += 30;
        }

        return seq;
    }

    /// <summary>
    /// A value, as a link where the catalogue types the property as a URL. Consumes five sequence
    /// numbers from <paramref name="seq"/>.
    /// </summary>
    /// <remarks>
    /// A field that exists to be followed should be followable (FHIDev/Munin#5385). <c>rel</c>
    /// guards the middle-click and ctrl-click paths; there is no <c>target</c>, so a reader stays
    /// on the page they were reading.
    /// </remarks>
    private static void Text(RenderTreeBuilder builder, int seq, string value, string? href)
    {
        if (href is null)
        {
            builder.AddContent(seq, value);
            return;
        }

        builder.OpenElement(seq + 1, "a");
        builder.AddAttribute(seq + 2, "href", href);
        builder.AddAttribute(seq + 3, "rel", "noopener noreferrer");
        builder.AddContent(seq + 4, value);
        builder.CloseElement();
    }

    /// <summary>One metadata group: its name, then its rows.</summary>
    internal static RenderFragment Group(PropertyGroup group, int level, string? language) => builder =>
    {
        var reader = ReaderLanguage.Of(language);
        var text = Texts.For(language);

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

            seq = Values(builder, seq + 5, row, reader, text);

            builder.CloseElement();
        }

        builder.CloseElement();
    };
}
