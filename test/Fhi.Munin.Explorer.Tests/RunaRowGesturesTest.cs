using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Runa's row follows helsedata's own list: one Tab stop per collapsed row, the name button, which
/// is the disclosure and carries the chevron (Fhi.Metadata-35w0p.78, reversing Fhi.Metadata-35w0p.34).
/// </summary>
/// <remarks>
/// Stiler draws the row ring and the chevron through this exact shape (Fhi.Metadata-35w0p.80), so
/// the shape is asserted here: a wrong one renders as nothing and nothing else here would notice.
/// </remarks>
public class RunaRowGesturesTest : ExplorerTestContext
{
    private const string Name = "button.munin-explorer-dataitem-main__name";

    private static readonly Guid TaleId = Guid.NewGuid();

    private static readonly Guid SpyttId = Guid.NewGuid();

    private static readonly Guid BlankId = Guid.NewGuid();

    private sealed class RowsClient : EmptyMuninExplorerClient
    {
        private static readonly VariableSummary[] Rows =
        [
            new() { Id = TaleId, Code = "V_ALS.TALE", PreferredTerm = "1. Tale" },
            new() { Id = SpyttId, Code = "V_ALS.SPYTT", PreferredTerm = "2. Spyttsekresjon" },
            new() { Id = BlankId, Code = "V_ALS.BLANK", PreferredTerm = "" },
        ];

