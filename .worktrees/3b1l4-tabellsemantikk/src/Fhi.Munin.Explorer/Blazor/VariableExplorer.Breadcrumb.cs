using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The trail over the results: where in the hierarchy the selection stands, and the way back up.</summary>
/// <remarks>
/// Runa's <c>HierarchyBreadcrumb</c>, in this component's vocabulary. It exists because the filter
/// panel cannot answer the question it answers: a kilde chosen three disclosures down, a delkilde
/// under it and a variabelgruppe under that are three pressed buttons in three collapsed
/// <c>&lt;details&gt;</c>, and the only sign of them on screen is the facet counts on the summary
/// lines. The trail puts the same selection above the results as a path, which is also the only
/// way to undo part of it without opening the panel again.
/// </remarks>
public partial class VariableExplorer
{
    /// <summary>
    /// The four levels of the catalogue hierarchy, outermost first.
    /// </summary>
    /// <remarks>
    /// The order is the trail's order and the order the members are compared in — a press on one
    /// level clears every level greater than it — so the members are not free to be reordered.
    /// Kildetype is deliberately not among them: it is a facet of its own in the panel rather than
    /// a step on the way to a kilde, and a reader who cleared the trail would not expect the type
    /// filter to go with it.
    /// </remarks>
    private enum HierarchyLevel
    {
        Kilde,
        Delkilde,
        Datasamling,
        Variabelgruppe
    }

    /// <summary>
    /// One step of the trail: which level it stands for, and how it reads.
    /// </summary>
    /// <remarks>
    /// <c>Norwegian</c> says whether <c>Text</c> is a name out of the catalogue or this component's
    /// own prose, exactly as the panel's <c>Crumb</c> does — a fallback label follows
    /// <see cref="Language"/> and must not be handed to a synthesiser as Norwegian.
    /// </remarks>
    private sealed record HierarchyCrumb(HierarchyLevel Level, string Text, bool Norwegian);

    /// <summary>
    /// The selected hierarchy as a path, outermost first, with the levels that have no selection
    /// left out.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A level with nothing chosen is skipped rather than drawn empty, the same rule the variable
    /// panel's kilde trail follows: a trail is read as a path, and a step saying nothing is worse
    /// than a shorter path. A selection can legitimately skip a level — a kilde and a
    /// variabelgruppe with no delkilde between them is an ordinary thing to pick — so the trail is
    /// the levels that are set and not the first N of them.
    /// </para>
    /// <para>
    /// Several values on one level collapse to the first name and a count of the rest, which is
    /// what Runa does: "Dødsårsaksregisteret (+2)". The alternative is a step whose width grows
    /// with the selection, and the number is the part a reader needs — the names are all still in
    /// the panel, one press away.
    /// </para>
    /// <para>
    /// Built per render rather than cached, for the reason <see cref="FacetGroups"/> is: it is at
    /// most four records, and a cached trail could describe a selection the rows on screen no
    /// longer came from.
    /// </para>
    /// </remarks>
    private IReadOnlyList<HierarchyCrumb> HierarchyCrumbs
    {
        get
        {
            List<HierarchyCrumb> crumbs = new(4);

            Add(HierarchyLevel.Kilde, _filter.KildeIds, KildeName, T.FieldSource);
            Add(HierarchyLevel.Delkilde, _filter.DelkildeIds, DelkildeName, T.FieldDelkilde);
            Add(HierarchyLevel.Datasamling, _filter.DatasamlingIds, DatasamlingName, T.FieldDataCollection);
            Add(HierarchyLevel.Variabelgruppe, _filter.VariabelgruppeIds, VariabelgruppeName, T.FieldVariableGroup);

            return crumbs;

            void Add(HierarchyLevel level, IReadOnlyList<Guid> chosen, Func<Guid, string?> name, string fallback)
            {
                if (chosen.Count == 0)
                {
                    return;
                }

                // The first id's name, and the level's own word when nothing on screen knows it —
                // see the name lookups below for when that happens. Never the id: a guid in a
                // trail is not information, and the step still has to be pressable to clear what
                // is under it.
                var known = name(chosen[0]);
                var text = known ?? fallback;

                crumbs.Add(new HierarchyCrumb(
                    level,
                    chosen.Count > 1 ? T.CrumbMore(text, chosen.Count - 1) : text,
                    Norwegian: known is not null));
            }
        }
    }

