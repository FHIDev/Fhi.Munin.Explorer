using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Runa's row has two gestures on two controls: the name opens the whole variable in place, and a
/// separate chevron is the row's disclosure (Fhi.Metadata-35w0p.34, reversing Fhi.Metadata-zqe14).
/// </summary>
/// <remarks>
/// Stiler's rules for the chevron are <c>:has()</c>-gated on its exact shape
/// (Fhi.Metadata-35w0p.65, .67), so the shape is asserted here: a wrong one renders as nothing and
/// nothing else in this repository would notice.
/// </remarks>
public class RunaRowGesturesTest : ExplorerTestContext
{
    private const string Chevron = "button.munin-explorer-dataitem__expand-toggle";

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

    private IRenderedComponent<VariableSearch> Render(RowsClient? client = null)
    {
        Services.AddSingleton<IMuninExplorerClient>(client ?? new RowsClient());

        return Render<VariableSearch>();
    }

    private static IReadOnlyList<IElement> Chevrons(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll($"ul.munin-explorer-data-list {Chevron}");

    private static IReadOnlyList<IElement> Names(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll($"ul.munin-explorer-data-list {Name}");

    private static IElement Strip(IRenderedComponent<VariableSearch> cut, int row) =>
        cut.FindAll("ul.munin-explorer-data-list .munin-explorer-dataitem-main")[row];

    private static string? Expanded(IRenderedComponent<VariableSearch> cut, int row) =>
        Chevrons(cut)[row].GetAttribute("aria-expanded");

    private static void Press(IElement control) => control.Click(new MouseEventArgs { Detail = 1 });

    private static IElement? WholeVariable(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll(".munin-explorer-drilldown[role=region]").SingleOrDefault();

    private static void Back(IRenderedComponent<VariableSearch> cut) =>
        Press(WholeVariable(cut)!.QuerySelector("button")!);

    /// <summary>
    /// The browser's own activation of a <c>&lt;button type="button"&gt;</c> from Enter or Space: a
    /// click counting nought. The control must leave that to the browser, so a key handler of its
    /// own — which would fire the press twice there — is asserted absent before the click is sent.
    /// </summary>
    private static void KeyboardActivate(IElement control, string key)
    {
        Assert.Equal("BUTTON", control.TagName);
        Assert.Equal("button", control.GetAttribute("type"));
        Assert.Throws<MissingEventHandlerException>(() => control.KeyDown(key));

        control.Click(new MouseEventArgs { Detail = 0 });
    }

    // -----------------------------------------------------------------------
    // Criterion 1: the name opens the whole variable in place, and Back restores the row.

    [Fact]
    public void Name_WhenPressed_ThenTheWholeVariableIsShownForThatCodeAndNoRowPanelIs()
    {
        var client = new RowsClient();
        var cut = Render(client);

        Press(Names(cut)[1]);

        var view = WholeVariable(cut);
        Assert.NotNull(view);
        Assert.Equal(
            "2. Spyttsekresjon",
            cut.Find($"#{view!.GetAttribute("aria-labelledby")}").TextContent.Trim());
        Assert.Contains("V_ALS.SPYTT", view.TextContent);
        Assert.Equal([SpyttId], client.DetailCalls);

        Assert.Empty(cut.FindAll(".munin-explorer-detail"));
        Assert.Empty(cut.FindAll("ul.munin-explorer-data-list"));
    }

    [Theory]
    [InlineData(null, "false,false,false")]
    [InlineData(1, "false,true,false")]
    [InlineData(0, "true,false,false")]
    public void Name_WhenTheReaderGoesBack_ThenTheRowDisclosuresAreAsTheyWereBeforeThePress(
        int? openBefore, string expected)
    {
        var cut = Render();

        if (openBefore is { } row)
        {
            Press(Chevrons(cut)[row]);
        }

        Press(Names(cut)[1]);
        Assert.NotNull(WholeVariable(cut));

        Back(cut);

        Assert.Null(WholeVariable(cut));
        Assert.Equal(expected, string.Join(",", Chevrons(cut).Select(c => c.GetAttribute("aria-expanded"))));
        Assert.Equal(openBefore is null ? 0 : 1, cut.FindAll(".munin-explorer-detail").Count);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(1, true)]
    public void Name_WhenAGestureSelectsItsText_ThenTheListStays(long clicks, bool shift)
    {
        // The name left the disclosure sweep with its aria-expanded, so the gesture rule it still
        // owes — a double-click or a shift-click is a reader selecting the term — is pinned here.
        var cut = Render();

        Names(cut)[0].Click(new MouseEventArgs { Detail = clicks, ShiftKey = shift });

        Assert.Null(WholeVariable(cut));
        Assert.NotEmpty(Names(cut));
    }

    // -----------------------------------------------------------------------
    // Criterion 2: the disclosure attributes are the chevron's, one enabled chevron per row.

    [Fact]
    public void Chevron_Always_ThenEveryRowHasOneEnabledAndTheNameCarriesNeitherAttribute()
    {
        var cut = Render();

        Assert.Equal(3, cut.FindAll("ul.munin-explorer-data-list > li").Count);
        Assert.Equal(3, Chevrons(cut).Count);

        foreach (var (chevron, i) in Chevrons(cut).Select((c, i) => (c, i)))
        {
            Assert.Equal("hd-button-reset munin-explorer-dataitem__expand-toggle", chevron.ClassName);
            Assert.False(chevron.HasAttribute("disabled"));
            Assert.Equal("false", chevron.GetAttribute("aria-expanded"));
            Assert.False(chevron.HasAttribute("aria-controls"));
            Assert.Single(Strip(cut, i).QuerySelectorAll(Chevron));
            Assert.False(chevron.HasAttribute("role"));
        }

        Press(Chevrons(cut)[0]);

        Assert.Equal("true", Expanded(cut, 0));
        Assert.Equal(cut.Find(".munin-explorer-detail").Id, Chevrons(cut)[0].GetAttribute("aria-controls"));

        Assert.All(Names(cut), name =>
        {
            Assert.False(name.HasAttribute("aria-expanded"));
            Assert.False(name.HasAttribute("aria-controls"));
        });
    }

    // -----------------------------------------------------------------------
    // Criterion 3: the row strip still toggles; neither control's click reaches it.

    [Fact]
    public void RowStrip_WhenPressed_ThenItFlipsThatRowsChevron()
    {
        var cut = Render();

        Press(Strip(cut, 1));
        Assert.Equal("true", Expanded(cut, 1));

        Press(Strip(cut, 1));
        Assert.Equal("false", Expanded(cut, 1));
    }

    [Fact]
    public void RowStrip_WhenThePressWasADrag_ThenTheChevronStaysAsItWas()
    {
        var cut = Render();
        Strip(cut, 0).MouseDown(new MouseEventArgs { ClientX = 10, ClientY = 10 });
        Strip(cut, 0).MouseUp(new MouseEventArgs { ClientX = 60, ClientY = 10 });
        Press(Strip(cut, 0));

        Assert.Equal("false", Expanded(cut, 0));
    }

    [Fact]
    public void Name_WhenPressed_ThenTheRowBehindItIsNotToggledToo()
    {
        var cut = Render();

        Press(Names(cut)[0]);

        // Had the click reached the strip, it would have toggled the row the name had just
        // selected, closing the view it opened.
        Assert.NotNull(WholeVariable(cut));

        Back(cut);

        Assert.Equal("false", Expanded(cut, 0));
        Assert.True(Names(cut)[0].HasAttribute("blazor:onclick:stoppropagation"));
    }

    [Fact]
    public void Chevron_WhenPressed_ThenItTogglesOnceAndTheRowDoesNotToggleItBack()
    {
        var cut = Render();

        Press(Chevrons(cut)[2]);

        Assert.Equal("true", Expanded(cut, 2));
        Assert.True(Chevrons(cut)[2].HasAttribute("blazor:onclick:stoppropagation"));
    }

    // -----------------------------------------------------------------------
    // Criterion 4: the panel is labelled by its own heading since Fhi.Metadata-yaco2, and the
    // name it gets still matches the name button's.

    [Theory]
    [InlineData(0, "1. Tale")]
    [InlineData(2, "V_ALS.BLANK")]
    public void Panel_WhenOpen_ThenItsLabelIsItsHeadingAndItSaysWhatTheNameButtonSays(int row, string name)
    {
        var cut = Render();

        Press(Chevrons(cut)[row]);

        var panel = cut.Find(".munin-explorer-detail");
        var label = cut.Find($"#{panel.GetAttribute("aria-labelledby")}");

        Assert.Equal("H3", label.TagName);
        Assert.Equal(panel.Id, label.ParentElement!.Id);
        Assert.Equal(name, AccessibleName.Of(panel));
        Assert.Equal(AccessibleName.Of(Names(cut)[row]), AccessibleName.Of(panel));
    }

    // -----------------------------------------------------------------------
    // Criterion 5: keyboard.

    [Theory]
    [InlineData("Enter")]
    [InlineData(" ")]
    public void Chevron_WhenActivatedFromTheKeyboard_ThenEachActivationToggles(string key)
    {
        var cut = Render();

        KeyboardActivate(Chevrons(cut)[0], key);
        Assert.Equal("true", Expanded(cut, 0));

        KeyboardActivate(Chevrons(cut)[0], key);
        Assert.Equal("false", Expanded(cut, 0));
    }

    [Fact]
    public void Name_WhenActivatedWithEnter_ThenTheWholeVariableOpens()
    {
        var cut = Render();

        KeyboardActivate(Names(cut)[0], "Enter");

        Assert.NotNull(WholeVariable(cut));
    }

    // -----------------------------------------------------------------------
    // Criterion 9: the DOM contract Stiler's :has() rules key on.

    [Fact]
    public void Chevron_Always_ThenItIsTheShapeStilersRulesKeyOn()
    {
        var cut = Render();

        for (var i = 0; i < 3; i++)
        {
            var chevron = Chevrons(cut)[i];
            var strip = Strip(cut, i);
            var cell = strip.FirstElementChild!;

            // The cell is the direct first child, because a row owns only cells; the button is its
            // only child (Fhi.Metadata-35w0p.72 keys on both).
            Assert.Equal("DIV", cell.TagName);
            Assert.Equal("cell", cell.GetAttribute("role"));
            Assert.Equal("munin-explorer-dataitem__expand-cell", cell.ClassName);
            Assert.Equal(chevron.OuterHtml, Assert.Single(cell.Children).OuterHtml);
            Assert.Equal("", chevron.TextContent.Trim());

            // The glyph class names the picture Stiler draws — collapsed points down — and the
            // attribute beside it is how Stiler tells the two states apart while both the old and
            // the new name are in circulation. (Fhi.Metadata-l9l2n.84)
            Assert.Equal("false", chevron.GetAttribute("aria-expanded"));

            var glyph = Assert.Single(chevron.Children);
            Assert.Equal("SPAN", glyph.TagName);
            Assert.Equal("icon icon-keyboard-arrow-down munin-explorer-dataitem-main__expand-icon", glyph.ClassName);
            Assert.DoesNotContain("icon-keyboard-arrow-right", glyph.ClassName!);
            Assert.DoesNotContain("icon--nomargin", glyph.ClassName!);
            Assert.Equal("true", glyph.GetAttribute("aria-hidden"));
        }

        Assert.Equal("Vis detaljer for 1. Tale", AccessibleName.Of(Chevrons(cut)[0]));
        Assert.Equal("Vis detaljer for V_ALS.BLANK", AccessibleName.Of(Chevrons(cut)[2]));

        Press(Chevrons(cut)[0]);

        Assert.Equal(
            "icon icon-keyboard-arrow-up munin-explorer-dataitem-main__expand-icon",
            Chevrons(cut)[0].FirstElementChild!.ClassName);
        Assert.Equal("true", Chevrons(cut)[0].GetAttribute("aria-expanded"));
        Assert.True(Chevrons(cut)[0].HasAttribute("aria-controls"));
        Assert.Equal("Skjul detaljer for 1. Tale", AccessibleName.Of(Chevrons(cut)[0]));
    }

    // -----------------------------------------------------------------------
    // Criteria 7 and 10: the sample stand-in copies Stiler's rule and adds nothing of its own.

    public static TheoryData<string> SampleStylesheets() =>
    [
        Repo.In("samples", "ModernHost", "wwwroot", "host.css"),
        Repo.In("samples", "LegacyHost", "wwwroot", "css", "host.css"),
    ];

    [Theory]
    [MemberData(nameof(SampleStylesheets))]
    public void SampleStylesheet_Always_ThenTheChevronHasStilersRulesAndNoOutline(string path)
    {
        // Exactly Stiler 0.1.98's rules. An outline here would pass every check and draw nothing on
        // helsedata, where body:not(.is-tabbing) button:focus removes it; the indicator is a fill.
        var css = Regex.Replace(File.ReadAllText(path), @"/\*.*?\*/", " ", RegexOptions.Singleline);
        var rules = Regex.Matches(css, @"([^{}]+)\{([^{}]*)\}")
            .Where(m => m.Groups[1].Value.Contains("munin-explorer-dataitem__expand-", StringComparison.Ordinal))
            .Select(m => Regex.Replace(m.Groups[1].Value.Trim(), @"\s+", " ") + " { "
                + string.Join("; ", m.Groups[2].Value
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Order(StringComparer.Ordinal)) + " }")
            .Order(StringComparer.Ordinal);

        Assert.Equal(
        [
            ".munin-explorer-data-list__item__row .munin-explorer-dataitem__expand-toggle:focus-visible[aria-expanded=\"false\"] .icon { background-image: url(../img/icons/keyboard-arrow/icon_down--white.svg) }",
            ".munin-explorer-data-list__item__row .munin-explorer-dataitem__expand-toggle:focus-visible[aria-expanded=\"true\"] .icon { background-image: url(../img/icons/keyboard-arrow/icon_up--white.svg) }",
            ".munin-explorer-dataitem-main .munin-explorer-dataitem__expand-toggle .icon { display: inline-block; margin: 0 }",
            ".munin-explorer-dataitem__expand-cell { display: flex }",
            ".munin-explorer-dataitem__expand-toggle { align-items: center; cursor: pointer; display: inline-flex; padding: 8px 12px }",
            ".munin-explorer-dataitem__expand-toggle:focus-visible { background-color: rgb(81, 84, 123); color: rgb(255, 255, 255) }",
        ],
            rules);
        Assert.DoesNotContain(rules, rule => rule.Contains("outline", StringComparison.Ordinal));

        // The set above is read without its @media context, so none of it may sit in one: Stiler's
        // 1280px placement assumes stacked rows, which the sample does not have.
        Assert.DoesNotMatch(@"@media[^{]*\{(?:[^{}]*\{[^{}]*\})*[^{}]*munin-explorer-dataitem__expand-", css);
    }
}
