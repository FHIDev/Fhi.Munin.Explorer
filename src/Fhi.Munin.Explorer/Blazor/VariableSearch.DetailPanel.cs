using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.Display;
using Microsoft.AspNetCore.Components;
namespace Fhi.Munin.Explorer.Blazor;

/// <summary>The panel that opens under a selected row: what the variable is, and what its data holds.</summary>
public partial class VariableSearch
{

    /// <summary>Whether this row is the one whose detail panel is open.</summary>
    private bool IsSelected(VariableSummary v) => _selected == VariableDatasamlingKey.Of(v);

    /// <summary>The open panel's description, trimmed, or null while there is none to show.</summary>
    private string? DetailDescription => DisplayText.Trimmed(_detail?.Description);

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

    // One below the drawer's own heading, so the Data tab's group headings nest under it.
    private int DrawerSectionLevel => Math.Clamp(RowLevel + 1, 1, 6);

    // The whole variable titles itself at RowLevel and Kodeverk one below, so its groups go two
    // below, the level VariableView gives its own metadata groups. (Fhi.Metadata-35w0p.81)
    private int WholeGroupLevel => Math.Clamp(RowLevel + 2, 1, 6);

    private string DrawerHeadingId(VariableSummary v) => $"munin-explorer-meta-heading-{_instance}-{RowSuffix(v)}";

    // Drawn from the row rather than the detail payload, so the region has its name while the fetch
    // is still in flight. Stiler scopes the rule under .munin-explorer-meta, so it must stay inside
    // the panel. (Fhi.Metadata-yaco2)
    private RenderFragment DrawerHeading(VariableSummary v) => builder =>
    {
        var named = T.Named(v.PreferredTerm, v.Code);

        builder.OpenElement(0, $"h{RowLevel}");
        builder.AddAttribute(1, "class", "munin-explorer-meta__heading");
        builder.AddAttribute(2, "id", DrawerHeadingId(v));
        builder.AddAttribute(3, "lang", named.Norwegian ? Foreign("no") : null);
        builder.AddContent(4, named.Text);
        builder.CloseElement();
    };

    // No aria-labelledby on the name button: it is named by ExpandLabel, one aria-label in the
    // reader's language around Munin's name, as Kelda's chevron is. The two-language
    // aria-labelledby rule lives on PanelSaveButton, the one control here that still needs it.

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

    private RenderFragment PanelTabs(VariableSummary v, VariableDetail detail) => builder =>
    {
        builder.OpenComponent<VariablePanelTabs>(0);
        builder.AddComponentParameter(1, nameof(VariablePanelTabs.Detail), detail);
        builder.AddComponentParameter(2, nameof(VariablePanelTabs.Lists), _codeLists);
        builder.AddComponentParameter(3, nameof(VariablePanelTabs.LabelledBy), DrawerHeadingId(v));
        builder.AddComponentParameter(4, nameof(VariablePanelTabs.Tab), _tab);
        builder.AddComponentParameter(5, nameof(VariablePanelTabs.TabChanged),
            EventCallback.Factory.Create<PanelTab>(this, tab => _tab = tab));
        builder.AddComponentParameter(6, nameof(VariablePanelTabs.SectionLevel), DrawerSectionLevel);
        builder.AddComponentParameter(7, nameof(VariablePanelTabs.Language), Language);
        builder.AddComponentParameter(8, nameof(VariablePanelTabs.DataTypeName), (Func<string, string?>)DataTypeName);
        builder.CloseComponent();
    };

    // Shared with the drawer through _codeLists, so the whole variable does not fetch a list again;
    // no section at all without kodeverk, so the nav offers none.
    private IReadOnlyList<DetailNamedSection> KodeverkSections(VariableDetail detail) =>
        detail.KodeverkLinks.Count == 0 || _codeLists is not { } lists
            ? []
            : [new DetailNamedSection(DetailSectionIds.CodeLists, T.HeadingKodeverk, builder =>
            {
                builder.OpenComponent<KodeverkGroups>(0);
                builder.AddComponentParameter(1, nameof(KodeverkGroups.Detail), detail);
                builder.AddComponentParameter(2, nameof(KodeverkGroups.Lists), lists);
                builder.AddComponentParameter(3, nameof(KodeverkGroups.Level), WholeGroupLevel);
                builder.AddComponentParameter(4, nameof(KodeverkGroups.Language), Language);
                builder.CloseComponent();
            })];
}
