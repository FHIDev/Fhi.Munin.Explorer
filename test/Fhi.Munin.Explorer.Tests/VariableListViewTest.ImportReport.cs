using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// A list imported from the old helsedata says what did not come over (Fhi.Metadata-9huq2.37): the rows not found and
/// the items left without their notes or desired data, until the reader hides it.
/// </summary>
public partial class VariableListViewTest
{
    private const string ImportNotice = "section.munin-explorer-list-import";

    private static readonly Guid FirstEntry = new("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid SecondEntry = new("a0000000-0000-0000-0000-000000000002");
    private static readonly Guid ThirdEntry = new("a0000000-0000-0000-0000-000000000003");
    private static readonly Guid FourthEntry = new("a0000000-0000-0000-0000-000000000004");

    private static ListImportNotFound NotFound(Guid id, int index, string status, string? code, string? datasamling = null) =>
        new() { EntryId = id, Index = index, Status = status, VariableCode = code, DataCollectionCode = datasamling };

    /// <summary>All three reasons and one the package does not know, sent out of order.</summary>
    private static ListImportReport FourNotFound() => new()
    {
        ImportedFrom = "helsedata-legacy",
        NotFound =
        [
            NotFound(ThirdEntry, 7, ListImportStatuses.Ambiguous, "V_TVETYDIG"),
            NotFound(FirstEntry, 1, ListImportStatuses.UnknownVariable, "V_BORTE") with
            {
                Notes = "Spør om 2012", DesiredDataFreeText = "Bare kvinner", Skipped = [ListImportFields.Notes, ListImportFields.DesiredData]
            },
            NotFound(SecondEntry, 4, ListImportStatuses.UnknownDatasamling, "V_ALDER", "DS_FEIL"),
            NotFound(FourthEntry, 9, "somethingNew", null, "DS_ALENE"),
        ]
    };

    private sealed class ImportClient(params VariableListItem[] items) : ListClient(items)
    {
        /// <summary>The first list's import fields; the others are lists made here.</summary>
        public Func<VariableList, VariableList> Imported { get; init; } = list => list with
        {
            ImportedFrom = "helsedata-legacy",
            ImportNotFoundCount = 4,
            ImportSkippedCount = 0
        };

        public ListImportReport? Report { get; set; } = FourNotFound();

        public Exception? ReportThrows { get; init; }
        public Exception? DismissThrows { get; set; }
        public Exception? EntryRemoveThrows { get; set; }

        /// <summary>Holds the first list's report read until released, so the reader can switch list meanwhile.</summary>
        public TaskCompletionSource<ListImportReport?>? HoldReport { get; init; }

        public List<Guid> ReportsAskedFor { get; } = [];
        public List<Guid> DismissedLists { get; } = [];
        public List<Guid> RemovedEntries { get; } = [];

        public override async Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            [.. (await base.GetMyListsAsync(cancellationToken)).Select(l => l.Id == ListId ? Imported(l) : l)];

        public override async Task<ListImportReport?> GetMyListImportReportAsync(Guid id, CancellationToken cancellationToken = default)
        {
            ReportsAskedFor.Add(id);

            if (ReportThrows is not null)
            {
                throw ReportThrows;
            }

            if (HoldReport is not null && id == ListId)
            {
                return await HoldReport.Task;
            }

            return id == ListId ? Report : null;
        }

        public override Task<bool> DismissMyListImportReportAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (DismissThrows is not null)
            {
                throw DismissThrows;
            }

            DismissedLists.Add(id);
            return Task.FromResult(true);
        }

        public override Task<bool> RemoveMyListImportEntryAsync(Guid id, Guid entryId, CancellationToken cancellationToken = default)
        {
            if (EntryRemoveThrows is not null)
            {
                throw EntryRemoveThrows;
            }

            RemovedEntries.Add(entryId);
            return Task.FromResult(true);
        }

        public override Task<SharedList?> GetSharedListAsync(string? code, CancellationToken cancellationToken = default) =>
            Task.FromResult<SharedList?>(new SharedList("Kollegas liste", [Item("Alder ved diagnose", "V_BDR.ALDER")]));
    }

    private IRenderedComponent<VariableListView> RenderImport(
        ImportClient client, string? language = null, bool signedIn = true, string? shareCode = null)
    {
        Services.AddSingleton(new CodePickerSwitch(true));
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        return Render<VariableListView>(p =>
        {
            p.Add(c => c.IsAuthenticated, signedIn);
            p.Add(c => c.ShareCode, shareCode);

            if (language is not null)
            {
                p.Add(c => c.Language, language);
            }
        });
    }

