using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>An opened variable's tabs: the data behind it, what it is, and where a parent gives them, the reader's notes.</summary>
/// <remarks>
/// The tab is the parent's, so the parent decides when to go back to Data: on opening another
/// variable, and not on coming back from the whole-variable view.
/// </remarks>
internal sealed class VariablePanelTabs : ComponentBase
{

    private readonly string _instance = Guid.NewGuid().ToString("N")[..8];

    [Parameter, EditorRequired] public VariableDetail Detail { get; set; } = null!;

    [Parameter, EditorRequired] public KodeverkCodeLists Lists { get; set; } = null!;

    /// <summary>The id of the heading that names the panel, which names the tablist too.</summary>
    [Parameter, EditorRequired] public string LabelledBy { get; set; } = "";

    [Parameter] public PanelTab Tab { get; set; } = PanelTab.Data;

    [Parameter] public EventCallback<PanelTab> TabChanged { get; set; }

    /// <summary>The level of the headings inside the Data tab: one below the panel's own.</summary>
    [Parameter] public int SectionLevel { get; set; } = 3;

    [Parameter] public string Language { get; set; } = "no";

    /// <summary>Whether the second tab opens with the description, which a parent may already show above.</summary>
    [Parameter] public bool ShowDescription { get; set; } = true;

    /// <summary>Drawn at the top of the Data tab, where a saved list puts the row's "Ønskede data".</summary>
    [Parameter] public RenderFragment? DataTop { get; set; }

    /// <summary>The third tab's content; without it there is no third tab.</summary>
    [Parameter] public RenderFragment? Notes { get; set; }

    /// <summary>A datatype code's name where the detail's own vocabulary has none.</summary>
    [Parameter] public Func<string, string?>? DataTypeName { get; set; }

    private Texts T => Texts.For(Language);

    private PanelTab[] Tabs => Notes is null ? [PanelTab.Data, PanelTab.About] : [PanelTab.Data, PanelTab.About, PanelTab.Notes];

    private string Reader => ReaderLanguage.Of(Language);

    private string TabId(PanelTab tab) => $"munin-explorer-tab-{_instance}-{tab}";

