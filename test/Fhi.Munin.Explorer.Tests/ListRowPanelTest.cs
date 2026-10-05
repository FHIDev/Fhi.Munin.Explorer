using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>A row of an own list opens into the variable's panel, with a way to share it (ADO 121586).</summary>
public class ListRowPanelTest : ExplorerTestContext
{
    private static readonly Guid ListId = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid HeartKildeId = new("cccccccc-0000-0000-0000-000000000001");

    private static readonly Guid OtherListId = new("22222222-2222-2222-2222-222222222222");

    private static readonly VariableListItem DatabaseVersion = new()
    {
        VariableId = new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
        VariableName = "Databaseversjon",
        VariableCode = "V_HKR.VERSJON_DATABASE",
        KildeId = HeartKildeId,
        KildeName = "Hjerte- og karregisteret",
        DataFrom = new DateTimeOffset(2012, 1, 1, 0, 0, 0, TimeSpan.Zero),
        DataTo = new DateTimeOffset(2022, 12, 31, 0, 0, 0, TimeSpan.Zero),
    };

    private static readonly VariableListItem Age = new()
    {
        VariableId = new Guid("aaaaaaaa-0000-0000-0000-000000000002"),
        VariableName = "Alder",
        VariableCode = "V_HKR.ALDER",
    };

    private sealed class PanelClient(params VariableListItem[] all) : EmptyMuninExplorerClient
    {
        private readonly List<VariableListItem> items = [.. all];

        public List<Guid> DetailsAskedFor { get; } = [];

        public List<bool> HistoricalAskedFor { get; } = [];

        public List<(Guid Variable, string? Text)> NotesWritten { get; } = [];

        public List<(Guid Variable, string? Text)> DesiredDataWritten { get; } = [];

        /// <summary>What the notes endpoint answers; it throws instead when this is null.</summary>
        public DesiredDataResult? NotesAnswer { get; set; } = new(DesiredDataOutcome.Saved);

        public bool NotesThrottled { get; set; }

        /// <summary>Holds the notes answer until the test releases it.</summary>
        public TaskCompletionSource? NotesGate { get; set; }

        /// <summary>One gate per write, in the order the writes are made, with the answer each gives.</summary>
        public Queue<(TaskCompletionSource Gate, DesiredDataResult Answer)> NotesGates { get; } = [];

        /// <summary>What the API holds after a write from somewhere else, such as another tab.</summary>
        public void NotesChangedElsewhere(Guid variableId, string notes)
        {
            var index = items.FindIndex(item => item.VariableId == variableId);
            items[index] = items[index] with { Notes = notes };
        }