    private static string Text(AngleSharp.Dom.IElement element) =>
        System.Text.RegularExpressions.Regex.Replace(element.TextContent, @"\s+", " ").Trim();

    /// <summary>One entry's lines, each element its own string: the markup carries no whitespace between them.</summary>
    private static string Lines(AngleSharp.Dom.IElement entry) =>
        string.Join(" | ", entry.QuerySelectorAll("strong, p, button").Select(Text));

    private static string ImportButton(IRenderedComponent<VariableListView> cut, Guid entry) =>
        ((Dictionary<Guid, ElementReference>)typeof(VariableListView)
            .GetField("_importRemoveButtons", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(cut.Instance)!)[entry].Id;

    [Fact]
    public void ImportReport_WhenRowsWereNotFound_ThenTheNoticeNamesEachInIndexOrderWithItsReasonAndWords()
    {
        var cut = RenderImport(new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        var notice = cut.WaitForElement(ImportNotice);
        Assert.Equal("Importert fra den gamle listen: 4 variabler ble ikke funnet", Text(notice.QuerySelector("h3")!));
        Assert.Contains("Søk etter variabelen på navn, eller spør registeret som har dataene.", Text(notice));

        var entries = notice.QuerySelectorAll("li.munin-explorer-list-import__entry").Select(Lines).ToList();
        Assert.Equal(
            [
                "V_BORTE | Fant ikke variabelen i katalogen | Notat: Spør om 2012 | Ønskede data: Bare kvinner | "
                + "Notatet ble ikke med | Ønskede data ble ikke med | Fjern",
                "V_ALDER i datasamlingen DS_FEIL | Datasamlingen hører ikke til variabelen | Fjern",
                "V_TVETYDIG | Koden passer til flere variabler | Fjern",

                // A status the package does not know draws no reason sentence, never a raw status.
                "Datasamlingen DS_ALENE | Fjern",
            ],
            entries);
    }

    [Fact]
    public void ImportReport_WhenReadInEnglish_ThenEveryWordIsEnglish()
    {
        var cut = RenderImport(new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER")), language: "en");

        var notice = cut.WaitForElement(ImportNotice);
        Assert.Equal("Imported from the old list: 4 variables were not found", Text(notice.QuerySelector("h3")!));
        Assert.Contains("Search for the variable by name, or ask the registry that holds the data.", Text(notice));
        var entries = notice.QuerySelectorAll("li").Select(Lines).ToList();
        Assert.Equal(
            "V_BORTE | The variable was not found in the catalogue | Note: Spør om 2012 | Desired data: Bare kvinner | "
            + "The note was not brought over | The desired data was not brought over | Remove",
            entries[0]);
        Assert.Equal("V_ALDER in datasamling DS_FEIL | The datasamling does not belong to the variable | Remove", entries[1]);
        Assert.Equal("V_TVETYDIG | The code fits more than one variable | Remove", entries[2]);
        Assert.Equal("Datasamling DS_ALENE | Remove", entries[3]);
        Assert.Equal("Hide", Text(notice.QuerySelector(":scope > button")!));
    }

    [Theory]
    [InlineData(null, "Importert fra den gamle listen: 1 variabel ble ikke funnet")]
    [InlineData("en", "Imported from the old list: 1 variable was not found")]
    public void ImportReport_WhenOneRowWasNotFound_ThenTheHeadingIsSingular(string? language, string expected)
    {
        var client = new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            Report = new ListImportReport { NotFound = [NotFound(FirstEntry, 0, ListImportStatuses.UnknownVariable, "V_BORTE")] }
        };

        var cut = RenderImport(client, language);

        Assert.Equal(expected, Text(cut.WaitForElement($"{ImportNotice} h3")));
    }

    [Fact]
    public void ImportReport_WhenShown_ThenItIsARegionNamedByItsHeadingAndNotAnAlert()
    {
        // An alert or status would be read out on every visit to a list the reader has not hidden it on.
        var cut = RenderImport(new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        var notice = cut.WaitForElement(ImportNotice);
        Assert.Null(notice.GetAttribute("role"));
        Assert.Null(notice.GetAttribute("aria-live"));
        Assert.Equal(notice.GetAttribute("aria-labelledby"), notice.QuerySelector("h3")!.Id);
        Assert.Null(notice.Closest("[role=alert],[role=status],[aria-live]"));
    }

    [Fact]
    public void ImportReport_WhenARemoveButtonIsNamed_ThenItNamesTheCodesItRemoves()
    {
        var cut = RenderImport(new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER")));

        var button = cut.WaitForElement($"{ImportNotice} li button");
        Assert.Equal("Fjern V_BORTE", AccessibleName.Of(button));
    }

    [Fact]
    public void ImportReport_WhenBothCountsAreKnownAndZero_ThenNoReportIsRead()
    {
        var client = new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            Imported = l => l with { ImportedFrom = "helsedata-legacy", ImportNotFoundCount = 0, ImportSkippedCount = 0 }
        };

        var cut = RenderImport(client);

        cut.WaitForElement(OwnTable);
        Assert.Empty(client.ReportsAskedFor);
        Assert.Empty(cut.FindAll(ImportNotice));
    }

    [Fact]
    public void ImportReport_WhenTheListWasNotImported_ThenNoReportIsReadAtAll()
    {
        var client = new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            Imported = l => l with { ImportNotFoundCount = 4, ImportSkippedCount = 1 }
        };

        var cut = RenderImport(client);

        cut.WaitForElement(OwnTable);
        Assert.Empty(client.ReportsAskedFor);
        Assert.Empty(cut.FindAll(ImportNotice));
    }

    [Fact]
    public void ImportReport_WhenTheListSaysItWasHidden_ThenNoReportIsRead()
    {
        var client = new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            Imported = l => l with { ImportedFrom = "helsedata-legacy", ImportNotFoundCount = 4, ImportReportDismissed = true }
        };

        var cut = RenderImport(client);

        cut.WaitForElement(OwnTable);
        Assert.Empty(client.ReportsAskedFor);
        Assert.Empty(cut.FindAll(ImportNotice));
    }

    [Fact]
    public void ImportReport_WhenTheApiPredatesTheSkippedCount_ThenTheReportIsReadEvenWithNoRowNotFound()
    {
        // An unknown count is not 0: only the report can say whether a note was left off.
        var item = Item("Alder ved diagnose", "V_BDR.ALDER") with { ItemId = Guid.NewGuid() };
        var client = new ImportClient(item)
        {
            Imported = l => l with { ImportedFrom = "helsedata-legacy", ImportNotFoundCount = 0, ImportSkippedCount = null },
            Report = new ListImportReport { Skipped = [new ListImportSkipped { ItemId = item.ItemId!.Value, Fields = [ListImportFields.Notes] }] }
        };

        var cut = RenderImport(client);

        cut.WaitForElement(ImportNotice);
        Assert.Equal([ListId], client.ReportsAskedFor);
    }

    [Fact]
    public void ImportReport_WhenEveryRowMatchedButNotesWereLeftOff_ThenTheNoticeNamesThoseItems()
    {
        var onPage = Item("Alder ved diagnose", "V_BDR.ALDER") with { ItemId = Guid.NewGuid() };
        var client = new ImportClient(onPage)
        {
            Imported = l => l with { ImportedFrom = "helsedata-legacy", ImportNotFoundCount = 0, ImportSkippedCount = 4 },
            Report = new ListImportReport
            {
                Skipped =
                [
                    new ListImportSkipped { ItemId = onPage.ItemId!.Value, Fields = [ListImportFields.Notes] },
                    new ListImportSkipped
                    {
                        ItemId = Guid.NewGuid(), VariableCode = "V_KJONN", DataCollectionCode = "DS1",
                        Fields = [ListImportFields.Notes, ListImportFields.DesiredData]
                    },
                    new ListImportSkipped { ItemId = Guid.NewGuid(), Fields = [ListImportFields.DesiredData] },
                    new ListImportSkipped { ItemId = Guid.NewGuid(), Fields = [ListImportFields.DesiredData] },
                ]
            }
        };

        var cut = RenderImport(client);

        var notice = cut.WaitForElement(ImportNotice);
        Assert.Equal("Importert fra den gamle listen: noe ble ikke tatt med", Text(notice.QuerySelector("h3")!));
        Assert.DoesNotContain("Søk etter variabelen", Text(notice));
        Assert.Contains("For 4 variabler ble ønskede data eller notater ikke tatt med:", Text(notice));
        Assert.Equal(
            ["V_BDR.ALDER Alder ved diagnose: notatet", "V_KJONN i datasamlingen DS1: notatet og ønskede data",
             "2 variabler til, som ikke står på denne siden"],
            notice.QuerySelectorAll("li").Select(Text));
    }

    [Fact]
    public void ImportReport_WhenOneItemLostItsFieldsInEnglish_ThenTheSentenceIsSingularAndEnglish()
    {
        var client = new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            Imported = l => l with { ImportedFrom = "helsedata-legacy", ImportSkippedCount = 1 },
            Report = new ListImportReport
            {
                Skipped = [new ListImportSkipped { ItemId = Guid.NewGuid(), VariableCode = "V_KJONN", Fields = [ListImportFields.DesiredData] }]
            }
        };

        var cut = RenderImport(client, language: "en");

        var notice = cut.WaitForElement(ImportNotice);
        Assert.Equal("Imported from the old list: some of it was not brought over", Text(notice.QuerySelector("h3")!));
        Assert.Contains("For 1 variable the desired data or notes were not brought over:", Text(notice));
        Assert.Equal("V_KJONN: the desired data", Text(notice.QuerySelector("li")!));
    }