    // One id for every tab: a single panel whose contents change, so no tab names an absent element.
    private string TabPanelId => $"munin-explorer-tabpanel-{_instance}";

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        // APG tabs: only the selected tab is in the tab order and arrow keys move between them.
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "munin-explorer-meta__tabs");
        builder.AddAttribute(2, "role", "tablist");
        builder.AddAttribute(3, "aria-labelledby", LabelledBy);
        builder.AddAttribute(4, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, TabKeyAsync));

        foreach (var tab in Tabs)
        {
            var selected = tab == Tab;

            builder.OpenElement(5, "div");
            builder.SetKey(tab);
            builder.AddAttribute(6, "class", selected
                ? "munin-explorer-meta__tab munin-explorer-meta__tab--active"
                : "munin-explorer-meta__tab");

            builder.OpenElement(7, "button");
            builder.AddAttribute(8, "class", "hd-button-reset");
            builder.AddAttribute(9, "type", "button");
            builder.AddAttribute(10, "role", "tab");
            builder.AddAttribute(11, "id", TabId(tab));
            builder.AddAttribute(12, "aria-selected", selected ? "true" : "false");
            builder.AddAttribute(13, "aria-controls", TabPanelId);
            builder.AddAttribute(14, "tabindex", selected ? 0 : -1);
            builder.AddAttribute(15, "onclick", EventCallback.Factory.Create(this, () => SelectAsync(tab)));
            builder.AddContent(16, TabLabel(tab));
            builder.CloseElement();

            builder.CloseElement();
        }

        builder.CloseElement();

        builder.OpenElement(20, "div");
        builder.OpenElement(21, "div");
        builder.AddAttribute(22, "class", "munin-explorer-meta__tab-content");
        builder.AddAttribute(23, "role", "tabpanel");
        builder.AddAttribute(24, "id", TabPanelId);
        builder.AddAttribute(25, "aria-labelledby", TabId(Tab));
        builder.AddAttribute(26, "tabindex", 0);
        builder.AddContent(27, Tab switch
        {
            PanelTab.Data => DataTab,
            PanelTab.Notes when Notes is { } notes => notes,
            _ => AboutTab,
        });
        builder.CloseElement();
        builder.CloseElement();
    }

    private RenderFragment DataTab => builder =>
    {
        if (DataTop is { } top)
        {
            builder.OpenRegion(30);
            builder.AddContent(0, top);
            builder.CloseRegion();
        }

        if (Detail.KodeverkLinks.Count == 0 && !StatisticsBlock.AnyStatistics(Detail, StatisticsLayout.Drawer))
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "class", $"caption {DetailBlocks.Absent}");
            builder.AddContent(2, T.NoKodeverkOrStatistics);
            builder.CloseElement();
        }

        builder.OpenComponent<KodeverkGroups>(3);
        builder.AddComponentParameter(4, nameof(KodeverkGroups.Detail), Detail);
        builder.AddComponentParameter(5, nameof(KodeverkGroups.Lists), Lists);
        builder.AddComponentParameter(6, nameof(KodeverkGroups.Level), SectionLevel);
        builder.AddComponentParameter(7, nameof(KodeverkGroups.Language), Language);
        builder.CloseComponent();

        builder.AddContent(8, StatisticsBlock.For(
            Detail, SectionLevel, "headline headline-xxs margin--none munin-explorer-group", T, StatisticsLayout.Drawer));
    };

    private RenderFragment AboutTab => builder =>
    {
        // A <div>, not a <p>: authored text can hold a list.
        if (ShowDescription)
        {
            builder.OpenElement(0, "div");
            builder.AddContent(1, DetailValue(Detail.Description, authored: true));
            builder.CloseElement();
        }

        builder.OpenElement(2, "dl");
        builder.AddAttribute(3, "class", "munin-explorer-meta__grid munin-explorer-meta__grid-1");

        builder.AddContent(4, Fact(T.FieldCode, DetailValue(Detail.Code)));

        var dataType = DataType;
        builder.AddContent(5, Fact(T.FieldDataType, b => b.AddContent(0, dataType?.Text ?? T.NotSpecified), dataType?.Lang));

        // Absent rather than "Ikke oppgitt": most variables carry no statistics at all.
        if (StatisticsBlock.LatestYearSet(Detail) is { } latestYearSet)
        {
            builder.AddContent(6, Fact(T.FieldLatestYearSet, b => b.AddContent(0, latestYearSet)));
        }

        if (KildeTrailBlock.NamedDatasamlinger(Detail) is { Count: > 0 } datasamlinger)
        {
            builder.AddContent(7, Fact(T.HeadingDataCollections, b =>
            {
                b.OpenElement(0, "ul");

                foreach (var datasamling in datasamlinger)
                {
                    b.OpenElement(1, "li");
                    b.AddAttribute(2, "lang", CatalogueProperties.Foreign("no", Reader));
                    b.AddContent(3, datasamling.Name);
                    b.CloseElement();
                }

                b.CloseElement();
            }));
        }

        // Munin's other fields, where they have a value, as the whole-variable view draws them (ADO 121586).
        builder.OpenRegion(8);
        DetailBlocks.Rows(builder, 0, CatalogueProperties.Rows(Detail.PropertyMetadata, Detail.AdditionalProperties, Reader, VariableView.DrawnElsewhere), Reader, T);
        builder.CloseRegion();

        builder.CloseElement();
    };


    private static RenderFragment Fact(string label, RenderFragment value, string? valueLang = null) => builder =>
    {
        builder.OpenElement(0, "div");

        builder.OpenElement(1, "dt");
        builder.AddAttribute(2, "class", "headline headline-xxs margin--none");
        builder.AddContent(3, label);
        builder.CloseElement();

        builder.OpenElement(4, "dd");
        builder.AddAttribute(5, "lang", valueLang);
        builder.AddContent(6, value);
        builder.CloseElement();

        builder.CloseElement();
    };

    private string TabLabel(PanelTab tab) => tab switch
    {
        PanelTab.Data => T.TabData,
        PanelTab.About => T.TabDetails,
        PanelTab.Notes => T.TabNotes,
        _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, "No label for this tab."),
    };

    private async Task SelectAsync(PanelTab tab)
    {
        Tab = tab;
        await TabChanged.InvokeAsync(tab);
    }

    // The unselected tabs are out of the tab order, so without arrow keys they cannot be reached at all.
    private Task TabKeyAsync(KeyboardEventArgs e)
    {
        var i = Array.IndexOf(Tabs, Tab);

        var next = e.Key switch
        {
            "ArrowRight" or "ArrowDown" => (i + 1) % Tabs.Length,
            "ArrowLeft" or "ArrowUp" => (i - 1 + Tabs.Length) % Tabs.Length,
            "Home" => 0,
            "End" => Tabs.Length - 1,
            _ => i,
        };

        return next == i ? Task.CompletedTask : SelectAsync(Tabs[next]);
    }

    // The detail carries the datatype vocabulary in both languages, so the facets are only a fallback.
    private (string Text, string? Lang)? DataType
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Detail.DataType))
            {
                return null;
            }

            var code = T.CanonicalDataTypeCode(Detail.DataType);

            return CatalogueProperties.DataTypeWord(Detail.PropertyMetadata, code, Reader)
                ?? (DataTypeName?.Invoke(code) ?? code, null);
        }
    }

    /// <summary>One value: the catalogue's own words, marked Norwegian, or "Ikke oppgitt".</summary>
    private RenderFragment DetailValue(string? value, bool authored = false) => builder =>
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            builder.AddContent(0, T.NotSpecified);

            return;
        }

        builder.OpenElement(1, "span");
        builder.AddAttribute(2, "lang", "no");
        builder.AddContent(3, authored ? CatalogueMarkdown.Render(value) : (RenderFragment)(b => b.AddContent(0, value)));
        builder.CloseElement();
    };
}
