using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// A detail page's hero row: the handful of facts the page leads with, under the name block and
/// above the body.
/// </summary>
/// <remarks>
/// <para>
/// Emits one <c>&lt;dl class="munin-explorer-page__facts"&gt;</c> holding a <c>&lt;div&gt;</c> per
/// fact, so the grid rule has one child per cell to lay out. Stiler gives that list six tracks at
/// desktop, three below 1080px and two below 600px, which is why the views name six facts each.
/// </para>
/// <para>
/// A summary, not a relocation: every fact here is still drawn in the section it belongs to
/// further down the page, and no key is added to <c>CatalogueProperties.Groups</c>'
/// <c>drawnElsewhere</c> on account of it. That set exists for the same fact drawn twice in the
/// same register under two labels; a hero row is a different register and is expected to repeat.
/// What must not differ is the wording, which is why each view resolves a hero value through the
/// same member its section below reads.
/// </para>
/// <para>
/// Public only because a Razor component must be, in the way <see cref="DetailSection"/> and
/// <see cref="DetailTrail"/> are: this is <see cref="DetailPage"/>'s own furniture and a host has
/// no reason to mount it. It ships no CSS, like everything else in this package.
/// </para>
/// </remarks>
public sealed class DetailFacts : ComponentBase
{
    /// <summary>
    /// The facts, in the order the page leads with them. Anything whose value the catalogue has not
    /// filled in is dropped, and a list with nothing left draws no element at all.
    /// </summary>
    [Parameter, EditorRequired]
    public IReadOnlyList<DetailFact> Facts { get; set; } = [];

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var shown = Facts.Where(fact => !string.IsNullOrWhiteSpace(fact.Value)).ToList();

        if (shown.Count == 0)
        {
            return;
        }

        builder.OpenElement(0, "dl");
        builder.AddAttribute(1, "class", "munin-explorer-page__facts");

        var seq = 10;

        foreach (var fact in shown)
        {
            builder.OpenElement(seq, "div");

            // No headline class on the dt, unlike the fact lists below: Stiler sizes this one
            // itself, and a second type rule on the same element is a fight the host loses.
            builder.OpenElement(seq + 1, "dt");
            builder.AddContent(seq + 2, fact.Label);
            builder.CloseElement();

            builder.OpenElement(seq + 3, "dd");
            Words(builder, seq + 4, fact.Value, fact.Lang);

            if (!string.IsNullOrWhiteSpace(fact.Note))
            {
                builder.OpenElement(seq + 8, "small");
                builder.AddAttribute(seq + 9, "lang", fact.NoteLang);
                builder.AddContent(seq + 10, fact.Note);
                builder.CloseElement();
            }

            builder.CloseElement();
            builder.CloseElement();

            seq += 20;
        }

        builder.CloseElement();
    }

    /// <summary>
    /// The value, wrapped in a marked span only where it is not in the reader's language. Consumes
    /// four sequence numbers from <paramref name="seq"/>.
    /// </summary>
    /// <remarks>
    /// A span rather than a <c>lang</c> on the <c>dd</c>, which is what the fact lists below use:
    /// the note shares that <c>dd</c> and is usually in the other language, so a mark on the
    /// parent would switch the voice for both.
    /// </remarks>
    private static void Words(RenderTreeBuilder builder, int seq, string? value, string? language)
    {
        if (language is null)
        {
            builder.AddContent(seq, value);
            return;
        }

        builder.OpenElement(seq + 1, "span");
        builder.AddAttribute(seq + 2, "lang", language);
        builder.AddContent(seq + 3, value);
        builder.CloseElement();
    }
}