    [Fact]
    public void ImportReport_WhenTheReportSaysItWasHiddenElsewhere_ThenNothingIsDrawn()
    {
        // Another tab hid it after the lists were read: the report's own flag is the later word.
        var client = new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { Report = FourNotFound() with { Dismissed = true } };

        var cut = RenderImport(client);

        cut.WaitForAssertion(() => Assert.Equal([ListId], client.ReportsAskedFor));
        Assert.Empty(cut.FindAll(ImportNotice));
    }

    [Fact]
    public void ImportReport_WhenTheReportReadIsNotFound_ThenNothingIsDrawn()
    {
        var client = new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { Report = null };

        var cut = RenderImport(client);

        cut.WaitForAssertion(() => Assert.Equal([ListId], client.ReportsAskedFor));
        Assert.Empty(cut.FindAll(ImportNotice));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ImportReport_WhenTheReportReadFails_ThenNothingIsDrawnAndNothingIsSaid(bool throttled)
    {
        var client = new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            ReportThrows = throttled ? new MuninExplorerRateLimitedException(null) : new HttpRequestException("down")
        };

        var cut = RenderImport(client);

        cut.WaitForAssertion(() => Assert.Equal([ListId], client.ReportsAskedFor));
        Assert.Empty(cut.FindAll(ImportNotice));
        Assert.Equal("", cut.FindAll("[role=alert]")[0].TextContent.Trim());
    }

