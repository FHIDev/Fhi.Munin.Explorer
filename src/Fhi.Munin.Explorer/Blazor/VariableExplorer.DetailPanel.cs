using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The panel that opens under a selected row: what the variable is, and what its data holds.</summary>
public partial class VariableExplorer
{

    /// <summary>Whether this row is the one whose detail panel is open.</summary>
    private bool IsSelected(VariableSummary v) => _selectedId == v.Id;

    /// <summary>The open panel's description, trimmed, or null while there is none to show.</summary>
    private string? DetailDescription => Trimmed(_detail?.Description);

    /// <summary>
    /// Whether the card draws the description the search listed it with.
    /// </summary>
    /// <remarks>
    /// It stops as soon as the panel underneath is showing the same sentence out of the detail
    /// payload, which is the fuller and the more authoritative of the two — the search returns the
    /// description of the row, the detail returns the one on the version being shown. Printing both
    /// would put the same paragraph on screen twice inside one card. The card keeps its own until
    /// the fetch lands, so nothing blinks out while the panel is loading.
    /// </remarks>
    private bool ShowRowDescription(VariableSummary v) =>
        !string.IsNullOrWhiteSpace(v.Description) && !(IsSelected(v) && DetailDescription is not null);

    private string DetailBusy => _detailLoading ? "true" : "false";

    private string DetailToggleText(VariableSummary v) => IsSelected(v) ? T.HideDetails : T.ShowDetails;

    private string DetailExpanded(VariableSummary v) => IsSelected(v) ? "true" : "false";

    /// <summary>
    /// The panel's id while it exists, and nothing at all while it does not.
    /// </summary>
    /// <remarks>
    /// A closed panel is not in the document, and <c>aria-controls</c> pointing at an element that
    /// is not there is a dangling reference — announced by some readers as a control that opens
    /// nothing. <c>aria-expanded</c> is what says the button is a disclosure; this only says which
    /// element it revealed.
    /// </remarks>
    private string? DetailControls(VariableSummary v) => IsSelected(v) ? DetailId(v) : null;

    /// <summary>
    /// The toggle's accessible name: its own words, then the variable's.
    /// </summary>
    /// <remarks>
    /// Twenty-five buttons all called "Vis detaljer" say nothing about which row they open when a
    /// screen reader lists them out of context. Pointing at both elements names it "Vis detaljer
    /// 1. Tale" and keeps each half in its own language, which an <c>aria-label</c> could not do:
    /// the words are ours and follow <see cref="Language"/>, the variable's name is Munin's and is
    /// Norwegian whatever the surrounding UI is. It starts with the visible text, so speech input
    /// still reaches it by what is on screen (WCAG 2.5.3).
    /// </remarks>
    private string DetailToggleLabelledBy(VariableSummary v) => $"{DetailToggleId(v)} {RowHeadingId(v)}";

    /// <summary>What the panel's status line says: that it is loading, or why it is empty.</summary>
    private string? DetailStatus => _detailLoading ? T.DetailLoading : _detailError;

    /// <summary>
    /// The status line's class: Stiler's muted caption while it is loading, its infobox when
    /// something went wrong.
    /// </summary>
    /// <remarks>
    /// One element in one place rather than two that swap, so the polite live region it carries
    /// survives the change. A failure is therefore announced — it replaces text in a region that
    /// is already in the document, which is the arrangement a screen reader reads reliably. The
    /// loading message itself arrives with the panel and may not be, which is the same trade the
    /// component's own alert region documents; the button's <c>aria-expanded</c> is what reports
    /// the press.
    /// </remarks>
    private string DetailStatusClass => _detailError is null ? "caption" : "infobox infobox--bg-yellow";

    /// <summary>One step of the kilde trail, and whether it is Munin's Norwegian or our own prose.</summary>
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
    /// component cannot borrow.
    /// </param>
    private sealed record Crumb(string Text, bool Norwegian, bool OpensKilde = false);