        public override async Task<DesiredDataResult> SetMyListNotesAsync(
            Guid id, Guid variableId, string? text, CancellationToken cancellationToken = default)
        {
            NotesWritten.Add((variableId, text));

            if (NotesGates.TryDequeue(out var queued))
            {
                await queued.Gate.Task;
                return queued.Answer;
            }

            if (NotesGate is { } gate)
            {
                await gate.Task;
            }

            return NotesThrottled
                ? throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30))
                : NotesAnswer ?? throw new HttpRequestException("nede");
        }

        public override Task<DesiredDataResult> SetMyListDesiredDataAsync(
            Guid id, Guid variableId, string? freeText, CancellationToken cancellationToken = default)
        {
            DesiredDataWritten.Add((variableId, freeText));
            return Task.FromResult(new DesiredDataResult(DesiredDataOutcome.Saved));
        }

        public bool DetailMissing { get; init; }

        /// <summary>Held until the test releases it, as a browser's fetch is never instant.</summary>
        public TaskCompletionSource? DetailGate { get; init; }

        /// <summary>One nameless kildekodeverk, whose codes are fetched with the variable and never answer.</summary>
        public bool NamelessStalled { get; init; }

        public int CodeRequests { get; private set; }

        public List<string> CodeReferences { get; } = [];

        private readonly List<TaskCompletionSource<KodeverkCodes?>> _codeStalls = [];

        /// <summary>A second nameless kildekodeverk, fetched only after the first answers.</summary>
        public bool TwoNameless { get; init; }

        public void AnswerOldestCodes() =>
            _codeStalls.First(stall => !stall.Task.IsCompleted).TrySetResult(null);

        /// <summary>The detail's own period, where a test needs it to differ from the list's.</summary>
        public (DateTimeOffset? From, DateTimeOffset? To)? DetailPeriod { get; init; }

        public override Task<KodeverkCodes?> GetKodeverkCodesAsync(
            Guid variableId, string kodeverkType, string kodeverkReference, CancellationToken cancellationToken = default)
        {
            CodeRequests++;
            CodeReferences.Add(kodeverkReference);
            var stall = new TaskCompletionSource<KodeverkCodes?>();
            _codeStalls.Add(stall);
            return stall.Task;
        }

        /// <summary>A second list holding the same variables, which makes the list picker appear.</summary>
        public bool TwoLists { get; init; }

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>(TwoLists
                ? [new VariableList { Id = ListId, Name = "Hjertelista", VariableCount = items.Count(item => !_removed.Contains((ListId, item.VariableId))) },
                   new VariableList { Id = OtherListId, Name = "Kopien", VariableCount = items.Count }]
                : [new VariableList { Id = ListId, Name = "Hjertelista", VariableCount = items.Count }]);

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [.. items.Where(item => !_removed.Contains((id, item.VariableId)))],
                TotalCount = items.Count(item => !_removed.Contains((id, item.VariableId))),
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1,
            });

        public override Task<bool> AddVariablesToMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            _removed.RemoveWhere(entry => entry.List == id && variableIds.Contains(entry.Variable));
            items.AddRange(all.Where(item => variableIds.Contains(item.VariableId) && !items.Contains(item)));
            return Task.FromResult(true);
        }

        public bool RefuseRemoval { get; init; }

        private readonly HashSet<(Guid List, Guid Variable)> _removed = [];

        /// <summary>Held until the test releases it, so the reader can move on meanwhile.</summary>
        public TaskCompletionSource? RemovalGate { get; init; }

        public override async Task<bool> RemoveVariablesFromMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            if (RemovalGate is { } gate)
            {
                await gate.Task;
            }

            if (RefuseRemoval)
            {
                return false;
            }

            _removed.UnionWith(variableIds.Select(variable => (id, variable)));
            return true;
        }

        public override async Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default)
        {
            DetailsAskedFor.Add(id);
            HistoricalAskedFor.Add(includeHistorical);

            if (DetailGate is { } gate)
            {
                await gate.Task;
            }

            if (DetailMissing)
            {
                return null;
            }

            var item = all.Single(i => i.VariableId == id);

            return new VariableDetail
            {
                Id = id,
                Code = item.VariableCode ?? "",
                PreferredTerm = item.VariableName ?? "",
                Description = $"Om {item.VariableName}.",
                KildeName = "Hjerte- og karregisteret",
                DatasamlingName = "Alle variabler",
                VariabelgruppeName = "Datakilde",
                DataFrom = DetailPeriod is { } period ? period.From : item.DataFrom,
                DataTo = DetailPeriod is { } periodTo ? periodTo.To : item.DataTo,
                KodeverkLinks = (NamelessStalled, TwoNameless) switch
                {
                    (_, true) =>
                    [
                        new KodeverkLink { KodeverkType = "Kildekodeverk", KodeverkReference = "2336", HasCodeValues = true },
                        new KodeverkLink { KodeverkType = "Kildekodeverk", KodeverkReference = "2338", HasCodeValues = true },
                    ],
                    (true, _) => [new KodeverkLink { KodeverkType = "Kildekodeverk", KodeverkReference = "2336", HasCodeValues = true }],
                    _ => [],
                },
            };
        }
    }

    private IRenderedComponent<VariableListView> RenderView(
        PanelClient client, Func<VariableListItem, string>? variableHref = null)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();

        return Render<VariableListView>(p =>
        {
            p.Add(c => c.IsAuthenticated, true);

            if (variableHref is not null)
            {
                p.Add(c => c.VariableHref, variableHref);
            }
        });
    }

    private static IElement NameButton(IRenderedComponent<VariableListView> cut, string name) =>
        cut.FindAll("th[scope=row] button").Single(b => b.TextContent.Trim() == name);

    private static IElement? Panel(IRenderedComponent<VariableListView> cut) =>
        cut.FindAll("[id^='munin-explorer-list-panel-']").SingleOrDefault(e => e.GetAttribute("role") == "region");

    private static string Fact(IElement panel, string label) =>
        panel.QuerySelectorAll("dt").Single(dt => dt.TextContent.Trim() == label).NextElementSibling!.TextContent.Trim();

    [Fact]
    public void Row_WhenTheListOpens_ThenItIsAClosedDisclosure()
    {
        var cut = RenderView(new PanelClient(DatabaseVersion));

        var name = NameButton(cut, "Databaseversjon");

        Assert.Equal("false", name.GetAttribute("aria-expanded"));
        Assert.Null(name.GetAttribute("aria-controls"));
        Assert.Null(Panel(cut));
    }

    [Fact]
    public void Row_WhenItsNameIsPressed_ThenItsPanelOpensNamedAfterTheVariable()
    {
        var cut = RenderView(new PanelClient(DatabaseVersion));

        NameButton(cut, "Databaseversjon").Click();

        var panel = cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        var name = NameButton(cut, "Databaseversjon");

        Assert.Equal("true", name.GetAttribute("aria-expanded"));
        Assert.Equal(panel.Id, name.GetAttribute("aria-controls"));
        Assert.Equal("Databaseversjon", cut.Find($"#{panel.GetAttribute("aria-labelledby")}").TextContent.Trim());

        // The panel spans the whole row, so it is not squeezed into the name column.
        Assert.Equal(
            cut.FindAll("thead th").Count.ToString(),
            panel.ParentElement!.GetAttribute("colspan"));
    }

    [Fact]
    public void Panel_WhenTheDetailArrives_ThenItShowsDescriptionPeriodKildeAndVariabelgruppe()
    {
        var cut = RenderView(new PanelClient(DatabaseVersion));

        NameButton(cut, "Databaseversjon").Click();
        var panel = cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        Assert.Equal("Om Databaseversjon.", Fact(panel, "Beskrivelse"));
        Assert.Contains("2012", Fact(panel, "Dataperiode"));
        Assert.Contains("2022", Fact(panel, "Dataperiode"));
        Assert.Contains("Hjerte- og karregisteret", Fact(panel, "Kilde"));
        Assert.Contains("Alle variabler", Fact(panel, "Kilde"));
        Assert.Equal("Datakilde", Fact(panel, "Variabelgruppe"));
    }

    [Fact]
    public void Panel_WhenOpened_ThenItHasTheExplorersTwoTabsAndNotesAndTheSecondDoesNotRepeatTheDescription()
    {
        var cut = RenderView(new PanelClient(DatabaseVersion));

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=tablist]");

        Assert.Equal(["Data", "Om variabelen", "Mine notater"], cut.FindAll("[role=tab]").Select(t => t.TextContent.Trim()));
        Assert.Equal("true", cut.FindAll("[role=tab]")[0].GetAttribute("aria-selected"));

        cut.FindAll("[role=tab]")[1].Click();

        var about = cut.Find("[role=tabpanel]");
        Assert.Contains("V_HKR.VERSJON_DATABASE", about.TextContent);
        Assert.DoesNotContain("Om Databaseversjon.", about.TextContent);
    }

    [Fact]
    public void Row_WhenItsNameIsPressedAgain_ThenThePanelClosesAndNamesNothing()
    {
        var cut = RenderView(new PanelClient(DatabaseVersion));

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        NameButton(cut, "Databaseversjon").Click();

        Assert.Null(Panel(cut));
        Assert.Equal("false", NameButton(cut, "Databaseversjon").GetAttribute("aria-expanded"));
        Assert.Null(NameButton(cut, "Databaseversjon").GetAttribute("aria-controls"));
    }

    [Fact]
    public void Row_WhenAnotherRowIsOpened_ThenOnlyThatOneIsOpen()
    {
        var client = new PanelClient(DatabaseVersion, Age);
        var cut = RenderView(client);

        NameButton(cut, "Databaseversjon").Click();
        NameButton(cut, "Alder").Click();

        cut.WaitForAssertion(() =>
            Assert.Equal("Alder", cut.Find($"#{Panel(cut)!.GetAttribute("aria-labelledby")}").TextContent.Trim()));
        Assert.Equal("false", NameButton(cut, "Databaseversjon").GetAttribute("aria-expanded"));
        Assert.Equal([DatabaseVersion.VariableId, Age.VariableId], client.DetailsAskedFor);
    }

    [Fact]
    public async Task Panel_WhenTheReaderSwitchesList_ThenTheSameVariableThereIsClosed()
    {
        var cut = RenderView(new PanelClient(DatabaseVersion) { TwoLists = true });

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        await cut.InvokeAsync(() => cut.Find("select").Change(OtherListId.ToString()));

        cut.WaitForAssertion(() => Assert.Equal("false", NameButton(cut, "Databaseversjon").GetAttribute("aria-expanded")));
        Assert.Null(Panel(cut));

        await cut.InvokeAsync(() => cut.Find("select").Change(ListId.ToString()));

        cut.WaitForAssertion(() => Assert.Equal("false", NameButton(cut, "Databaseversjon").GetAttribute("aria-expanded")));
        Assert.Null(Panel(cut));
    }

    private static IElement RemoveButton(IRenderedComponent<VariableListView> cut, string name)
    {
        var nameId = cut.FindAll("th[scope=row] span[id]").Single(span => span.TextContent.Trim() == name).Id;
        return cut.FindAll("button").Single(b => b.GetAttribute("aria-labelledby")?.Contains(nameId!) == true);
    }

    [Fact]
    public async Task Row_WhenTheOpenVariableIsRemovedAndAddedBack_ThenItComesBackClosed()
    {
        var cut = RenderView(new PanelClient(DatabaseVersion, Age));

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        RemoveButton(cut, "Databaseversjon").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("th[scope=row] button")));

        var state = Services.GetRequiredService<VariableListState>();
        await cut.InvokeAsync(() => state.AddVariablesAsync(ListId, [DatabaseVersion.VariableId]));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("th[scope=row] button").Count));
        Assert.Equal("false", NameButton(cut, "Databaseversjon").GetAttribute("aria-expanded"));
        Assert.Null(Panel(cut));
    }

    [Fact]
    public void Row_WhenRemovingTheOpenVariableIsRefused_ThenItsPanelStaysOpen()
    {
        var cut = RenderView(new PanelClient(DatabaseVersion, Age) { RefuseRemoval = true });

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        RemoveButton(cut, "Databaseversjon").Click();

        Assert.Equal("true", NameButton(cut, "Databaseversjon").GetAttribute("aria-expanded"));
        Assert.NotNull(Panel(cut));
    }

    [Fact]
    public async Task Row_WhenARemovalLandsAfterTheSameVariableWasOpenedInAnotherList_ThenThatPanelStaysOpen()
    {
        var gate = new TaskCompletionSource();
        var cut = RenderView(new PanelClient(DatabaseVersion) { TwoLists = true, RemovalGate = gate });

        RemoveButton(cut, "Databaseversjon").Click();
        await cut.InvokeAsync(() => cut.Find("select").Change(OtherListId.ToString()));
        cut.WaitForAssertion(() => NameButton(cut, "Databaseversjon").Click());
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        await cut.InvokeAsync(gate.SetResult);
        await cut.InvokeAsync(() => Task.Delay(100));

        Assert.Equal("true", NameButton(cut, "Databaseversjon").GetAttribute("aria-expanded"));
        Assert.NotNull(Panel(cut));
    }

    [Fact]
    public async Task Row_WhenTheOpenVariableIsRemovedElsewhereAndAddedBack_ThenItComesBackClosed()
    {
        var cut = RenderView(new PanelClient(DatabaseVersion, Age));

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        // Another surface on the page, such as the search's save button, writes through the shared state.
        var state = Services.GetRequiredService<VariableListState>();
        await cut.InvokeAsync(() => state.RemoveVariablesAsync(ListId, [DatabaseVersion.VariableId]));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("th[scope=row] button")));
        await cut.InvokeAsync(() => state.AddVariablesAsync(ListId, [DatabaseVersion.VariableId]));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("th[scope=row] button").Count));
        Assert.Equal("false", NameButton(cut, "Databaseversjon").GetAttribute("aria-expanded"));
        Assert.Null(Panel(cut));
    }

    [Theory]
    [InlineData("Historical", true)]
    [InlineData("Active", false)]
    public void Panel_WhenTheVariableIsHistorical_ThenItsDetailIsAskedForWithHistoricalOnes(string status, bool historical)
    {
        var client = new PanelClient(DatabaseVersion with { VersionStatus = status });
        var cut = RenderView(client);

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        Assert.Equal([historical], client.HistoricalAskedFor);
    }

    [Fact]
    public void Row_WhenAnotherVariableIsRemoved_ThenTheOpenOneStaysOpen()
    {
        var cut = RenderView(new PanelClient(DatabaseVersion, Age));

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        RemoveButton(cut, "Alder").Click();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("th[scope=row] button")));
        Assert.Equal("true", NameButton(cut, "Databaseversjon").GetAttribute("aria-expanded"));
        Assert.NotNull(Panel(cut));
    }

    [Fact]
    public void Row_WhenItsVariableHasLeftTheCatalogue_ThenItsNameIsTextAndOpensNothing()
    {
        var orphan = new VariableListItem { VariableId = Guid.NewGuid() };
        var cut = RenderView(new PanelClient(DatabaseVersion, orphan));

        Assert.Single(cut.FindAll("th[scope=row] button"));
        Assert.Contains(cut.FindAll("th[scope=row] span"), s => s.TextContent.Trim() == "Variabelen er ikke tilgjengelig lenger");
    }

    private static IElement OpenNotes(IRenderedComponent<VariableListView> cut, string name = "Databaseversjon")
    {
        NameButton(cut, name).Click();
        cut.WaitForElement("[role=tablist]");
        cut.FindAll("[role=tab]").Single(t => t.TextContent.Trim() == "Mine notater").Click();
        var label = cut.FindAll("label").Single(l => l.TextContent.Trim() == "Egne notater");
        return cut.Find($"#{label.GetAttribute("for")}");
    }

    private static string NotesStatusText(IRenderedComponent<VariableListView> cut, IElement field) =>
        cut.Find($"#{field.GetAttribute("aria-describedby")}").TextContent.Trim();

    private static string PageAlert(IRenderedComponent<VariableListView> cut) =>
        string.Join(" ", cut.FindAll("[role=alert]").Select(alert => alert.TextContent.Trim()).Where(text => text.Length > 0));

    [Fact]
    public void Notes_WhenTheTabOpens_ThenTheSavedNotesAreInTheField()
    {
        var cut = RenderView(new PanelClient(DatabaseVersion with { Notes = "Spør om 2012" }));

        var field = OpenNotes(cut);

        Assert.Equal("textarea", field.TagName, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("Spør om 2012", field.GetAttribute("value"));
    }

    [Fact]
    public void Notes_WhileTyped_ThenNoKeystrokeReachesTheServer()
    {
        // A round trip per keystroke drops characters on a paste inside a Blazor Server circuit, the
        // reason KildeSearch and VariableSearch save on change too.
        var cut = RenderView(new PanelClient(DatabaseVersion));

        var field = OpenNotes(cut);

        Assert.Throws<MissingEventHandlerException>(() => field.Input("abc"));
    }

    private static IElement OpenNotesField(IRenderedComponent<VariableListView> cut)
    {
        var label = cut.FindAll("label").Single(l => l.TextContent.Trim() == "Egne notater");
        return cut.Find($"#{label.GetAttribute("for")}");
    }

    [Fact]
    public void Notes_WhenTheFieldIsLeft_ThenTheyAreSavedTrimmedAndKeptWhenTheRowIsReopened()
    {
        var client = new PanelClient(DatabaseVersion);
        var cut = RenderView(client);

        OpenNotes(cut).Change("  Spør om 2012  ");

        Assert.Equal([(DatabaseVersion.VariableId, (string?)"Spør om 2012")], client.NotesWritten);

        NameButton(cut, "Databaseversjon").Click();
        var reopened = OpenNotes(cut);
        Assert.Equal("Spør om 2012", reopened.GetAttribute("value"));
        Assert.Null(reopened.GetAttribute("aria-invalid"));
        Assert.Equal("", NotesStatusText(cut, reopened));
    }

    [Fact]
    public void Notes_WhenTheApiRefusesTheLength_ThenTheFieldIsMarkedAndTheCeilingIsSaid()
    {
        var client = new PanelClient(DatabaseVersion) { NotesAnswer = new(DesiredDataOutcome.Refused, 1500, 1600) };
        var cut = RenderView(client);

        OpenNotes(cut).Change(new string('x', 1600));

        var field = OpenNotesField(cut);
        Assert.Equal("true", field.GetAttribute("aria-invalid"));
        Assert.Equal("Notatene kan ikke overstige 1500 tegn. Teksten er ikke lagret.", NotesStatusText(cut, field));
        Assert.Equal(1600, field.GetAttribute("value")!.Length);
    }

    [Fact]
    public void Notes_WhenTheWriteFailsWithTheRowOpen_ThenItIsSaidUnderTheFieldOnlyAndTheTextStays()
    {
        var client = new PanelClient(DatabaseVersion) { NotesAnswer = null };
        var cut = RenderView(client);

        OpenNotes(cut).Change("Spør om 2012");

        var field = OpenNotesField(cut);
        Assert.Equal("Kunne ikke lagre notatene nå. Prøv igjen om litt.", NotesStatusText(cut, field));
        Assert.Equal("", PageAlert(cut));
        Assert.Equal("Spør om 2012", field.GetAttribute("value"));
        Assert.Null(field.GetAttribute("aria-invalid"));
    }

    [Fact]
    public void Notes_WhenTheWriteIsThrottled_ThenTheRateLimitIsSaid()
    {
        var client = new PanelClient(DatabaseVersion) { NotesThrottled = true };
        var cut = RenderView(client);

        OpenNotes(cut).Change("Spør om 2012");

        Assert.StartsWith("Du har gjort for mange forespørsler", NotesStatusText(cut, OpenNotesField(cut)));
        Assert.Equal("", PageAlert(cut));
    }

    [Fact]
    public void Notes_WhenAnotherRowOpens_ThenItShowsItsOwnNotesNotTheDraft()
    {
        var cut = RenderView(new PanelClient(DatabaseVersion with { Notes = "første" }, Age with { Notes = "andre" }));

        OpenNotes(cut).Change("lagret for den første");
        var other = OpenNotes(cut, "Alder");

        Assert.Equal("andre", other.GetAttribute("value"));
    }

    [Fact]
    public async Task Notes_WhenTheAnswerLandsAfterAnotherRowOpened_ThenThatRowSaysNothing()
    {
        var gate = new TaskCompletionSource();
        var client = new PanelClient(DatabaseVersion, Age)
        {
            NotesGate = gate,
            NotesAnswer = new(DesiredDataOutcome.Refused, 1500, 1600),
        };
        var cut = RenderView(client);

        OpenNotes(cut).Change(new string('x', 1600));
        var other = OpenNotes(cut, "Alder");
        await cut.InvokeAsync(gate.SetResult);

        other = OpenNotesField(cut);
        Assert.Null(other.GetAttribute("aria-invalid"));
        Assert.Equal("", NotesStatusText(cut, other));
    }

    [Fact]
    public void Notes_WhenTheSaveFailedAndTheRowIsReopened_ThenTheUnsavedTextIsStillThere()
    {
        var client = new PanelClient(DatabaseVersion, Age) { NotesAnswer = null };
        var cut = RenderView(client);

        OpenNotes(cut).Change("tekst som ikke ble lagret");
        OpenNotes(cut, "Alder");
        var again = OpenNotes(cut);

        Assert.Equal("tekst som ikke ble lagret", again.GetAttribute("value"));
        Assert.Equal("Kunne ikke lagre notatene nå. Prøv igjen om litt.", NotesStatusText(cut, again));
    }

    [Fact]
    public async Task Notes_WhenAnOlderAnswerLandsAfterANewerOne_ThenTheNewerOneStands()
    {
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();
        var client = new PanelClient(DatabaseVersion);
        client.NotesGates.Enqueue((first, new DesiredDataResult(DesiredDataOutcome.Refused, 1500, 1600)));
        client.NotesGates.Enqueue((second, new DesiredDataResult(DesiredDataOutcome.Saved)));
        var cut = RenderView(client);

        OpenNotes(cut).Change(new string('x', 1600));
        OpenNotesField(cut).Change("kortere");
        await cut.InvokeAsync(second.SetResult);
        await cut.InvokeAsync(first.SetResult);

        var field = OpenNotesField(cut);
        Assert.Null(field.GetAttribute("aria-invalid"));
        Assert.Equal("", NotesStatusText(cut, field));
    }

    [Fact]
    public async Task Notes_WhenASaveLandsAfterTheReaderSwitchedList_ThenTheOtherListDoesNotShowThem()
    {
        var gate = new TaskCompletionSource();
        var client = new PanelClient(DatabaseVersion) { TwoLists = true, NotesGate = gate };
        var cut = RenderView(client);

        OpenNotes(cut).Change("skrevet i liste A");
        await cut.InvokeAsync(() => cut.Find("select").Change(OtherListId.ToString()));
        await cut.InvokeAsync(gate.SetResult);

        cut.WaitForAssertion(() => Assert.Equal("", OpenNotes(cut).GetAttribute("value") ?? ""));
    }

    [Fact]
    public async Task Notes_WhenThePageIsReadAgainAfterAFailedSave_ThenTheUnsavedTextStays()
    {
        var client = new PanelClient(DatabaseVersion) { TwoLists = true, NotesAnswer = null };
        var cut = RenderView(client);

        OpenNotes(cut).Change("ikke lagret ennå");
        await cut.InvokeAsync(() => cut.Find("select").Change(OtherListId.ToString()));
        await cut.InvokeAsync(() => cut.Find("select").Change(ListId.ToString()));

        cut.WaitForAssertion(() => Assert.Equal("ikke lagret ennå", OpenNotes(cut).GetAttribute("value")));
    }

    [Fact]
    public async Task Notes_WhenAFailedSaveIsFollowedByAnotherWrite_ThenTheAlertNoLongerSaysIt()
    {
        var gate = new TaskCompletionSource();
        var client = new PanelClient(DatabaseVersion) { NotesAnswer = null, NotesGate = gate };
        var cut = RenderView(client);

        // Lands after the row is closed, so it goes to the list's alert.
        OpenNotes(cut).Change("feiler");
        NameButton(cut, "Databaseversjon").Click();
        await cut.InvokeAsync(gate.SetResult);
        Assert.NotEqual("", PageAlert(cut));

        cut.Find("td.munin-explorer-dataitem-main__desiredData input").Change("C76");

        Assert.Equal("", PageAlert(cut));
    }

    [Fact]
    public void Notes_WhileTheSaveIsOut_ThenTheFieldDoesNotClaimTheyAreUnsaved()
    {
        var client = new PanelClient(DatabaseVersion) { NotesGate = new TaskCompletionSource() };
        var cut = RenderView(client);

        OpenNotes(cut).Change("på vei");

        Assert.Equal("", NotesStatusText(cut, OpenNotesField(cut)));
    }

    [Fact]
    public async Task Notes_WhenAnotherRowsSaveSucceedsAfterAFailure_ThenTheFailureIsStillSaid()
    {
        var failing = new TaskCompletionSource();
        var succeeding = new TaskCompletionSource();
        var client = new PanelClient(DatabaseVersion, Age);
        client.NotesGates.Enqueue((failing, new DesiredDataResult(DesiredDataOutcome.NotFound)));
        client.NotesGates.Enqueue((succeeding, new DesiredDataResult(DesiredDataOutcome.Saved)));
        var cut = RenderView(client);

        OpenNotes(cut).Change("feiler");
        OpenNotes(cut, "Alder").Change("lagres");
        await cut.InvokeAsync(failing.SetResult);
        await cut.InvokeAsync(succeeding.SetResult);

        Assert.Equal("Kunne ikke lagre notatene nå. Prøv igjen om litt.", PageAlert(cut));
    }

    [Fact]
    public async Task Notes_WhenAFailureLandsAfterTheReaderSwitchedList_ThenTheOtherListSaysNothing()
    {
        var gate = new TaskCompletionSource();
        var client = new PanelClient(DatabaseVersion) { TwoLists = true, NotesGate = gate, NotesAnswer = null };
        var cut = RenderView(client);

        OpenNotes(cut).Change("feiler i liste A");
        await cut.InvokeAsync(() => cut.Find("select").Change(OtherListId.ToString()));
        await cut.InvokeAsync(gate.SetResult);

        Assert.Equal("", PageAlert(cut));
    }

    [Fact]
    public async Task Notes_WhenAVariableWithUnsavedNotesIsRemovedAndAddedBack_ThenTheyAreGone()
    {
        var client = new PanelClient(DatabaseVersion with { Notes = "lagret" }, Age) { NotesAnswer = null };
        var cut = RenderView(client);

        OpenNotes(cut).Change("ikke lagret");
        RemoveButton(cut, "Databaseversjon").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("th[scope=row] button")));
        var state = Services.GetRequiredService<VariableListState>();
        await cut.InvokeAsync(() => state.AddVariablesAsync(ListId, [DatabaseVersion.VariableId]));
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("th[scope=row] button").Count));

        var field = OpenNotes(cut);
        Assert.Equal("lagret", field.GetAttribute("value"));
        Assert.Equal("", NotesStatusText(cut, field));
    }

    [Fact]
    public async Task Notes_WhenASaveFromBeforeARemoveAndReAddLandsLast_ThenTheNewerSaveStands()
    {
        var old = new TaskCompletionSource();
        var newer = new TaskCompletionSource();
        var client = new PanelClient(DatabaseVersion, Age);
        client.NotesGates.Enqueue((old, new DesiredDataResult(DesiredDataOutcome.NotFound)));
        client.NotesGates.Enqueue((newer, new DesiredDataResult(DesiredDataOutcome.Saved)));
        var cut = RenderView(client);

        OpenNotes(cut).Change("gammel");
        RemoveButton(cut, "Databaseversjon").Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("th[scope=row] button")));
        var state = Services.GetRequiredService<VariableListState>();
        await cut.InvokeAsync(() => state.AddVariablesAsync(ListId, [DatabaseVersion.VariableId]));
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("th[scope=row] button").Count));
        OpenNotes(cut).Change("ny");
        await cut.InvokeAsync(newer.SetResult);
        await cut.InvokeAsync(old.SetResult);

        var field = OpenNotesField(cut);
        Assert.Equal("ny", field.GetAttribute("value"));
        Assert.Equal("", NotesStatusText(cut, field));
        Assert.Equal("", PageAlert(cut));
    }

    [Fact]
    public async Task Notes_WhenTheListIsEmptiedAndAVariableAddedBack_ThenItsUnsavedNotesAreGone()
    {
        var client = new PanelClient(DatabaseVersion with { Notes = "lagret" }, Age) { NotesAnswer = null };
        var cut = RenderView(client);

        OpenNotes(cut).Change("ikke lagret");
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Tøm liste").Click();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Ja, tøm listen").Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("th[scope=row] button")));
        var state = Services.GetRequiredService<VariableListState>();
        await cut.InvokeAsync(() => state.AddVariablesAsync(ListId, [DatabaseVersion.VariableId]));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("th[scope=row] button")));

        var field = OpenNotes(cut);
        Assert.Equal("lagret", field.GetAttribute("value"));
        Assert.Equal("", NotesStatusText(cut, field));
    }

    [Fact]
    public void Notes_WhenAnotherRowOpensAfterARefusal_ThenItIsNotMarked()
    {
        var client = new PanelClient(DatabaseVersion, Age) { NotesAnswer = new(DesiredDataOutcome.Refused, 1500, 1600) };
        var cut = RenderView(client);

        OpenNotes(cut).Change(new string('x', 1600));
        var other = OpenNotes(cut, "Alder");

        Assert.Null(other.GetAttribute("aria-invalid"));
        Assert.Equal("", NotesStatusText(cut, other));
    }

    [Fact]
    public async Task Notes_WhenThePageIsReadAgain_ThenWhatTheApiHoldsWinsOverThisSessionsSave()
    {
        var client = new PanelClient(DatabaseVersion) { TwoLists = true };
        var cut = RenderView(client);

        OpenNotes(cut).Change("skrevet her");
        client.NotesChangedElsewhere(DatabaseVersion.VariableId, "skrevet i en annen fane");
        await cut.InvokeAsync(() => cut.Find("select").Change(OtherListId.ToString()));
        await cut.InvokeAsync(() => cut.Find("select").Change(ListId.ToString()));

        cut.WaitForAssertion(() => Assert.Equal("skrevet i en annen fane", OpenNotes(cut).GetAttribute("value")));
    }

    [Fact]
    public void DesiredData_InTheDataTab_ShowsTheColumnsValueAndSavesThroughTheSameRoute()
    {
        var client = new PanelClient(DatabaseVersion with { DesiredDataType = "freeText", DesiredDataFreeText = "C76" });
        var cut = RenderView(client);

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=tablist]");
        var label = cut.FindAll("[role=tabpanel] label").Single(l => l.TextContent.Trim() == "Ønskede data");
        var field = cut.Find($"#{label.GetAttribute("for")}");

        Assert.Equal("C76", field.GetAttribute("value"));
        Assert.Contains(
            "Angi hvilke kodeverdier du ønsker å søke om",
            cut.Find($"#{field.GetAttribute("aria-describedby")!.Split(' ')[0]}").TextContent);

        field.Change("  C76 og C77  ");

        Assert.Equal([(DatabaseVersion.VariableId, (string?)"C76 og C77")], client.DesiredDataWritten);
        var column = cut.Find("td.munin-explorer-dataitem-main__desiredData input");
        Assert.Equal("C76 og C77", column.GetAttribute("value"));
    }

    [Fact]
    public void Panel_WhenTheVariableIsNotFound_ThenItSaysSoAndKeepsItsHeading()
    {
        var cut = RenderView(new PanelClient(DatabaseVersion) { DetailMissing = true });

        NameButton(cut, "Databaseversjon").Click();

        cut.WaitForAssertion(() =>
            Assert.Equal("Fant ingen detaljer for denne variabelen.", Panel(cut)!.QuerySelector("[role=status]")!.TextContent.Trim()));
        Assert.Empty(cut.FindAll("[role=tablist]"));
    }

    [Fact]
    public async Task Panel_WhileANamelessKodeverksCodesLoad_ThenItIsAlreadyDrawn()
    {
        var gate = new TaskCompletionSource();
        var client = new PanelClient(DatabaseVersion) { DetailGate = gate, NamelessStalled = true };
        var cut = RenderView(client);

        NameButton(cut, "Databaseversjon").Click();
        await cut.InvokeAsync(gate.SetResult);

        cut.WaitForAssertion(() => Assert.Equal(1, client.CodeRequests));
        cut.WaitForAssertion(() => Assert.NotNull(Panel(cut)!.QuerySelector("[role=tablist]")));
    }

    [Fact]
    public async Task NamelessCodes_WhenThePanelClosesWhileTheyLoad_ThenTheRestAreNotAskedFor()
    {
        var client = new PanelClient(DatabaseVersion) { TwoNameless = true };
        var cut = RenderView(client);

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForAssertion(() => Assert.Equal(["2336"], client.CodeReferences));
        NameButton(cut, "Databaseversjon").Click();
        await cut.InvokeAsync(client.AnswerOldestCodes);

        Assert.Equal(["2336"], client.CodeReferences);
    }

    [Fact]
    public void Period_WhenTheDetailKnowsNoPeriod_ThenTheListsIsShown()
    {
        var cut = RenderView(new PanelClient(DatabaseVersion) { DetailPeriod = (null, null) });

        NameButton(cut, "Databaseversjon").Click();
        var panel = cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        Assert.Contains("2012", Fact(panel, "Dataperiode"));
        Assert.Contains("2022", Fact(panel, "Dataperiode"));
    }

    [Fact]
    public void Period_WhenTheDetailKnowsOnlyItsEnd_ThenTheListsStartIsNotBorrowed()
    {
        var client = new PanelClient(DatabaseVersion)
        {
            DetailPeriod = (null, new DateTimeOffset(2024, 6, 30, 0, 0, 0, TimeSpan.Zero)),
        };
        var cut = RenderView(client);

        NameButton(cut, "Databaseversjon").Click();
        var panel = cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        Assert.Contains("2024", Fact(panel, "Dataperiode"));
        Assert.DoesNotContain("2012", Fact(panel, "Dataperiode"));
    }

    [Fact]
    public void Period_WhenTheDetailKnowsOnlyItsStart_ThenTheListsEndIsNotBorrowed()
    {
        var client = new PanelClient(DatabaseVersion)
        {
            DetailPeriod = (new DateTimeOffset(2015, 1, 1, 0, 0, 0, TimeSpan.Zero), null),
        };
        var cut = RenderView(client);

        NameButton(cut, "Databaseversjon").Click();
        var panel = cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        Assert.Contains("2015", Fact(panel, "Dataperiode"));
        Assert.DoesNotContain("2022", Fact(panel, "Dataperiode"));
    }

    [Fact]
    public void Panel_WhenTheHostGivesNoVariableAddress_ThenItOffersNoSharing()
    {
        var cut = RenderView(new PanelClient(DatabaseVersion));

        NameButton(cut, "Databaseversjon").Click();
        var panel = cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        Assert.DoesNotContain("Del variabel", panel.TextContent);
    }

    [Fact]
    public void CopyLink_WhenPressed_ThenTheVariablesAddressGoesToTheClipboardAndThatIsSaid()
    {
        var copied = JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true);
        copied.SetVoidResult();
        var cut = RenderView(new PanelClient(DatabaseVersion), item => $"https://helsedata.example/variabler?variabelId={item.VariableId}");

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Kopier lenke").Click();

        Assert.Equal(
            $"https://helsedata.example/variabler?variabelId={DatabaseVersion.VariableId}",
            copied.Invocations.Single().Arguments.Single());
        cut.WaitForAssertion(() => Assert.Contains(
            cut.FindAll("[role=status]"), s => s.TextContent.Trim() == "Lenken er kopiert."));
    }

    [Fact]
    public void CopyLink_WhenTheBrowserRefuses_ThenItPointsToTheEmailInstead()
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true).SetException(new JSException("NotAllowedError"));
        var cut = RenderView(new PanelClient(DatabaseVersion), _ => "https://helsedata.example/variabler");

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Kopier lenke").Click();

        cut.WaitForAssertion(() => Assert.Contains(
            cut.FindAll("[role=status]"),
            s => s.TextContent.Trim() == "Nettleseren tillot ikke kopiering. Kopier lenken under, eller send den på e-post."));

        var label = cut.FindAll("label").Single(l => l.TextContent.Trim() == "Lenke til variabelen");
        var field = cut.Find($"#{label.GetAttribute("for")}");
        Assert.Equal("https://helsedata.example/variabler", field.GetAttribute("value"));
        Assert.True(field.HasAttribute("readonly"));
    }

    [Fact]
    public void CopyLink_WhenPressedAgain_ThenTheStatusEmptiesFirstSoItIsAnnouncedAgain()
    {
        var copied = JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true);
        var cut = RenderView(new PanelClient(DatabaseVersion), _ => "https://helsedata.example/variabler");

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        Copy(cut);
        Assert.Equal("", LinkStatus(cut));
        cut.InvokeAsync(() => copied.SetVoidResult());
        cut.WaitForAssertion(() => Assert.Equal("Lenken er kopiert.", LinkStatus(cut)));

        JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true);
        Copy(cut);
        Assert.Equal("", LinkStatus(cut));
    }

    private static void Copy(IRenderedComponent<VariableListView> cut) =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Kopier lenke").Click();

    private static string LinkStatus(IRenderedComponent<VariableListView> cut) =>
        cut.Find("[role=group] [role=status]").TextContent.Trim();

    [Fact]
    public void CopyLink_WhenItFailedOnOneRow_ThenAnotherRowShowsNoField()
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true).SetException(new JSException("NotAllowedError"));
        var cut = RenderView(new PanelClient(DatabaseVersion, Age), item => $"https://helsedata.example/{item.VariableCode}");

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        Copy(cut);
        cut.WaitForElement("input[readonly]");

        NameButton(cut, "Alder").Click();
        cut.WaitForAssertion(() => Assert.Equal("Alder", cut.Find($"#{Panel(cut)!.GetAttribute("aria-labelledby")}").TextContent.Trim()));

        Assert.Empty(cut.FindAll("[role=region] input[readonly]"));
        Assert.Equal("", LinkStatus(cut));
    }

    [Fact]
    public async Task CopyLink_WhenItAnswersAfterAnotherRowOpened_ThenThatRowSaysNothing()
    {
        var copied = JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true);
        var cut = RenderView(new PanelClient(DatabaseVersion, Age), item => $"https://helsedata.example/{item.VariableCode}");

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        Copy(cut);
        NameButton(cut, "Alder").Click();
        cut.WaitForAssertion(() => Assert.Equal("Alder", cut.Find($"#{Panel(cut)!.GetAttribute("aria-labelledby")}").TextContent.Trim()));

        await cut.InvokeAsync(() => copied.SetVoidResult());
        await cut.InvokeAsync(() => Task.Delay(50));

        Assert.Equal("", LinkStatus(cut));
    }

    [Fact]
    public void CopyLink_WhenTheCircuitIsGone_ThenThePressDoesNotThrow()
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", _ => true).SetException(new JSDisconnectedException("gone"));
        var cut = RenderView(new PanelClient(DatabaseVersion), _ => "https://helsedata.example/variabler");

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");
        Copy(cut);

        cut.WaitForAssertion(() => Assert.StartsWith("Nettleseren tillot ikke kopiering", LinkStatus(cut)));
    }

    [Fact]
    public void SendByEmail_Always_ThenTheMessageCarriesTheNameAndTheAddress()
    {
        var cut = RenderView(new PanelClient(DatabaseVersion), _ => "https://helsedata.example/variabler?search=V_HKR&variabelId=1");

        NameButton(cut, "Databaseversjon").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        var href = Uri.UnescapeDataString(cut.FindAll("a").Single(a => a.TextContent.Trim() == "Send via e-post").GetAttribute("href")!);

        Assert.StartsWith("mailto:?subject=Variabel: Databaseversjon&body=", href);
        Assert.EndsWith("Databaseversjon: https://helsedata.example/variabler?search=V_HKR&variabelId=1", href);
    }

    private IRenderedComponent<VariableExplorer> OpenInExplorer(
        PanelClient client, IReadOnlyCollection<string>? declined = null)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        this.SetRendererInfo(new RendererInfo("Server", true));
        Services.GetRequiredService<NavigationManager>().NavigateTo("/variabler");

        var cut = Render<VariableExplorer>(p => p.Add(c => c.IsAuthenticated, true).Add(c => c.DeclinedKeys, declined));
        cut.FindAll("[role=tab]").Single(t => t.TextContent.Trim() == "Variabelliste").Click();
        cut.WaitForElement("th[scope=row] button").Click();
        cut.WaitForElement("[role=region][id^='munin-explorer-list-panel-']");

        return cut;
    }

    [Fact]
    public void Explorer_WhenARowIsShared_ThenTheLinkSearchesItsCodeWithinItsKilde()
    {
        var cut = OpenInExplorer(new PanelClient(DatabaseVersion));

        var mail = cut.WaitForElement("a[href^='mailto:']");
        var body = Uri.UnescapeDataString(mail.GetAttribute("href")!).Split("&body=")[1];
        var link = new Uri(body["Databaseversjon: ".Length..]);
        var state = ExplorerUrlState.Parse(link.Query);

        Assert.True(link.IsAbsoluteUri);
        Assert.Equal("V_HKR.VERSJON_DATABASE", state.Search);
        Assert.Equal([HeartKildeId], state.Filter.KildeIds);
        Assert.Equal(DatabaseVersion.VariableId, state.SelectedVariableId);
    }

    [Fact]
    public void Explorer_WhenTheHostDeclinesSearch_ThenThePanelOffersNoLinkThatCouldNotOpenIt()
    {
        var cut = OpenInExplorer(new PanelClient(DatabaseVersion), declined: ["search"]);

        Assert.DoesNotContain("Del variabel", cut.Find("[role=region][id^='munin-explorer-list-panel-']").TextContent);
    }

    [Theory]
    [InlineData("Historical")]
    [InlineData("historisk")]
    public void Explorer_WhenTheVariableIsHistorical_ThenTheLinkShowsHistoricalVariables(string status)
    {
        var cut = OpenInExplorer(new PanelClient(DatabaseVersion with { VersionStatus = status }));

        Assert.True(SharedLinkState(cut).Filter.IncludeHistorical);
    }

    [Fact]
    public void Explorer_WhenTheVariableIsActive_ThenTheLinkLeavesHistoricalVariablesOut()
    {
        var cut = OpenInExplorer(new PanelClient(DatabaseVersion with { VersionStatus = "Active" }));

        Assert.False(SharedLinkState(cut).Filter.IncludeHistorical);
    }

    private static ExplorerUrlState SharedLinkState(IRenderedComponent<VariableExplorer> cut)
    {
        var mail = cut.Find("a[href^='mailto:']");
        var body = Uri.UnescapeDataString(mail.GetAttribute("href")!).Split("&body=")[1];
        return ExplorerUrlState.Parse(new Uri(body[(body.IndexOf("http", StringComparison.Ordinal))..]).Query);
    }

    [Fact]
    public void Explorer_WhenTheVariableHasNoCode_ThenThePanelOffersNoLink()
    {
        var cut = OpenInExplorer(new PanelClient(DatabaseVersion with { VariableCode = null }));

        Assert.DoesNotContain("Del variabel", cut.Find("[role=region][id^='munin-explorer-list-panel-']").TextContent);
    }
}