        public List<Guid> DetailCalls { get; } = [];

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Page<VariableSummary>
            {
                Items = Rows,
                TotalCount = Rows.Length,
                PageNumber = 1,
                Size = 25,
                TotalPages = 1,
            });

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default)
        {
            DetailCalls.Add(id);
            var row = Rows.Single(r => r.Id == id);

            return Task.FromResult<VariableDetail?>(
                new VariableDetail { Id = id, Code = row.Code, PreferredTerm = row.PreferredTerm });
        }
    }

    private IRenderedComponent<VariableSearch> Render(RowsClient? client = null, bool signedIn = false)
    {
        Services.AddSingleton<IMuninExplorerClient>(client ?? new RowsClient());
        Services.AddScoped<VariableListState>();

        return Render<VariableSearch>(p => p.Add(c => c.IsAuthenticated, signedIn));
    }

    private static IReadOnlyList<IElement> Rows(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll("ul.munin-explorer-data-list > li");

    private static IReadOnlyList<IElement> Names(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll($"ul.munin-explorer-data-list {Name}");

    private static IElement Strip(IRenderedComponent<VariableSearch> cut, int row) =>
        cut.FindAll("ul.munin-explorer-data-list .munin-explorer-dataitem-main")[row];

    private static string? Expanded(IRenderedComponent<VariableSearch> cut, int row) =>
        Names(cut)[row].GetAttribute("aria-expanded");

    private static void Press(IElement control) => control.Click(new MouseEventArgs { Detail = 1 });

    private static IElement? WholeVariable(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll(".munin-explorer-drilldown[role=region]").SingleOrDefault();

    private static IElement ShowWholeVariable(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll(".munin-explorer-detail button").Single(b => b.TextContent.Trim() == "Vis hele variabelen");

    private static void Back(IRenderedComponent<VariableSearch> cut) =>
        Press(WholeVariable(cut)!.QuerySelector("button")!);

    // What the browser puts in the Tab order, by AC1's definition.
    private static IReadOnlyList<IElement> TabStops(IElement scope) =>
        [.. scope.QuerySelectorAll("button, a[href], [tabindex]")
            .Where(e => e.GetAttribute("tabindex") is not { } t || (int.TryParse(t, out var n) && n >= 0))];

    /// <summary>
    /// The browser's own activation of a <c>&lt;button type="button"&gt;</c> from Enter or Space: a
    /// click counting nought. A key handler of its own would fire the press twice there, so its
    /// absence is asserted before the click is sent.
    /// </summary>
    private static void KeyboardActivate(IElement control, string key)
    {
        Assert.Equal("BUTTON", control.TagName);
        Assert.Equal("button", control.GetAttribute("type"));
        Assert.Throws<MissingEventHandlerException>(() => control.KeyDown(key));

        control.Click(new MouseEventArgs { Detail = 0 });
    }

    // -----------------------------------------------------------------------
    // Criterion 1: one Tab stop per collapsed row, signed in or out.

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CollapsedRow_WhateverTheSignIn_ThenItsOnlyTabStopIsTheNameButton(bool signedIn)
    {
        var cut = Render(signedIn: signedIn);

        Assert.Equal(3, Rows(cut).Count);

        foreach (var row in Rows(cut))
        {
            var stop = Assert.Single(TabStops(row));
            Assert.Equal("munin-explorer-dataitem-main__name", stop.ClassName);
            Assert.Equal("BUTTON", stop.TagName);
        }

        // An open row adds only its panel's stops; the rows around it stay at one.
        Press(Names(cut)[0]);

        Assert.All(Rows(cut).Skip(1), row => Assert.Single(TabStops(row)));
        Assert.Equal(signedIn, cut.FindAll(".munin-explorer-detail button[aria-pressed]").Count == 1);
    }

    // -----------------------------------------------------------------------
    // Criterion 2: the name button is the disclosure.

    [Fact]
    public void Name_Always_ThenItCarriesTheDisclosureAttributesAndAControlsOnlyWhileThePanelExists()
    {
        var cut = Render();

        foreach (var name in Names(cut))
        {
            Assert.False(name.HasAttribute("disabled"));
            Assert.False(name.HasAttribute("role"));
            Assert.Equal("false", name.GetAttribute("aria-expanded"));
            Assert.False(name.HasAttribute("aria-controls"));
        }

        Press(Names(cut)[1]);

        Assert.Equal("true", Expanded(cut, 1));
        Assert.Equal(cut.Find(".munin-explorer-detail").Id, Names(cut)[1].GetAttribute("aria-controls"));
        Assert.Equal("false", Expanded(cut, 0));
        Assert.False(Names(cut)[0].HasAttribute("aria-controls"));

        Press(Names(cut)[1]);

        Assert.Equal("false", Expanded(cut, 1));
        Assert.False(Names(cut)[1].HasAttribute("aria-controls"));
        Assert.Empty(cut.FindAll(".munin-explorer-detail"));
    }

    [Theory]
    [InlineData(0, "Vis detaljer for 1. Tale", "Skjul detaljer for 1. Tale")]
    [InlineData(2, "Vis detaljer for V_ALS.BLANK", "Skjul detaljer for V_ALS.BLANK")]
    public void Name_WhenToggled_ThenItsLabelSaysWhatPressingItDoesForThatVariable(
        int row, string collapsed, string expanded)
    {
        // A blank PreferredTerm is named by its code, not by a sentence every such row shares
        // (Fhi.Metadata-w13lk).
        var cut = Render();

        Assert.Equal(collapsed, Names(cut)[row].GetAttribute("aria-label"));
        Assert.Equal(collapsed, AccessibleName.Of(Names(cut)[row]));

        Press(Names(cut)[row]);

        Assert.Equal(expanded, AccessibleName.Of(Names(cut)[row]));
    }

    // -----------------------------------------------------------------------
    // Criterion 3: Enter, Space and click expand in place; the whole variable is the panel's.

    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public void Name_WhenActivatedFromTheKeyboard_ThenEachActivationTogglesInPlace(string key)
    {
        var client = new RowsClient();
        var cut = Render(client);
        var navigation = Services.GetRequiredService<NavigationManager>();
        var uri = navigation.Uri;
        var id = Names(cut)[0].Id;

        KeyboardActivate(Names(cut)[0], key);

        Assert.Equal("true", Expanded(cut, 0));
        Assert.Null(WholeVariable(cut));
        Assert.Equal(id, Names(cut)[0].Id);
        Assert.Equal(uri, navigation.Uri);
        Assert.Equal([TaleId], client.DetailCalls);

        KeyboardActivate(Names(cut)[0], key);

        Assert.Equal("false", Expanded(cut, 0));
        Assert.Equal(uri, navigation.Uri);
    }

    [Fact]
    public void Name_WhenClicked_ThenThePanelOpensInPlaceAndNotTheWholeVariable()
    {
        var cut = Render();
        var navigation = Services.GetRequiredService<NavigationManager>();
        var uri = navigation.Uri;

        Press(Names(cut)[1]);

        Assert.Null(WholeVariable(cut));
        Assert.NotEmpty(cut.FindAll("ul.munin-explorer-data-list"));
        Assert.Single(cut.FindAll(".munin-explorer-detail"));
        Assert.Equal("true", Expanded(cut, 1));
        Assert.Equal(uri, navigation.Uri);
    }

    [Fact]
    public void Panel_WhenShowWholeVariableIsPressed_ThenTheWholeVariableOpensAndBackRestoresTheRow()
    {
        var client = new RowsClient();
        var cut = Render(client);

        Press(Names(cut)[1]);
        Press(ShowWholeVariable(cut));

        var view = WholeVariable(cut);
        Assert.NotNull(view);
        Assert.Equal(
            "2. Spyttsekresjon",
            cut.Find($"#{view!.GetAttribute("aria-labelledby")}").TextContent.Trim());
        Assert.Contains("V_ALS.SPYTT", view.TextContent);
        Assert.Equal([SpyttId], client.DetailCalls);

        Back(cut);

        Assert.Null(WholeVariable(cut));
        Assert.Equal("false,true,false", string.Join(",", Names(cut).Select(n => n.GetAttribute("aria-expanded"))));
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(1, true)]
    public void Name_WhenAClickIsPartOfASelectionGesture_ThenItDoesNotToggle(long clicks, bool shift)
    {
        // The second click of a double-click, or a shift-click, is a reader taking the term. A real
        // double-click's first click still opens the row; the second must not shut it again, which
        // VariableSearchTest.RowHeading_WhenItIsDoubleClicked_ThenThePanelIsLeftOpen drives in order.
        var cut = Render();

        Names(cut)[0].Click(new MouseEventArgs { Detail = clicks, ShiftKey = shift });

        Assert.Equal("false", Expanded(cut, 0));
        Assert.Empty(cut.FindAll(".munin-explorer-detail"));
    }

    // -----------------------------------------------------------------------
    // Criterion 4: the chevron is inside the name button; the strip still toggles.

    [Fact]
    public void Row_Always_ThenTheChevronIsTheNameButtonsFirstChildAndNothingElseDrawsOne()
    {
        var cut = Render();

        for (var i = 0; i < 3; i++)
        {
            var name = Names(cut)[i];
            var glyph = name.FirstElementChild!;

            Assert.Equal("SPAN", glyph.TagName);
            Assert.Equal("icon icon-keyboard-arrow-down munin-explorer-dataitem-main__expand-icon", glyph.ClassName);
            Assert.Equal("true", glyph.GetAttribute("aria-hidden"));
            Assert.Equal("", glyph.TextContent);
            Assert.Single(Rows(cut)[i].QuerySelectorAll(".munin-explorer-dataitem-main__expand-icon"));

            // The name's wrapper is the strip's first cell now that no chevron cell precedes it.
            Assert.Equal("rowheader", Strip(cut, i).FirstElementChild!.GetAttribute("role"));
        }

        Assert.Empty(cut.FindAll(".munin-explorer-dataitem__expand-cell"));
        Assert.Empty(cut.FindAll(".munin-explorer-dataitem__expand-toggle"));
        Assert.DoesNotContain(
            cut.FindAll(".munin-explorer-dataitem-header [role=columnheader]"),
            h => h.ClassList.Contains("screenreader-only"));

        Press(Names(cut)[0]);

        Assert.Equal(
            "icon icon-keyboard-arrow-up munin-explorer-dataitem-main__expand-icon",
            Names(cut)[0].FirstElementChild!.ClassName);
    }

    [Fact]
    public void RowStrip_WhenPressed_ThenItFlipsThatRowsDisclosure()
    {
        var cut = Render();

        Press(Strip(cut, 1));
        Assert.Equal("true", Expanded(cut, 1));

        Press(Strip(cut, 1));
        Assert.Equal("false", Expanded(cut, 1));
    }

    [Fact]
    public void RowStrip_WhenThePressWasADrag_ThenTheDisclosureStaysAsItWas()
    {
        var cut = Render();
        Strip(cut, 0).MouseDown(new MouseEventArgs { ClientX = 10, ClientY = 10 });
        Strip(cut, 0).MouseUp(new MouseEventArgs { ClientX = 60, ClientY = 10 });
        Press(Strip(cut, 0));

        Assert.Equal("false", Expanded(cut, 0));
    }

    [Fact]
    public void Name_WhenADragBeginsAndEndsInsideIt_ThenThePanelStaysShut()
    {
        // The name's mousedown and mouseup are the strip's to measure, so they must not be stopped;
        // the drag guard is what leaves a copied term's row shut. (Fhi.Metadata-l9l2n.81)
        var cut = Render();

        Assert.False(Names(cut)[0].HasAttribute("blazor:onmousedown:stoppropagation"));
        Assert.False(Names(cut)[0].HasAttribute("blazor:onmouseup:stoppropagation"));

        Strip(cut, 0).MouseDown(new MouseEventArgs { ClientX = 20, ClientY = 10 });
        Strip(cut, 0).MouseUp(new MouseEventArgs { ClientX = 90, ClientY = 10 });
        Press(Names(cut)[0]);

        Assert.Equal("false", Expanded(cut, 0));
    }

    [Fact]
    public void Name_WhenPressed_ThenItTogglesOnceAndTheStripDoesNotToggleItBack()
    {
        var cut = Render();

        Press(Names(cut)[2]);

        Assert.Equal("true", Expanded(cut, 2));
        Assert.True(Names(cut)[2].HasAttribute("blazor:onclick:stoppropagation"));
    }

    // -----------------------------------------------------------------------
    // The panel is named by its own heading (Fhi.Metadata-yaco2), not by the name button.

    [Theory]
    [InlineData(0, "1. Tale")]
    [InlineData(2, "V_ALS.BLANK")]
    public void Panel_WhenOpen_ThenItIsNamedByItsHeadingRatherThanTheNameButton(int row, string name)
    {
        var cut = Render();

        Press(Names(cut)[row]);

        var panel = cut.Find(".munin-explorer-detail");
        var label = cut.Find($"#{panel.GetAttribute("aria-labelledby")}");

        Assert.Equal("H3", label.TagName);
        Assert.Equal(panel.Id, label.ParentElement!.Id);
        Assert.Equal(name, AccessibleName.Of(panel));
        Assert.NotEqual(AccessibleName.Of(Names(cut)[row]), AccessibleName.Of(panel));
    }

    // -----------------------------------------------------------------------
    // The sample stand-in copies Stiler's rules for the name button and adds nothing of its own.

    public static TheoryData<string> SampleStylesheets() =>
    [
        Repo.In("samples", "ModernHost", "wwwroot", "host.css"),
        Repo.In("samples", "LegacyHost", "wwwroot", "css", "host.css"),
    ];

    [Theory]
    [MemberData(nameof(SampleStylesheets))]
    public void SampleStylesheet_Always_ThenTheNameButtonHasStilersRingAndChevronRules(string path)
    {
        // Exactly Stiler 0.1.119's rules. An outline would draw nothing on helsedata, where
        // body:not(.is-tabbing) button:focus removes it; the ring is the ::after's box-shadow.
        var css = Regex.Replace(File.ReadAllText(path), @"/\*.*?\*/", " ", RegexOptions.Singleline);
        var rules = Regex.Matches(css, @"([^{}]+)\{([^{}]*)\}")
            .Where(m => m.Groups[1].Value.Contains("button.munin-explorer-dataitem-main__name", StringComparison.Ordinal)
                && !m.Groups[1].Value.Contains("> button", StringComparison.Ordinal))
            .Select(m => Regex.Replace(m.Groups[1].Value.Trim(), @"\s+", " ") + " { "
                + string.Join("; ", m.Groups[2].Value
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Order(StringComparer.Ordinal)) + " }")
            .Order(StringComparer.Ordinal);

        Assert.Equal(
        [
            ".munin-explorer-data-list__item__row:hover button.munin-explorer-dataitem-main__name[aria-expanded=\"false\"] .munin-explorer-dataitem-main__expand-icon { background-image: url(../img/icons/keyboard-arrow/icon_down--blue.svg) }",
            ".munin-explorer-data-list__item__row:hover button.munin-explorer-dataitem-main__name[aria-expanded=\"true\"] .munin-explorer-dataitem-main__expand-icon { background-image: url(../img/icons/keyboard-arrow/icon_up--blue.svg) }",
            ".munin-explorer-dataitem-main button.munin-explorer-dataitem-main__name .munin-explorer-dataitem-main__expand-icon { align-self: center }",
            ".munin-explorer-dataitem-main button.munin-explorer-dataitem-main__name .munin-explorer-dataitem-main__expand-icon { display: inline-block; margin: 0 12px 0 0 }",
            "button.munin-explorer-dataitem-main__name:focus-visible::after { box-shadow: inset 0 0 0 3px rgb(66, 139, 255) }",
            "button.munin-explorer-dataitem-main__name[aria-expanded=\"false\"] .munin-explorer-dataitem-main__expand-icon { background-image: url(../img/icons/keyboard-arrow/icon_down.svg) }",
            "button.munin-explorer-dataitem-main__name[aria-expanded=\"true\"] .munin-explorer-dataitem-main__expand-icon { background-image: url(../img/icons/keyboard-arrow/icon_up.svg) }",
        ],
            rules);
        Assert.DoesNotContain(rules, rule => rule.Contains("outline", StringComparison.Ordinal));
    }
}