    /// <summary>
    /// The variable's place in the catalogue, widest first: kildetype, kilde, datasamling.
    /// </summary>
    /// <remarks>
    /// A level with nothing in it is left out rather than written as "Ikke oppgitt": a trail is
    /// read as a path, and a step saying nothing is worse than a shorter path. All three missing
    /// leaves an empty list, which the markup reports as "Ikke oppgitt" once.
    /// </remarks>
    private IReadOnlyList<Crumb> KildeCrumbs(VariableDetail detail)
    {
        var crumbs = new List<Crumb>(3);

        if (!string.IsNullOrWhiteSpace(detail.KildeType))
        {
            // The one step that is our prose rather than a name out of the catalogue — it follows
            // Language, so it is the one step not marked as Norwegian.
            crumbs.Add(new Crumb(T.KildeTypeLabel(detail.KildeType, detail.KildeType), Norwegian: false));
        }

        if (!string.IsNullOrWhiteSpace(detail.KildeName))
        {
            var shortName = Trimmed(detail.KildeShortName);
            var sameThingTwice = shortName is null
                || string.Equals(shortName, detail.KildeName, StringComparison.OrdinalIgnoreCase);

            crumbs.Add(new Crumb(
                sameThingTwice ? detail.KildeName : $"{detail.KildeName} ({shortName})",
                Norwegian: true,
                OpensKilde: true));
        }

        if (!string.IsNullOrWhiteSpace(detail.DatasamlingName))
        {
            crumbs.Add(new Crumb(detail.DatasamlingName, Norwegian: true));
        }

        return crumbs;
    }

    /// <summary>
    /// Every variabelgruppe the variable is in, by name.
    /// </summary>
    /// <remarks>
    /// <see cref="VariableDetail.AllVariabelgrupper"/> rather than the primary one alone, because a
    /// variable in three groups listed under one is a half-truth the payload already has the answer
    /// to. The primary name is the fallback for a payload that carries no list.
    /// </remarks>
    private static IReadOnlyList<string> VariabelgruppeNames(VariableDetail detail)
    {
        var names = detail.AllVariabelgrupper
            .Select(gruppe => gruppe.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (names.Count == 0 && !string.IsNullOrWhiteSpace(detail.VariabelgruppeName))
        {
            names.Add(detail.VariabelgruppeName);
        }

        return names;
    }

    /// <summary>One value in the panel: the catalogue's own words, or "Ikke oppgitt".</summary>
    /// <remarks>
    /// The same rule the result cards follow — a missing value is written out for everyone rather
    /// than drawn as an em dash, and a value that is there is marked as Norwegian while the label
    /// beside it follows <see cref="Language"/>.
    /// </remarks>
    private RenderFragment DetailValue(string? value) => builder =>
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            builder.AddContent(0, T.NotSpecified);

            return;
        }