    /// <summary>
    /// The trail, as a list of steps and a control that clears the whole of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A list rather than a <c>&lt;nav&gt;</c>. A breadcrumb is a navigation landmark by
    /// convention, but this component already contributes a search landmark and up to two regions
    /// to somebody else's page, and two explorers mounted together would put two identically named
    /// navs in the landmark list with nothing to tell them apart — the search form avoids that by
    /// naming itself after this instance's title, which a trail cannot borrow without being called
    /// "Variabelutforsker". The <c>&lt;ol&gt;</c> carries the name instead, and is what says these
    /// are steps in order. No class on it, for the reason the panel's kilde trail has none: Stiler
    /// has no breadcrumb rule that can be read back off its compiled stylesheet, so a host draws
    /// the chevrons and a host that draws nothing gets a numbered list that still reads correctly.
    /// </para>
    /// <para>
    /// Every step is a button, including the last one, which has nothing under it to clear. That
    /// is not an oversight: pressing a step makes it the last step, so a last step drawn as plain
    /// text would take the control the reader just pressed out of the document and drop focus to
    /// <c>&lt;body&gt;</c> — the failure the pager's <c>aria-disabled</c> and the ever-present
    /// clear button both exist to avoid. It is inert rather than absent, and says so with
    /// <c>aria-current</c>; <see cref="ApplyFilterAsync"/> returns without a request when the
    /// filter it is handed is the one already in force.
    /// </para>
    /// <para>
    /// The clear control is the one place that rule cannot be kept. Clearing the hierarchy empties
    /// the trail, so the button leaves the document with it and focus lands on the page — and the
    /// alternative, an empty trail kept on screen for a lone × to sit in, is furniture describing a
    /// selection that no longer exists. What a screen-reader user gets instead is the result
    /// summary above the list, which is a polite live region and rewrites itself with the wider
    /// count the moment the fetch lands.
    /// </para>
    /// </remarks>
    private RenderFragment Breadcrumb(IReadOnlyList<HierarchyCrumb> crumbs) => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "munin-explorer-breadcrumb");

        builder.OpenElement(2, "ol");
        builder.AddAttribute(3, "aria-label", T.HierarchyTrail);

        for (var index = 0; index < crumbs.Count; index++)
        {
            var crumb = crumbs[index];
            var current = index == crumbs.Count - 1;

            builder.OpenElement(4, "li");
            // Keyed by the level rather than by position. Not for a press on a step, which only
            // ever drops steps off the end and patches the same either way — for a level
            // appearing in the middle, which the trail allows: a selection may skip a level, so
            // choosing a delkilde under a kilde and variabelgruppe already picked inserts a step
            // at index 1 and shifts the rest along. Positionally that patches the button under
            // the reader's finger into the step that took its place; by level it moves instead.
            builder.SetKey(crumb.Level);

            builder.OpenElement(5, "button");
            builder.AddAttribute(6, "class", "hd-button-reset munin-explorer-crumb");
            builder.AddAttribute(7, "type", "button");

            // Null on every step but the last, so the attribute is left out rather than spelled
            // "false" — the same treatment the sort headers' aria-current gets.
            builder.AddAttribute(8, "aria-current", current ? "true" : null);

            // What pressing it does, which the name on its own does not say. It starts with the
            // visible text so a speech-input user saying what they can see still hits the button
            // (WCAG 2.5.3), and the last step has none because pressing it does nothing.
            builder.AddAttribute(9, "aria-label", current ? null : T.CrumbLabel(crumb.Text));

            builder.AddAttribute(10, "onclick",
                EventCallback.Factory.Create(this, () => NarrowToAsync(crumb.Level)));

            if (crumb.Norwegian)
            {
                // The lang goes on a span around the catalogue's words alone, never on the
                // button — the result row's name does the same. The button owns the aria-label
                // above, which is this component's prose in the UI's language, and an accessible
                // name is announced in the computed language of the element that owns it: a
                // langed button would have an English reader hear "Tromsøundersøkelsen – remove
                // the levels below" in a Norwegian voice, which is lang="no" applied backwards.
                builder.OpenElement(11, "span");
                builder.AddAttribute(12, "lang", "no");
                builder.AddContent(13, crumb.Text);
                builder.CloseElement();
            }
            else
            {
                builder.AddContent(14, crumb.Text);
            }

            builder.CloseElement();

            builder.CloseElement();
        }

        builder.CloseElement();

        // Outside the list: it is not a step on the path, and a screen reader counting "list, 4
        // items" must not be counting the control that empties it.
        // Numbered above the crumb loop's highest, not from where the loop's outer element left
        // off: the numbers inside the loop's span reach 14, and a fragment that reads as one
        // ascending run is the only cue a later reader has that nothing collides. The two ranges
        // are diffed apart today, so a collision here would be silent until the markup is renested.
        builder.OpenElement(15, "button");
        builder.AddAttribute(16, "class", "hd-button-reset munin-explorer-breadcrumb__clear");
        builder.AddAttribute(17, "type", "button");
        // The visible glyph is a multiplication sign, which is decoration rather than a word, so
        // the accessible name is spelled out. It is the whole name and not a suffix, because there
        // is no visible text for it to have to start with.
        builder.AddAttribute(18, "aria-label", T.ClearHierarchy);
        builder.AddAttribute(19, "onclick", EventCallback.Factory.Create(this, ClearHierarchyAsync));
        builder.AddContent(20, "×");
        builder.CloseElement();

        builder.CloseElement();
    };

    /// <summary>
    /// Narrow back to <paramref name="level"/>: keep it and everything above it, drop everything
    /// under it.
    /// </summary>
    /// <remarks>
    /// The level pressed keeps its own selection — a press on the kilde step means "show me this
    /// kilde", not "forget it" — so <see cref="VariableFilter.KildeIds"/> is never cleared here at
    /// all: nothing sits above the outermost level for a press on it to keep.
    /// <para>
    /// Every other facet is untouched, deliberately. A datatype or a kodeverk is not part of the
    /// path, and taking it out because a reader stepped back up the hierarchy would be a filter
    /// disappearing with no control having said so.
    /// </para>
    /// </remarks>
    private Task NarrowToAsync(HierarchyLevel level)
    {
        return ApplyFilterAsync(_filter with
        {
            DelkildeIds = Keep(HierarchyLevel.Delkilde, _filter.DelkildeIds),
            DatasamlingIds = Keep(HierarchyLevel.Datasamling, _filter.DatasamlingIds),
            VariabelgruppeIds = Keep(HierarchyLevel.Variabelgruppe, _filter.VariabelgruppeIds)
        });

        IReadOnlyList<Guid> Keep(HierarchyLevel of, IReadOnlyList<Guid> ids) => of <= level ? ids : [];
    }

    /// <summary>Drop the whole hierarchy selection, and nothing else.</summary>
    /// <remarks>
    /// The four levels only. "Fjern alle filtre" in the panel is the control that clears
    /// everything; this one is for the reader who has narrowed deep into one kilde and wants the
    /// datatype, the kodeverk and the date range they also chose to survive it.
    /// </remarks>
    private Task ClearHierarchyAsync() =>
        ApplyFilterAsync(_filter with
        {
            KildeIds = [],
            DelkildeIds = [],
            DatasamlingIds = [],
            VariabelgruppeIds = []
        });

    /// <summary>
    /// A kilde's name, or null when nothing on screen knows it.
    /// </summary>
    /// <remarks>
    /// The facets first, because they are the whole selectable list and are refreshed with every
    /// filter change; the rows second, for the moment before the first facet answer arrives and for
    /// a refresh that failed. Both can miss — the facets are cross-filtered, so a value the reader
    /// selected can be absent from the payload describing what that selection leaves — which is why
    /// the trail has a fallback label rather than assuming a name.
    /// </remarks>
    private string? KildeName(Guid id) =>
        Trimmed(_facets?.Kilder.FirstOrDefault(kilde => kilde.Id == id)?.Name)
        ?? RowName(row => row.KildeId == id ? row.KildeName : null);

    /// <summary>
    /// A delkilde's name, or null when the facets do not carry it.
    /// </summary>
    /// <remarks>
    /// The facets are the only source: <see cref="VariableSummary"/> names a variable's kilde, its
    /// datasamling and its variabelgruppe, but not the delkilde in between, so there is no row to
    /// fall back to.
    /// </remarks>
    private string? DelkildeName(Guid id) =>
        Trimmed(_facets?.Delkilder.FirstOrDefault(delkilde => delkilde.Id == id)?.Name);

    /// <summary>
    /// A datasamling's name, or null when no row on screen carries it.
    /// </summary>
    /// <remarks>
    /// The rows are the only source here, and the reverse of the delkilde's case: nothing in
    /// <see cref="FilterOptions"/> offers datasamlinger as a facet — see the remarks on
    /// <see cref="VariableFilter.DatasamlingIds"/> — while every row a datasamling filter leaves
    /// belongs to it and says so. Which means the name is there whenever the filter matched
    /// anything at all, and absent exactly when it matched nothing.
    /// </remarks>
    private string? DatasamlingName(Guid id) =>
        RowName(row => row.DatasamlingId == id ? row.DatasamlingName : null);

    /// <summary>A variabelgruppe's name — the facets, then the rows, as the kilde's is.</summary>
    private string? VariabelgruppeName(Guid id) =>
        Trimmed(_facets?.Variabelgrupper.FirstOrDefault(gruppe => gruppe.Id == id)?.Name)
        ?? RowName(row => row.VariabelgruppeId == id ? row.VariabelgruppeName : null);

    /// <summary>The first name the visible rows can supply for a value, or null if none can.</summary>
    private string? RowName(Func<VariableSummary, string?> name) =>
        _result?.Items.Select(row => Trimmed(name(row))).FirstOrDefault(found => found is not null);
}
