using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.Display;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// Where a variable sits in the catalogue, drawn as an ordered list: kildetype, kilde, datasamling.
/// </summary>
/// <remarks>
/// One implementation and two callers — the panel an open row shows, and <see cref="VariableView"/>
/// — because the piece a second copy re-derives wrongly is <see cref="Steps"/>'s empty-level rule.
/// (Fhi.Metadata-35w0p.47)
/// </remarks>
internal static class KildeTrailBlock
{
    /// <summary>
    /// One step of the kilde trail, and whether it is Munin's Norwegian or our own prose.
    /// </summary>
    /// <param name="Text">The step's own words.</param>
    /// <param name="Norwegian">
    /// Whether the words are Munin's Norwegian rather than our prose, which decides whether the
    /// step is marked <c>lang="no"</c>.
    /// </param>
    /// <param name="OpensKilde">
    /// Whether this step opens the kilde. Runa links to its own kilde route; this component has no
    /// routes — the host owns the URL — so the affordance becomes the control the caller supplies,
    /// and a caller with none to offer gets the plain text every other step is.
    /// </param>
    internal sealed record Crumb(string Text, bool Norwegian, bool OpensKilde = false);

    /// <summary>
    /// The variable's place in the catalogue, widest first: kildetype, kilde, datasamling.
    /// </summary>
    /// <remarks>
    /// A level with nothing in it is left out rather than written as "Ikke oppgitt": a trail is
    /// read as a path, and a step saying nothing is worse than a shorter path. The last level is
    /// the one a variable can occupy several of at once, so it is counted rather than named.
    /// </remarks>
    /// <param name="detail">The variable being placed.</param>
    /// <param name="texts">The reader's language, for the two steps that are our own prose.</param>
    /// <param name="kildeTypeApiName">
    /// The kildetype facet's <c>displayName</c>, or null for a caller holding no filters payload —
    /// what <see cref="Texts.KildeTypeNameFromApi"/> falls back from. Not defaulted, because a
    /// caller omitting facets it holds spells one kildetype two ways. (Fhi.Metadata-3n6e1)
    /// </param>
    internal static IReadOnlyList<Crumb> Steps(
        VariableDetail detail, Texts texts, string? kildeTypeApiName)
    {
        var crumbs = new List<Crumb>(3);

        if (!string.IsNullOrWhiteSpace(detail.KildeType))
        {
            // The one step that is a vocabulary rather than a name out of the catalogue, so it
            // follows the reader's language either way — the API resolves it against
            // Accept-Language, and the shipped table behind that is the reader's language too.
            crumbs.Add(new Crumb(texts.KildeTypeNameFromApi(detail.KildeType, kildeTypeApiName),
                                 Norwegian: false));
        }

        if (!string.IsNullOrWhiteSpace(detail.KildeName))
        {
            var shortName = DisplayText.Trimmed(detail.KildeShortName);
            var sameThingTwice = shortName is null
                || string.Equals(shortName, detail.KildeName, StringComparison.OrdinalIgnoreCase);

            crumbs.Add(new Crumb(
                sameThingTwice ? detail.KildeName : $"{detail.KildeName} ({shortName})",
                Norwegian: true,
                OpensKilde: true));
        }

        var datasamlinger = NamedDatasamlinger(detail);

        if (datasamlinger.Count == 1)
        {
            crumbs.Add(new Crumb(datasamlinger[0].Name, Norwegian: true));
        }
        else if (datasamlinger.Count > 1)
        {
            // Our own prose about the catalogue rather than a name out of it, so it follows the
            // reader's language and is not marked Norwegian — as the kildetype step is not.
            crumbs.Add(new Crumb(texts.DatasamlingCountCrumb(datasamlinger.Count), Norwegian: false));
        }

        return crumbs;
    }

    /// <summary>
    /// The trail as an ordered list, one step per level, or "Ikke oppgitt" for no steps at all.
    /// </summary>
    /// <remarks>
    /// An <c>&lt;ol&gt;</c> rather than spans and a "›", which is read out as a symbol or skipped
    /// in silence. It carries no class of its own: the wrapper the caller puts it in is what a
    /// stylesheet reaches it by, and a caller drawing no chevrons still gets a list in order.
    /// </remarks>
    /// <param name="steps">What <see cref="Steps"/> answered for the variable being drawn.</param>
    /// <param name="texts">The reader's language, for the empty trail's one word.</param>
    /// <param name="pressKilde">
    /// What the kilde step does when pressed. Nothing passed, and that step is text: the one class
    /// this fragment can emit marks that button, so a caller without the control emits none.
    /// </param>
    internal static RenderFragment Write(
        IReadOnlyList<Crumb> steps, Texts texts,
        EventCallback<MouseEventArgs> pressKilde = default) => builder =>
    {
        if (steps.Count == 0)
        {
            builder.AddContent(0, texts.NotSpecified);

            return;
        }

        builder.OpenElement(1, "ol");

        foreach (var crumb in steps)
        {
            // A region per step, because these sequence numbers are written by hand rather than by
            // the Razor compiler: inside one the numbers below are a step's own, so a step that
            // changes shape — text where a button was — diffs against its own frames and no other.
            builder.OpenRegion(2);

            builder.OpenElement(0, "li");

            if (crumb.Norwegian)
            {
                builder.AddAttribute(1, "lang", "no");
            }

            if (crumb.OpensKilde && pressKilde.HasDelegate)
            {
                // Runa makes this step a link to its own kilde route. We have no routes — the host
                // owns the URL — so the same affordance is the control the caller supplies,
                // deliberately the same control as "Vis datakilde" further down its own panel.
                builder.OpenElement(2, "button");
                builder.AddAttribute(3, "class", "hd-button-reset munin-explorer-crumb");
                builder.AddAttribute(4, "type", "button");
                // No aria-expanded and no aria-controls. Both describe a control that discloses
                // something on the same screen, and this one replaces the list with the kilde's
                // own view — so aria-controls would name an element that is not in the document.
                builder.AddAttribute(5, "onclick", pressKilde);
                builder.AddContent(6, crumb.Text);
                builder.CloseElement();
            }
            else
            {
                builder.AddContent(7, crumb.Text);
            }

            builder.CloseElement();

            builder.CloseRegion();
        }

        builder.CloseElement();
    };

    /// <summary>
    /// Every datasamling the variable sits in, less the ones the payload left unnamed, falling
    /// back to the primary one when that leaves none.
    /// </summary>
    /// <remarks>
    /// The one place that decides what counts as a datasamling: count and list must both derive
    /// from it, or a count stands over a list of some other number. The fallback is inside it for
    /// that reason rather than beside it — a payload naming only the primary one is still one.
    /// </remarks>
    internal static IReadOnlyList<DatasamlingReference> NamedDatasamlinger(VariableDetail detail)
    {
        var named = detail.AllDatasamlinger
            .Where(datasamling => !string.IsNullOrWhiteSpace(datasamling.Name))
            .ToList();

        if (named.Count == 0 && !string.IsNullOrWhiteSpace(detail.DatasamlingName))
        {
            named.Add(new DatasamlingReference
            {
                Id = detail.DatasamlingId ?? Guid.Empty,
                Name = detail.DatasamlingName,
            });
        }

        return named;
    }
}