        builder.OpenElement(1, "span");
        builder.AddAttribute(2, "lang", "no");
        builder.AddContent(3, value);
        builder.CloseElement();
    };

    /// <summary>
    /// The kilde trail as an ordered list, one step per level.
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
    private RenderFragment KildeTrail(VariableDetail detail) => builder =>
    {
        var crumbs = KildeCrumbs(detail);

        if (crumbs.Count == 0)
        {
            builder.AddContent(0, T.NotSpecified);

            return;
        }

        builder.OpenElement(1, "ol");

        foreach (var crumb in crumbs)
        {
            builder.OpenElement(2, "li");

            if (crumb.Norwegian)
            {
                builder.AddAttribute(3, "lang", "no");
            }

            if (crumb.OpensKilde)
            {
                // Runa makes this step a link to its own kilde route. We have no routes — the host
                // owns the URL — so the same affordance is the control that discloses the kilde
                // below instead. Clicking the kilde gets you the kilde either way.
                //
                // It is the same control as the "Vis datakilde" button further down, deliberately:
                // two ways to the same panel, one of them on the thing itself, which is where a
                // reader looks first. aria-expanded and aria-controls say so, so a screen reader is
                // not told about two unrelated buttons that happen to do the same thing.
                builder.OpenElement(5, "button");
                builder.AddAttribute(6, "class", "hd-button-reset variable-explorer-crumb");
                builder.AddAttribute(7, "type", "button");
                // No aria-expanded and no aria-controls. Both describe a control that discloses
                // something on the same screen, and this one does not: it replaces the list with
                // the kilde's own view. aria-controls would also dangle — the element it named
                // does not exist while this button is the thing on screen.
                builder.AddAttribute(10, "onclick",
                    EventCallback.Factory.Create(this, () => ToggleSourceAsync(SourceKind.Kilde)));
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

    /// <summary>A list of names out of the catalogue, or "Ikke oppgitt" when there are none.</summary>
    private RenderFragment NameList(IReadOnlyList<string> names) => builder =>
    {
        if (names.Count == 0)
        {
            builder.AddContent(0, T.NotSpecified);

            return;
        }

        builder.OpenElement(1, "ul");

        foreach (var name in names)
        {
            builder.OpenElement(2, "li");
            builder.AddAttribute(3, "lang", "no");
            builder.AddContent(4, name);
            builder.CloseElement();
        }

        builder.CloseElement();
    };

    /// <summary>Which kodeverk link a code list belongs to.</summary>
    /// <remarks>
    /// The pair the endpoint is addressed by, and the only thing that identifies a link: a variable
    /// can hold two links of one kind, and two variables can hold the same reference under
    /// different kinds. Its position in the payload is not used, because the payload can be fetched
    /// again — a list open on a reference has to stay open on that reference and not on whatever
    /// ends up in its place.
    /// </remarks>
    private readonly record struct KodeverkKey(string Type, string Reference)
    {
        public static KodeverkKey Of(KodeverkLink link) => new(link.KodeverkType, link.KodeverkReference);
    }

    /// <summary>Open this link's code list, or close the one already open.</summary>
    /// <remarks>
    /// Several lists can be open at once, unlike the kilde and datasamling panels: those answer the
    /// same question about the same variable twice, where two kodeverk are two different things a
    /// reader may well want side by side.
    /// <para>
    /// A list closed and opened again is not fetched again — what came back is kept for as long as
    /// the panel it hangs in. A list that <em>failed</em> is, because re-pressing the control is the
    /// only retry a reader has and there is no answer being cached over.
    /// </para>
    /// </remarks>
    private async Task ToggleCodesAsync(KodeverkLink link)
    {
        var key = KodeverkKey.Of(link);

        if (!_openCodes.Add(key))
        {
            _openCodes.Remove(key);

            return;
        }

        if (_codes.ContainsKey(key) || _codesLoading.Contains(key))
        {
            return;
        }

        await LoadCodesAsync(key);
    }

    /// <summary>Fetch one link's codes into the list that was just opened.</summary>
    /// <remarks>
    /// Guarded on the generation, the same as the detail and the owner panel, and for the reason
    /// they are: the answer arrives after a yield, by which time the panel it was asked for can
    /// have been closed and another variable's opened in its place.
    /// <para>
    /// Null is "the catalogue publishes no codes for this link" rather than a failure — see
    /// <see cref="IMuninExplorerClient.GetKodeverkCodesAsync"/> — so it is cached as an empty list
    /// and reported as one. Caching it is what keeps a link the register does not know from
    /// re-asking on every expand.
    /// </para>
    /// </remarks>
    private async Task LoadCodesAsync(KodeverkKey key)
    {
        if (_selectedId is not { } variableId)
        {
            return;
        }

        var generation = _codesGeneration;

        _codesError.Remove(key);
        _codesLoading.Add(key);
        StateHasChanged();

        try
        {
            var codes = await Client.GetKodeverkCodesAsync(variableId, key.Type, key.Reference);

            if (_codesGeneration != generation)
            {
                return;
            }

            _codes[key] = codes?.Codes ?? [];
        }
        catch (Exception)
        {
            if (_codesGeneration == generation)
            {
                _codesError[key] = T.CodesError;
            }
        }
        finally
        {
            if (_codesGeneration == generation)
            {
                _codesLoading.Remove(key);
            }
        }
    }

    /// <summary>Close every open code list and forget what was fetched for them.</summary>
    /// <remarks>
    /// Bumps the generation for the reason <see cref="ClearSource"/> does: closing disowns whatever
    /// is still in flight, and the reference it was asked for can come back while the generation it
    /// claimed cannot.
    /// </remarks>
    private void ClearCodes()
    {
        _openCodes.Clear();
        _codes.Clear();
        _codesLoading.Clear();
        _codesError.Clear();

        _codesGeneration++;
    }

    /// <summary>
    /// The kodeverk block as a section, for the whole-variable view to place among its own.
    /// </summary>
    /// <remarks>
    /// A heading and then the same fragment the Data tab draws. The alternative was a second copy
    /// inside <see cref="VariableView"/>, which is a section to fix twice and two chances for the
    /// panel and the full view to disagree about the same links.
    /// </remarks>
    private RenderFragment KodeverkSection(VariableDetail detail) => builder =>
    {
        if (detail.KodeverkLinks.Count == 0)
        {
            return;
        }

        builder.OpenElement(0, $"h{Math.Min(RowLevel + 1, 6)}");
        builder.AddAttribute(1, "class", "headline headline-s");
        builder.AddContent(2, T.HeadingKodeverk);
        builder.CloseElement();

        builder.AddContent(3, KodeverkGroups(detail));
    };

    /// <summary>
    /// The kodeverk the variable's values are drawn from, grouped by the kind of link they are.
    /// </summary>
    /// <remarks>
    /// Runa's arrangement: a heading per kind — Kildekodeverk, Administrativt kodeverk, Helsefaglig
    /// kodeverk — and under it one line per link, each carrying its own reference and, where the
    /// codes can be had, the control that fetches them.
    /// <para>
    /// The kind is a heading rather than a prefix on every line because it is what a bare reference
    /// is missing: "2336" says nothing on its own, and the same catalogue holds kildekodeverk
    /// defined by the kilde, national administrative code systems and clinical classifications.
    /// </para>
    /// <para>
    /// A link the API resolved no name for is drawn as "Ukjent navn" with its reference underneath,
    /// labelled — <em>not</em> with the reference standing in for the name. That fallback is what
    /// put "Kildekodeverk: 2336" on screen for the variable this panel was measured against, which
    /// reads as the kodeverk being called 2336 rather than as its name being unknown. The reference
    /// is on every line either way, because it is the thing a reader can look up.
    /// </para>
    /// <para>
    /// Groups come out in the order the payload first mentions each kind, not in an order of ours:
    /// the API decides which links a variable has and in what sequence, and a fixed order here
    /// would be a second opinion about a list this component does not own.
    /// </para>
    /// </remarks>
    private RenderFragment KodeverkGroups(VariableDetail detail) => builder =>
    {
        if (detail.KodeverkLinks.Count == 0)
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "class", "caption");
            builder.AddContent(2, T.NoKodeverk);
            builder.CloseElement();

            return;
        }

        // Numbered before grouping, so a line's number is its place in the payload rather than its
        // place under a heading — which is what keeps the ids unique across the whole panel.
        var links = detail.KodeverkLinks.Select((link, index) => (Link: link, Index: index));

        var seq = 10;

        foreach (var group in links.GroupBy(entry => entry.Link.KodeverkType, StringComparer.OrdinalIgnoreCase))
        {
            builder.OpenElement(seq, $"h{RowLevel}");
            builder.AddAttribute(seq + 1, "class", "headline headline-xxs margin--none variable-explorer-group");
            builder.AddContent(seq + 2, T.KodeverkTypeLabel(group.Key));
            builder.CloseElement();

            builder.OpenElement(seq + 3, "ul");
            builder.AddAttribute(seq + 4, "class", "variable-explorer-kodeverk");
            builder.AddContent(seq + 5, KodeverkItems(group));
            builder.CloseElement();

            seq += 10;
        }
    };

    /// <summary>One line per link within a kind, with its reference and its codes.</summary>
    private RenderFragment KodeverkItems(IEnumerable<(KodeverkLink Link, int Index)> links) => builder =>
    {
        var seq = 0;

        foreach (var (link, index) in links)
        {
            var key = KodeverkKey.Of(link);

            builder.OpenElement(seq, "li");
            // Keyed on the link rather than left to positional diffing: each line owns an expanded
            // or collapsed code list, and two links reordered under one heading would otherwise
            // swap the lists open beneath them.
            builder.SetKey(key);
            builder.AddAttribute(seq + 1, "class", "variable-explorer-kodeverk__item");

            builder.OpenElement(seq + 2, "p");
            builder.AddAttribute(seq + 3, "class", "variable-explorer-kodeverk__name");
            builder.AddAttribute(seq + 4, "id", KodeverkNameId(index));

            if (Trimmed(link.DisplayName) is { } name)
            {
                // The catalogue's own name, so it stays Norwegian whatever the UI language is —
                // the rule the kilde trail and the variable's own name already follow.
                builder.AddAttribute(seq + 5, "lang", "no");
                builder.AddContent(seq + 6, name);
            }
            else
            {
                builder.AddContent(seq + 7, T.KodeverkUnnamed);
            }

            builder.CloseElement();

            builder.OpenElement(seq + 8, "p");
            builder.AddAttribute(seq + 9, "class", "caption variable-explorer-kodeverk__reference");
            builder.AddContent(seq + 10, $"{T.FieldKodeverkReference}: {link.KodeverkReference}");
            builder.CloseElement();

            // No button where the API serves no codes. HelsefagligKodeverk links are the case that
            // matters — the endpoint answers 404 for every one of them — and a control that could
            // only ever report "no codes" is worse than no control at all.
            if (link.HasCodeValues)
            {
                builder.AddContent(seq + 11, KodeverkCodesToggle(link, key, index));
            }

            builder.CloseElement();

            seq += 20;
        }
    };

    /// <summary>The "Vis koder" control and, once it has been pressed, what came back.</summary>
    /// <remarks>
    /// The panel is rendered only while it is open, so <c>aria-controls</c> is set only then — the
    /// rule the kilde and datasamling toggles follow, for the same reason: an id naming an element
    /// that is not in the document is worse than no id.
    /// </remarks>
    private RenderFragment KodeverkCodesToggle(KodeverkLink link, KodeverkKey key, int index) => builder =>
    {
        var open = _openCodes.Contains(key);

        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "class", "hd-button-square button-square--ghost margin-bottom");
        builder.AddAttribute(2, "type", "button");
        builder.AddAttribute(3, "aria-expanded", open ? "true" : "false");
        builder.AddAttribute(4, "aria-controls", open ? KodeverkCodesId(index) : null);
        builder.AddAttribute(5, "onclick", EventCallback.Factory.Create(this, () => ToggleCodesAsync(link)));
        builder.AddContent(6, open ? T.HideCodes : T.ShowCodes);
        builder.CloseElement();

        if (!open)
        {
            return;
        }

        builder.OpenElement(7, "div");
        builder.AddAttribute(8, "id", KodeverkCodesId(index));
        builder.AddAttribute(9, "class", "variable-explorer-codes");
        builder.AddContent(10, KodeverkCodesBody(key, index));
        builder.CloseElement();
    };

    /// <summary>What the open code list shows: that it is loading, why it is empty, or the codes.</summary>
    /// <remarks>
    /// A failure is said here and nowhere else. One code list that could not be fetched leaves the
    /// rest of the panel, the variable above it and the rows behind it all describing exactly what
    /// they described before — the same reasoning that gives the kilde panel its own error field.
    /// </remarks>
    private RenderFragment KodeverkCodesBody(KodeverkKey key, int index) => builder =>
    {
        if (_codesLoading.Contains(key))
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "class", "caption");
            builder.AddContent(2, T.CodesLoading);
            builder.CloseElement();

            return;
        }

        if (_codesError.TryGetValue(key, out var error))
        {
            builder.OpenElement(3, "p");
            builder.AddAttribute(4, "class", "infobox infobox--bg-yellow");
            builder.AddContent(5, error);
            builder.CloseElement();

            return;
        }

        var codes = _codes.GetValueOrDefault(key, []);

        if (codes.Count == 0)
        {
            builder.OpenElement(6, "p");
            builder.AddAttribute(7, "class", "caption");
            builder.AddContent(8, T.NoCodes);
            builder.CloseElement();

            return;
        }

        builder.AddContent(9, CodesTable(index, codes));
    };

    /// <summary>
    /// The codes as a table of Verdi, Navn, Gyldig fra and Gyldig til.
    /// </summary>
    /// <remarks>
    /// A real <c>&lt;table&gt;</c>, one of the two this package emits — the other is the
    /// datasamlinger list in <c>KildeView</c>. The results list is not one of them: it is
    /// helsedata's own <c>variable-data-list</c>, a <c>&lt;ul&gt;</c> with a header row of
    /// <c>&lt;div&gt;</c>s, because that is the shape their stylesheet dresses. Four columns of
    /// code values have no such alternative shape —
    /// a definition list per code would lose the alignment that makes a code list readable at all.
    /// What makes it safe is that the fallback here is an element's own browser default rather than
    /// an invented class name: an unstyled table still aligns its columns, where an unstyled
    /// <c>kodeverk-table</c> would render as nothing.
    /// <para>
    /// <c>variable-explorer-codes</c> is a handle, the same kind of name as the others in that
    /// prefix, and carries no rule in either helsedata stylesheet. Named from the link's own line
    /// above it rather than given a <c>&lt;caption&gt;</c>, which is what the results list does and
    /// for the same reason — the name is already on screen, one line up.
    /// </para>
    /// </remarks>
    private RenderFragment CodesTable(int index, IReadOnlyList<KodeverkCode> codes) => builder =>
    {
        builder.OpenElement(0, "table");
        builder.AddAttribute(1, "class", "variable-explorer-codes__table");
        builder.AddAttribute(2, "aria-labelledby", KodeverkNameId(index));

        builder.OpenElement(3, "thead");
        builder.OpenElement(4, "tr");

        var head = 5;

        foreach (var heading in new[] { T.ColumnCodeValue, T.ColumnCodeName, T.ColumnValidFrom, T.ColumnValidTo })
        {
            builder.OpenElement(head, "th");
            builder.AddAttribute(head + 1, "scope", "col");
            builder.AddContent(head + 2, heading);
            builder.CloseElement();

            head += 3;
        }

        builder.CloseElement();
        builder.CloseElement();

        builder.OpenElement(20, "tbody");

        var seq = 30;

        foreach (var code in codes)
        {
            builder.OpenElement(seq, "tr");

            builder.OpenElement(seq + 1, "td");
            builder.AddContent(seq + 2, code.Value);
            builder.CloseElement();

            builder.OpenElement(seq + 3, "td");
            // The catalogue's own wording, Norwegian whatever the page is — the rule every other
            // value out of the catalogue follows here.
            builder.AddAttribute(seq + 4, "lang", "no");
            builder.AddContent(seq + 5, Trimmed(code.Name) ?? T.NotSpecified);
            builder.CloseElement();

            builder.OpenElement(seq + 6, "td");
            builder.AddContent(seq + 7, ValidityDate(code.ValidFrom));
            builder.CloseElement();

            builder.OpenElement(seq + 8, "td");
            builder.AddContent(seq + 9, ValidityDate(code.ValidTo));
            builder.CloseElement();

            builder.CloseElement();

            seq += 20;
        }

        builder.CloseElement();
        builder.CloseElement();
    };

    /// <summary>
    /// A validity date as a day, or "Ikke oppgitt" when the kodeverk records none.
    /// </summary>
    /// <remarks>
    /// The day and not the time. Every one of these arrives as midnight UTC or as the instant a
    /// bulk import ran, neither of which is a fact about when a code applied — showing it would
    /// dress an import timestamp up as precision the data does not have.
    /// <para>
    /// Written out rather than shown as an em dash, which is the rule the whole panel follows:
    /// there is no visually-hidden helper to whisper the meaning of a dash into, so a missing value
    /// says so in words for everyone.
    /// </para>
    /// </remarks>
    private string ValidityDate(DateTimeOffset? date) =>
        date is { } value
            ? value.ToString("d", CatalogueProperties.Culture(Language))
            : T.NotSpecified;
}
