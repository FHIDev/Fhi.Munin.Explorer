using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.Display;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// Where a variable sits in the catalogue, drawn as an ordered list: kildetype, kilde, datasamling.
/// </summary>
/// <remarks>
/// One implementation and two callers — the panel an open row shows, and <see cref="VariableView"/>,
/// where "where does this variable come from" is the first question the page answers. A second copy
/// drifts, and the piece that drifts first is <see cref="Steps"/>'s empty-level rule, which nobody
/// re-derives correctly. (Fhi.Metadata-35w0p.47)
/// </remarks>
internal static class KildeTrail
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
    /// Whether this step opens the kilde panel. Runa makes the kilde a link to its own kilde route;
    /// this component has no routes — the host owns the URL — so the same affordance becomes the
    /// control that discloses the kilde in place. A reader clicks the kilde and gets the kilde
    /// either way; only the mechanism differs, and the mechanism is the one thing an embedded
    /// component cannot borrow. A caller with no such control to offer passes no press to
    /// <see cref="Write"/>, and the step is the plain text every other step is.
    /// </param>
    internal sealed record Crumb(string Text, bool Norwegian, bool OpensKilde = false);

    /// <summary>
    /// The variable's place in the catalogue, widest first: kildetype, kilde, datasamling.
    /// </summary>
    /// <remarks>
    /// A level with nothing in it is left out rather than written as "Ikke oppgitt": a trail is
    /// read as a path, and a step saying nothing is worse than a shorter path. All three missing
    /// leaves an empty list, which <see cref="Write"/> reports as "Ikke oppgitt" once — and which a
    /// page-shaped caller draws no section for at all.
    /// <para>
    /// The last step is the one level a variable can occupy several of at once, and a step is one
    /// place: it counts them rather than naming them, and the list beside the trail names every one.
    /// </para>
    /// </remarks>
    /// <param name="detail">The variable being placed.</param>
    /// <param name="texts">The reader's language, for the two steps that are our own prose.</param>
    /// <param name="kildeTypeApiName">
    /// The kildetype facet's <c>displayName</c>, for a caller holding the filters payload; null
    /// where it has none, which is what <see cref="Texts.KildeTypeNameFromApi"/> falls back from.
    /// </param>
    internal static IReadOnlyList<Crumb> Steps(
        VariableDetail detail, Texts texts, string? kildeTypeApiName = null)
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

        var datasamlinger = DatasamlingNames(detail);

        if (datasamlinger.Count == 1)
        {
            crumbs.Add(new Crumb(datasamlinger[0], Norwegian: true));
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
    /// An <c>&lt;ol&gt;</c> and no class name, for the reason the filter panel's nested
    /// <c>&lt;ul&gt;</c> carries none: Stiler has no breadcrumb rule that can be read back off its
    /// compiled stylesheet, and a name it has never heard of renders as a raw browser default. The
    /// list is also what says "these are steps in order" without a separator character — a "›"
    /// between spans is either read out as a symbol or skipped in silence, and neither says the
    /// kilde sits inside the kildetype. A host draws the chevrons; a host that draws nothing gets a
    /// numbered list that still reads correctly.
    /// </remarks>
    /// <param name="steps">What <see cref="Steps"/> answered for the variable being drawn.</param>
    /// <param name="texts">The reader's language, for the empty trail's one word.</param>
    /// <param name="pressKilde">
    /// What the kilde step does when pressed. Nothing passed, and that step is text: the only
    /// class this fragment can emit is the one that marks the button as pressable, so a caller
    /// without the control emits no class name at all.
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
            builder.OpenElement(2, "li");

            if (crumb.Norwegian)
            {
                builder.AddAttribute(3, "lang", "no");
            }

            if (crumb.OpensKilde && pressKilde.HasDelegate)
            {
                // Runa makes this step a link to its own kilde route. We have no routes — the host
                // owns the URL — so the same affordance is the control the caller supplies,
                // deliberately the same control as "Vis datakilde" further down its own panel.
                builder.OpenElement(5, "button");
                builder.AddAttribute(6, "class", "hd-button-reset munin-explorer-crumb");
                builder.AddAttribute(7, "type", "button");
                // No aria-expanded and no aria-controls. Both describe a control that discloses
                // something on the same screen, and this one does not: it replaces the list with
                // the kilde's own view. aria-controls would also dangle — the element it named
                // does not exist while this button is the thing on screen.
                builder.AddAttribute(10, "onclick", pressKilde);
                builder.AddContent(11, crumb.Text);
                builder.CloseElement();
            }
            else
            {
                builder.AddContent(4, crumb.Text);
            }

            builder.CloseElement();
        }

        builder.CloseElement();
    };

    /// <summary>
    /// Every datasamling the variable sits in, less the ones the payload left unnamed.
    /// </summary>
    /// <remarks>
    /// The one place that decides what counts as a datasamling, because three renders read it: this
    /// trail's last step, the guard on the row, and the panel's own list of them. Written out twice
    /// it drifts into a count standing over a list of some other number.
    /// </remarks>
    internal static IReadOnlyList<DatasamlingReference> NamedDatasamlinger(VariableDetail detail) =>
        detail.AllDatasamlinger
            .Where(datasamling => !string.IsNullOrWhiteSpace(datasamling.Name))
            .ToList();

    /// <summary>
    /// Every datasamling the variable sits in, by name.
    /// </summary>
    /// <remarks>
    /// <see cref="VariableDetail.AllDatasamlinger"/> rather than the primary one alone, for the
    /// reason the variabelgruppe list beside the trail reads its own: a variable in nineteen of
    /// them written up under one reads as singular rather than as incomplete. The primary name is
    /// the fallback for a payload that carries no list.
    /// </remarks>
    private static IReadOnlyList<string> DatasamlingNames(VariableDetail detail)
    {
        var names = NamedDatasamlinger(detail)
            .Select(datasamling => datasamling.Name)
            .ToList();

        if (names.Count == 0 && !string.IsNullOrWhiteSpace(detail.DatasamlingName))
        {
            names.Add(detail.DatasamlingName);
        }

        return names;
    }
}