    [Fact]
    public void ImportReport_WhenSignedOut_ThenNothingIsReadOrDrawn()
    {
        var client = new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER"));

        var cut = RenderImport(client, signedIn: false);

        Assert.Empty(client.ReportsAskedFor);
        Assert.Empty(cut.FindAll(ImportNotice));
    }

    [Fact]
    public void ImportReport_WhenASharedListIsShown_ThenTheReadersOwnReportIsNotDrawnOverIt()
    {
        var cut = RenderImport(new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER")), shareCode: "AB12CD");

        cut.WaitForAssertion(() => Assert.Contains("Kollegas liste", cut.Markup));
        Assert.Empty(cut.FindAll(ImportNotice));
    }

    [Fact]
    public async Task ImportReport_WhenHidden_ThenTheApiIsToldAndItStaysGoneAfterASwitchAwayAndBack()
    {
        var client = new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { ListCount = 2 };
        var cut = RenderImport(client);
        cut.WaitForElement(ImportNotice);

        await cut.InvokeAsync(() => cut.FindAll($"{ImportNotice} > button").Single().Click());

        Assert.Equal([ListId], client.DismissedLists);
        Assert.Empty(cut.FindAll(ImportNotice));
        cut.WaitForAssertion(() => Assert.Equal(Held(cut, "_menuToggle"), FocusedId()));

        await cut.InvokeAsync(() => cut.Find("select").Change(ListClient.SecondListId.ToString()));
        await cut.InvokeAsync(() => cut.Find("select").Change(ListId.ToString()));

        cut.WaitForElement(OwnTable);
        Assert.Equal([ListId], client.ReportsAskedFor);
        Assert.Empty(cut.FindAll(ImportNotice));
    }

    [Fact]
    public async Task ImportReport_WhenAnEntryIsRemoved_ThenItLeavesTheCountDropsAndFocusGoesToTheNext()
    {
        var client = new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER"));
        var cut = RenderImport(client);
        cut.WaitForElement(ImportNotice);

        await cut.InvokeAsync(() => cut.Find($"#{cut.FindAll($"{ImportNotice} li button")[0].Id}").Click());

        Assert.Equal([FirstEntry], client.RemovedEntries);
        Assert.Equal("Importert fra den gamle listen: 3 variabler ble ikke funnet", Text(cut.Find($"{ImportNotice} h3")));
        Assert.DoesNotContain("V_BORTE", Text(cut.Find(ImportNotice)));
        cut.WaitForAssertion(() => Assert.Equal(ImportButton(cut, SecondEntry), FocusedId()));
    }

    [Fact]
    public async Task ImportReport_WhenTheLastEntryIsRemoved_ThenFocusGoesToHideWhileOthersStand()
    {
        var client = new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER"));
        var cut = RenderImport(client);
        cut.WaitForElement(ImportNotice);

        await cut.InvokeAsync(() => cut.FindAll($"{ImportNotice} li button")[3].Click());

        Assert.Equal([FourthEntry], client.RemovedEntries);
        cut.WaitForAssertion(() => Assert.Equal(Held(cut, "_importHideButton"), FocusedId()));
    }

    [Fact]
    public async Task ImportReport_WhenEveryEntryIsRemoved_ThenTheNoticeGoesAndIsNotReadAgain()
    {
        var client = new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            Imported = l => l with { ImportedFrom = "helsedata-legacy", ImportNotFoundCount = 1, ImportSkippedCount = 0 },
            Report = new ListImportReport { NotFound = [NotFound(FirstEntry, 0, ListImportStatuses.UnknownVariable, "V_BORTE")] },
            ListCount = 2
        };
        var cut = RenderImport(client);
        cut.WaitForElement(ImportNotice);

        await cut.InvokeAsync(() => cut.Find($"{ImportNotice} li button").Click());

        Assert.Empty(cut.FindAll(ImportNotice));
        cut.WaitForAssertion(() => Assert.Equal(Held(cut, "_menuToggle"), FocusedId()));

        // The holder's count went to 0 with it, so coming back asks for nothing.
        await cut.InvokeAsync(() => cut.Find("select").Change(ListClient.SecondListId.ToString()));
        await cut.InvokeAsync(() => cut.Find("select").Change(ListId.ToString()));
        cut.WaitForElement(OwnTable);
        Assert.Equal([ListId], client.ReportsAskedFor);
    }

    [Theory]
    [InlineData(true, false, "Du har gjort for mange forespørsler. Vent litt før du prøver igjen.")]
    [InlineData(false, false, "Kunne ikke endre listen nå. Prøv igjen om litt.")]
    [InlineData(true, true, "Du har gjort for mange forespørsler. Vent litt før du prøver igjen.")]
    [InlineData(false, true, "Kunne ikke endre listen nå. Prøv igjen om litt.")]
    public async Task ImportReport_WhenAWriteFails_ThenTheNoticeStandsAndTheAlertSaysWhy(bool throttled, bool hiding, string expected)
    {
        Exception failure = throttled ? new MuninExplorerRateLimitedException(null) : new HttpRequestException("down");
        var client = new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER"))
        {
            DismissThrows = hiding ? failure : null,
            EntryRemoveThrows = hiding ? null : failure
        };
        var cut = RenderImport(client);
        var before = Text(cut.WaitForElement(ImportNotice));

        await cut.InvokeAsync(() => (hiding ? cut.Find($"{ImportNotice} > button") : cut.Find($"{ImportNotice} li button")).Click());

        Assert.Equal(before, Text(cut.Find(ImportNotice)));
        Assert.Equal(expected, cut.FindAll("[role=alert]")[0].TextContent.Trim());
    }

    [Fact]
    public async Task ImportReport_WhenItsAnswerLandsAfterTheReaderSwitchedList_ThenItIsNotDrawn()
    {
        var held = new TaskCompletionSource<ListImportReport?>();
        var client = new ImportClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { ListCount = 2, HoldReport = held };
        var cut = RenderImport(client);
        cut.WaitForAssertion(() => Assert.Equal([ListId], client.ReportsAskedFor));

        await cut.InvokeAsync(() => cut.Find("select").Change(ListClient.SecondListId.ToString()));
        await cut.InvokeAsync(() => held.SetResult(FourNotFound()));

        Assert.Empty(cut.FindAll(ImportNotice));
    }
}
