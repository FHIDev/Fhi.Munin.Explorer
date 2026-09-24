using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The open panel's save action. The state it shows belongs to the circuit, not to the row: results are
/// rebuilt whenever the facet counts change, so a button holding its own answer shows the wrong
/// word for a variable that is in the list.
/// </summary>
public class SaveToListTest : ExplorerTestContext
{
    private static readonly Guid ListId = Guid.NewGuid();

    private static Page<VariableSummary> OnePage(params VariableSummary[] rows) =>
        new() { Items = rows, TotalCount = rows.Length, PageNumber = 1, Size = 25, TotalPages = 1 };

    private static VariableSummary Variable(string name, string code) =>
        new() { Id = Guid.NewGuid(), Code = code, PreferredTerm = name, KildeName = "Als registeret" };

    private sealed class ListClient(Page<VariableSummary> answer) : EmptyMuninExplorerClient
    {
        public int SearchCalls { get; private set; }

        /// <summary>
        /// Rebuilds the rows as fresh objects with the same ids, the way a real refetch does. A fake
        /// that handed back the same instances would let a row that stashed "saved" on the DTO pass
        /// the survival test below.
        /// </summary>
        private Page<VariableSummary> FreshCopy() =>
            new()
            {
                Items = [.. answer.Items.Select(v => new VariableSummary
                {
                    Id = v.Id,
                    Code = v.Code,
                    PreferredTerm = v.PreferredTerm,
                    KildeName = v.KildeName
                })],
                TotalCount = answer.TotalCount,
                PageNumber = answer.PageNumber,
                Size = answer.Size,
                TotalPages = answer.TotalPages
            };
        public int AddCalls { get; private set; }
        public int RemoveCalls { get; private set; }
        public int MyListsCalls { get; private set; }
        public int MembershipCalls { get; private set; }
        public int CreateCalls { get; private set; }
        public readonly HashSet<Guid> Stored = [];

        /// <summary>Set when the reader is meant to already have a list.</summary>
        public bool HasExistingList { get; init; } = true;

        /// <summary>Refuse every add with the API's 429.</summary>
        public bool RateLimitAdd { get; init; }

        /// <summary>Refuse every add the way anything else that goes wrong refuses it.</summary>
        /// <remarks>
        /// Its own switch beside <see cref="RateLimitAdd"/> so the pair can be asserted against each
        /// other: the row has to say something different for each, and one flag could not show that.
        /// </remarks>
        public bool FailAdd { get; init; }

        /// <summary>Refuse every add with the API's 401/403, as if IsAuthenticated were wrong.</summary>
        public bool UnauthorizedAdd { get; init; }

        /// <summary>Refuse every membership read with the API's 429 while set.</summary>
        /// <remarks>
        /// Settable rather than <c>init</c>, unlike the two above: the point of the tests using it
        /// is what happens after the reader has waited, so it has to be turned off mid-test.
        /// </remarks>
        public bool RateLimitMembership { get; set; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            return Task.FromResult(FreshCopy());
        }

        /// <summary>Throw from every detail fetch, so the open panel shows its error.</summary>
        public bool FailDetail { get; init; }

        /// <summary>Never answer a detail fetch, so the open panel stays loading.</summary>
        public bool HangDetail { get; init; }

        public override Task<VariableDetail?> GetVariableAsync(
            Guid id, bool includeHistorical = false, CancellationToken cancellationToken = default)
        {
            if (FailDetail)
            {
                throw new HttpRequestException("nede");
            }

            if (HangDetail)
            {
                return new TaskCompletionSource<VariableDetail?>().Task;
            }

            var row = answer.Items.Single(v => v.Id == id);

            return Task.FromResult<VariableDetail?>(
                new VariableDetail { Id = id, Code = row.Code, PreferredTerm = row.PreferredTerm });
        }

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default)
        {
            MyListsCalls++;
            return Task.FromResult<IReadOnlyList<VariableList>>(
                HasExistingList ? [new VariableList { Id = ListId, Name = "Mine hjertevariabler" }] : []);
        }

        public override Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(new VariableList { Id = ListId, Name = name });
        }

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default)
        {
            MembershipCalls++;

            if (RateLimitMembership)
            {
                throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
            }

            return Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [.. Stored.Select(v => new VariableListItem { VariableId = v })],
                TotalCount = Stored.Count,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1
            });
        }

        public override Task<bool> AddVariablesToMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            AddCalls++;

            if (RateLimitAdd)
            {
                throw new MuninExplorerRateLimitedException(TimeSpan.FromSeconds(30));
            }

            if (UnauthorizedAdd)
            {
                throw new MuninExplorerUnauthorizedException();
            }

            if (FailAdd)
            {
                throw new HttpRequestException("nede");
            }

            foreach (var v in variableIds) { Stored.Add(v); }
            return Task.FromResult(true);
        }

        public override Task<bool> RemoveVariablesFromMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            RemoveCalls++;
            foreach (var v in variableIds) { Stored.Remove(v); }
            return Task.FromResult(true);
        }
    }

    private IRenderedComponent<VariableSearch> RenderSignedIn(ListClient client, bool signedIn = true)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        return Render<VariableSearch>(p => p.Add(c => c.IsAuthenticated, signedIn));
    }

    /// <summary>The row's column strip, which is the element a press on the row lands in.</summary>
    /// <remarks>Found on every call rather than held: a press re-renders the row.</remarks>
    private static IElement RowStrip(IRenderedComponent<VariableSearch> cut, int row = 0) =>
        cut.FindAll("ul.munin-explorer-data-list .munin-explorer-dataitem-main")[row];

    /// <summary>That row's disclosure, the name button, which is where its open-or-shut state is written.</summary>
    private static IElement RowToggle(IRenderedComponent<VariableSearch> cut, int row = 0) =>
        cut.FindAll("ul.munin-explorer-data-list button.munin-explorer-dataitem-main__name")[row];

    /// <summary>Opens <paramref name="row"/>'s panel unless it is already the open one.</summary>
    private static void Open(IRenderedComponent<VariableSearch> cut, int row = 0)
    {
        if (RowToggle(cut, row).GetAttribute("aria-expanded") != "true")
        {
            RowToggle(cut, row).Click(new MouseEventArgs { Detail = 1 });
        }
    }

    /// <summary>Every save button on the page, open panel or not.</summary>
    private static IReadOnlyList<IElement> SaveButtons(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll("button[aria-pressed]");

    /// <summary>The first row's save button, which lives in its open panel (Fhi.Metadata-35w0p.78).</summary>
    private static IElement SaveButton(IRenderedComponent<VariableSearch> cut, int row = 0)
    {
        Open(cut, row);

        return cut.Find(".munin-explorer-detail button[aria-pressed]");
    }

    // -----------------------------------------------------------------------

    [Fact]
    public void Panel_WhenTheReaderIsSignedOut_ThenThereIsNoSaveButtonAndNoListCall()
    {
        // Not a disabled button: a control that can never do anything is worse than no control.
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")));

        var cut = RenderSignedIn(client, signedIn: false);
        Open(cut);

        Assert.NotEmpty(cut.FindAll(".munin-explorer-detail"));
        Assert.Empty(SaveButtons(cut));
        Assert.Empty(cut.FindAll(".munin-explorer-data-list__save-status"));
        Assert.Equal(0, client.MyListsCalls);
        Assert.Equal(0, client.AddCalls);
    }

    [Fact]
    public void Panel_WhenTheReaderIsSignedIn_ThenOnlyTheOpenRowOffersToSave()
    {
        // A collapsed row is one Tab stop, as on helsedata's own list: saving costs no stop until
        // the reader has opened the row it belongs to. (Fhi.Metadata-35w0p.78)
        var client = new ListClient(OnePage(
            Variable("Alder ved diagnose", "V_BDR.ALDER"),
            Variable("Skjemastatus", "V_BDR.FORMSTATUS")));

        var cut = RenderSignedIn(client);

        Assert.Empty(SaveButtons(cut));

        Open(cut, 1);

        var button = Assert.Single(SaveButtons(cut));
        Assert.Equal("false", button.GetAttribute("aria-pressed"));
        Assert.Equal(cut.Find(".munin-explorer-detail").Id, button.Closest(".munin-explorer-detail")!.Id);
        Assert.NotNull(cut.FindAll("ul.munin-explorer-data-list > li")[1].QuerySelector("button[aria-pressed]"));
    }

    [Fact]
    public void Panel_WhenTheReaderIsSignedIn_ThenSaveSitsBesideShowWholeVariable()
    {
        var cut = RenderSignedIn(new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER"))));

        var button = SaveButton(cut);

        Assert.Equal("Vis hele variabelen", button.PreviousElementSibling!.TextContent.Trim());
        Assert.Null(button.Closest(".munin-explorer-dataitem-main"));
        Assert.Null(button.GetAttribute("role"));
        Assert.Equal("region", button.ParentElement!.GetAttribute("role"));
    }

    [Fact]
    public void Panel_WhenTheDetailFailsToLoad_ThenTheReaderCanStillSaveTheVariable()
    {
        // The row's own summary is enough to save by, so a failed fetch must not take the only
        // way to save it away with the tabs. (Fhi.Metadata-35w0p.83)
        var variable = Variable("Alder ved diagnose", "V_BDR.ALDER");
        var client = new ListClient(OnePage(variable)) { FailDetail = true };

        var cut = RenderSignedIn(client);
        Open(cut);

        Assert.Equal("Kunne ikke hente detaljene nå. Prøv igjen om litt.", DetailStatusText(cut));
        Assert.Empty(cut.FindAll(".munin-explorer-detail [role=tablist]"));

        var button = cut.Find(".munin-explorer-detail button[aria-pressed]");
        Assert.Equal("false", button.GetAttribute("aria-pressed"));
        Assert.All(
            button.GetAttribute("aria-labelledby")!.Split(' '),
            id => Assert.NotNull(cut.Find($"#{id}")));
        Assert.Single(cut.FindAll(".munin-explorer-detail .munin-explorer-data-list__save-status [role=alert]"));

        button.Click();

        Assert.Equal(1, client.AddCalls);
        Assert.Equal(variable.Id, Assert.Single(client.Stored));
        Assert.Equal("true", cut.Find(".munin-explorer-detail button[aria-pressed]").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Panel_WhenTheDetailIsStillLoading_ThenTheSaveButtonIsAlreadyThere()
    {
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER"))) { HangDetail = true };

        var cut = RenderSignedIn(client);
        Open(cut);

        Assert.Equal("true", cut.Find(".munin-explorer-detail").GetAttribute("aria-busy"));
        Assert.Single(cut.FindAll(".munin-explorer-detail button[aria-pressed]"));
    }

    [Fact]
    public void Panel_WhenTheDetailFailsForASignedOutReader_ThenThereIsStillNoSaveButton()
    {
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER"))) { FailDetail = true };

        var cut = RenderSignedIn(client, signedIn: false);
        Open(cut);

        Assert.Equal("Kunne ikke hente detaljene nå. Prøv igjen om litt.", DetailStatusText(cut));
        Assert.Empty(SaveButtons(cut));
        Assert.Empty(cut.FindAll(".munin-explorer-data-list__save-status"));
    }

    private static string DetailStatusText(IRenderedComponent<VariableSearch> cut) =>
        cut.Find(".munin-explorer-detail > p[role=status]").TextContent.Trim();

    [Fact]
    public void SaveButton_WhenItIsPressed_ThenItSavesAndThePanelStaysOpen()
    {
        var client = new ListClient(OnePage(
            Variable("Alder ved diagnose", "V_BDR.ALDER"),
            Variable("Skjemastatus", "V_BDR.FORMSTATUS")));

        var cut = RenderSignedIn(client);

        SaveButton(cut).Click();

        Assert.Equal(1, client.AddCalls);
        Assert.Single(client.Stored);
        Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed"));
        Assert.Equal("true", RowToggle(cut).GetAttribute("aria-expanded"));
        Assert.Single(cut.FindAll(".munin-explorer-detail"));
    }

    [Fact]
    public void Panel_WhenEachRowIsOpenedInTurn_ThenEachSaveButtonNamesItsOwnVariable()
    {
        // Two rows, because the weak version of this assertion — "the button has an accessible
        // name" — is satisfied by a constant label, the same "Lagre i liste" a screen reader hears
        // from the visible words alone. Distinctness is what makes the assertion mean anything.
        var client = new ListClient(OnePage(
            Variable("Alder ved diagnose", "V_BDR.ALDER"),
            Variable("Skjemastatus", "V_BDR.FORMSTATUS")));

        var cut = RenderSignedIn(client);

        var names = new[] { AccessibleName.Of(SaveButton(cut, 0)), AccessibleName.Of(SaveButton(cut, 1)) };

        Assert.Contains("Alder ved diagnose", names[0], StringComparison.Ordinal);
        Assert.Contains("Skjemastatus", names[1], StringComparison.Ordinal);
        Assert.Equal(2, names.Distinct(StringComparer.Ordinal).Count());

        // The visible words are still a contiguous part of the sentence, so a speech-input user
        // saying what they can see hits the button. WCAG 2.5.3.
        Assert.All(names, name => Assert.Contains("Lagre i liste", name, StringComparison.Ordinal));
    }

    [Fact]
    public void Row_WhenThePageIsEnglish_ThenTheSaveButtonKeepsEachHalfOfItsNameInItsOwnLanguage()
    {
        // The reason the name is two elements rather than one aria-label: "Save to list" is ours and
        // follows Language, "Alder ved diagnose" is Munin's and is Norwegian whatever the
        // surrounding UI is. WCAG 3.1.2.
        Services.AddSingleton<IMuninExplorerClient>(
            new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER"))));
        Services.AddScoped<VariableListState>();

        var cut = Render<VariableSearch>(p => p
            .Add(c => c.IsAuthenticated, true)
            .Add(c => c.Language, "en"));

        var button = SaveButton(cut);

        Assert.Equal("Save to list Alder ved diagnose", AccessibleName.Of(button));
        Assert.Null(button.GetAttribute("aria-label"));

        var referenced = button.GetAttribute("aria-labelledby")!.Split(' ');
        var nameSpan = cut.Find($"#{referenced[1]}");

        Assert.Equal("Alder ved diagnose", nameSpan.TextContent.Trim());
        Assert.Equal("no", nameSpan.GetAttribute("lang"));
        Assert.Equal(button.Id, referenced[0]);
    }

    [Fact]
    public void Row_WhenAVariableHasNoPreferredTerm_ThenItsButtonsAnnounceTheCodeOrTheirOwnWords()
    {
        // PreferredTerm defaults to "" and the row renders it blank. The save button borrows that
        // empty span, so it falls back to its own words rather than a sentence with a hole in it;
        // the disclosure names the row by its code (Fhi.Metadata-w13lk).
        var client = new ListClient(OnePage(Variable("", "V_BDR.ALDER")));
        var cut = RenderSignedIn(client);

        Assert.Equal("Vis detaljer for V_BDR.ALDER", AccessibleName.Of(RowToggle(cut)));
        Assert.Equal("Lagre i liste", AccessibleName.Of(SaveButton(cut)));
        Assert.Equal("Skjul detaljer for V_BDR.ALDER", AccessibleName.Of(RowToggle(cut)));
    }

    [Fact]
    public void Row_WhenAVariableHasAPreferredTerm_ThenTheDisclosureNamesItInsideItsOwnSentence()
    {
        // The visible name is a contiguous part of the label, so a speech-input user saying the words
        // they can see still reaches the control (WCAG 2.5.3).
        var cut = RenderSignedIn(new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER"))));

        Assert.Equal("Vis detaljer for Alder ved diagnose", AccessibleName.Of(RowToggle(cut)));
        Assert.Contains(RowToggle(cut).TextContent.Trim(), AccessibleName.Of(RowToggle(cut)), StringComparison.Ordinal);
    }

    [Fact]
    public void Row_WhenARowIsDrawn_ThenTheNameSpanCarriesItsIdOnceWhetherThePanelIsOpenOrShut()
    {
        // The save button borrows the name span by id. Two elements carrying that id would be a
        // WCAG 4.1.1 failure and would aim the button at whichever came first.
        var cut = RenderSignedIn(new ListClient(OnePage(
            Variable("Alder ved diagnose", "V_BDR.ALDER"),
            Variable("Skjemastatus", "V_BDR.FORMSTATUS"))));

        for (var row = 0; row < 2; row++)
        {
            var referenced = SaveButton(cut, row).GetAttribute("aria-labelledby")!.Split(' ');

            Assert.Equal(2, referenced.Length);
            Assert.All(referenced, id => Assert.Single(cut.FindAll($"#{id}")));
            Assert.Equal(RowToggle(cut, row).Id, cut.Find($"#{referenced[1]}").Closest("button")!.Id);
        }
    }

    [Fact]
    public void Row_WhenTwoExplorersShareAPage_ThenEverySaveButtonBorrowsItsOwnRowsName()
    {
        // helsedata's CMS can put two explorers on one page, which is why every id carries a
        // per-mount discriminator. One render fragment holding both: two Render calls are two
        // documents, where a repeated id is no duplicate.
        Services.AddSingleton<IMuninExplorerClient>(
            new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER"))));
        Services.AddScoped<VariableListState>();

        var page = Render(builder =>
        {
            builder.OpenComponent<VariableSearch>(0);
            builder.AddComponentParameter(1, nameof(VariableSearch.IsAuthenticated), true);
            builder.CloseComponent();
            builder.OpenComponent<VariableSearch>(2);
            builder.AddComponentParameter(3, nameof(VariableSearch.IsAuthenticated), true);
            builder.CloseComponent();
        });

        page.FindAll("button.munin-explorer-dataitem-main__name")[0].Click(new MouseEventArgs { Detail = 1 });
        page.FindAll("button.munin-explorer-dataitem-main__name")[1].Click(new MouseEventArgs { Detail = 1 });

        var buttons = page.FindAll("button[aria-pressed]");

        Assert.Equal(2, buttons.Count);

        var first = buttons[0].GetAttribute("aria-labelledby")!.Split(' ');
        var second = buttons[1].GetAttribute("aria-labelledby")!.Split(' ');

        Assert.NotEqual(first[0], second[0]);
        Assert.NotEqual(first[1], second[1]);
        Assert.All(first.Concat(second), id => Assert.Single(page.FindAll($"#{id}")));

        // Each button has to point at the name span in its OWN row: both explorers list the same
        // variable, so no assertion on the announced text could tell them apart.
        for (var i = 0; i < buttons.Count; i++)
        {
            var row = buttons[i].Closest("li[role=row]")!;
            var borrowed = i == 0 ? first[1] : second[1];

            Assert.NotNull(row.QuerySelector($"#{borrowed}"));
            Assert.Equal("Lagre i liste Alder ved diagnose", AccessibleName.Of(buttons[i]));
        }
    }

    [Fact]
    public void Row_WhenAVariableIsSaved_ThenItsButtonStillNamesItInTheOtherState()
    {
        // One control in two states, and the accessible name has to follow the word the same way
        // aria-pressed does.
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")));
        var cut = RenderSignedIn(client);

        SaveButton(cut).Click();

        Assert.Equal("Fjern fra liste Alder ved diagnose", AccessibleName.Of(SaveButton(cut)));
    }

    [Fact]
    public void Row_WhenSaveIsPressed_ThenTheVariableIsInTheListAndTheButtonSaysSo()
    {
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")));
        var cut = RenderSignedIn(client);

        SaveButton(cut).Click();

        Assert.Equal(1, client.AddCalls);
        Assert.Single(client.Stored);
        Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Row_WhenSaveIsPressedTwice_ThenTheVariableIsTakenOutAgain()
    {
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")));
        var cut = RenderSignedIn(client);

        SaveButton(cut).Click();
        SaveButton(cut).Click();

        Assert.Equal(1, client.RemoveCalls);
        Assert.Empty(client.Stored);
        Assert.Equal("false", SaveButton(cut).GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Row_WhenTheReaderHasNoListYet_ThenSavingMakesOneFirst()
    {
        // helsedata's 118497: the same action for a reader with nothing saved. Refusing until they
        // had made a list elsewhere would make the button lie about what it does.
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")))
        {
            HasExistingList = false
        };
        var cut = RenderSignedIn(client);

        SaveButton(cut).Click();

        Assert.Equal(1, client.CreateCalls);
        Assert.Single(client.Stored);
    }

    [Fact]
    public void Row_WhenTheVariableIsAlreadyInTheList_ThenTheFirstRenderSaysSo()
    {
        // Without preloading the membership the set is empty until the first save, so a variable
        // saved yesterday offers «Lagre i liste» and the press takes it out.
        var already = Variable("Alder ved diagnose", "V_BDR.ALDER");
        var client = new ListClient(OnePage(already));
        client.Stored.Add(already.Id);

        var cut = RenderSignedIn(client);

        Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Row_WhenAnAlreadySavedVariableIsPressed_ThenItIsRemovedRatherThanAddedTwice()
    {
        var already = Variable("Alder ved diagnose", "V_BDR.ALDER");
        var client = new ListClient(OnePage(already));
        client.Stored.Add(already.Id);
        var cut = RenderSignedIn(client);

        SaveButton(cut).Click();

        Assert.Equal(1, client.RemoveCalls);
        Assert.Equal(0, client.AddCalls);
        Assert.Empty(client.Stored);
    }

    [Fact]
    public void Panel_WhenNothingHasFailed_ThenTheAlertContainerIsAlreadyInTheDom()
    {
        // A role="alert" inserted and filled in the same update is announced unreliably, so the
        // container arrives with the panel, empty, before any press.
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")));

        var cut = RenderSignedIn(client);
        Open(cut);

        var alert = cut.FindAll(".munin-explorer-data-list__save-status [role=alert]");
        Assert.Single(alert);
        Assert.Equal("", alert[0].TextContent.Trim());
    }

    [Fact]
    public void Panel_WhenASaveFails_ThenItsSentenceFollowsTheButtonInsideThePanel()
    {
        // Heard with the panel it concerns, right after the control that failed, and not in the
        // row's column strip, where it once had a fixed width and was cut off (Fhi.Metadata-q7i5e).
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER"))) { FailAdd = true };
        var cut = RenderSignedIn(client);

        SaveButton(cut).Click();

        var alert = cut.Find("[role=alert]:not(:empty)");
        var status = alert.ParentElement!;

        Assert.Contains("Kunne ikke lagre", alert.TextContent);
        Assert.Equal("munin-explorer-data-list__save-status", status.ClassName);
        Assert.Equal(SaveButton(cut).Id, status.PreviousElementSibling!.Id);
        Assert.Null(alert.Closest(".munin-explorer-dataitem-main"));
    }

    [Fact]
    public void Row_WhenTheSaveIsRateLimited_ThenTheRowSaysSoRatherThanThatSavingFailed()
    {
        // The writes go through the same per-address limiter as the reads. "Prøv igjen om litt"
        // would advise the one thing that keeps the window full.
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")))
        {
            RateLimitAdd = true
        };
        var cut = RenderSignedIn(client);

        SaveButton(cut).Click();

        var alert = cut.Find(".munin-explorer-data-list__save-status [role=alert]");

        Assert.Contains("for mange forespørsler", alert.TextContent);
        Assert.DoesNotContain("Kunne ikke lagre", alert.TextContent);
    }

    [Fact]
    public void Row_WhenTheSaveFailsForAnyOtherReason_ThenTheRowStillSaysTheSaveFailed()
    {
        // The other half of the pair: the throttled sentence must not swallow the ordinary one.
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")))
        {
            FailAdd = true
        };
        var cut = RenderSignedIn(client);

        SaveButton(cut).Click();

        var alert = cut.Find(".munin-explorer-data-list__save-status [role=alert]");

        Assert.Contains("Kunne ikke lagre", alert.TextContent);
        Assert.DoesNotContain("for mange forespørsler", alert.TextContent);
    }

    [Fact]
    public void Row_WhenTheHostClaimsSignInButTheApiAnswersUnauthorized_ThenTheRowSaysToSignIn()
    {
        // The trap: signedIn stays true below, the same wrong claim MuninRuna hard-codes. Only the
        // API's 401/403 can be trusted — re-reading IsAuthenticated would repeat the host's claim.
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")))
        {
            UnauthorizedAdd = true
        };
        var cut = RenderSignedIn(client, signedIn: true);

        SaveButton(cut).Click();

        var alert = cut.Find(".munin-explorer-data-list__save-status [role=alert]");

        Assert.Contains("ikke logget inn", alert.TextContent);
        Assert.DoesNotContain("Kunne ikke lagre nå", alert.TextContent);
        Assert.DoesNotContain("for mange forespørsler", alert.TextContent);
    }

    [Fact]
    public void Mount_WhenTheListBootstrapIsRateLimited_ThenThePageStillRenders()
    {
        // The mount fires the search, the facet refresh and this list read together — the burst the
        // limiter counts. Left uncaught, the 429 tears down helsedata's circuit for the whole page.
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")))
        {
            RateLimitMembership = true
        };

        var cut = RenderSignedIn(client);

        Assert.Equal(1, client.MembershipCalls);
        Assert.Single(cut.FindAll("ul.munin-explorer-data-list > li"));
        Assert.NotNull(SaveButton(cut));

        // Nothing is claimed about the list either way: the reader has touched nothing yet.
        Assert.Empty(cut.Find("[role='alert']").TextContent.Trim());
    }

    [Fact]
    public void Mount_WhenTheListBootstrapWasRateLimited_ThenTheNextSavePutsTheOtherRowsRight()
    {
        // Why "wait and try again" is honest: the refused read leaves membership empty, so a
        // variable saved yesterday renders unsaved; the next press reads it again.
        var alder = Variable("Alder ved diagnose", "V_BDR.ALDER");
        var status = Variable("Skjemastatus", "V_BDR.FORMSTATUS");
        var client = new ListClient(OnePage(alder, status)) { RateLimitMembership = true };

        client.Stored.Add(status.Id);

        var cut = RenderSignedIn(client);

        // Wrong, and knowably so: the read that would have said otherwise was refused.
        Assert.Equal("false", SaveButton(cut, 1).GetAttribute("aria-pressed"));
        Assert.Equal("false", SaveButton(cut, 0).GetAttribute("aria-pressed"));

        client.RateLimitMembership = false;
        SaveButton(cut, 0).Click();

        Assert.Equal("true", SaveButton(cut, 0).GetAttribute("aria-pressed"));
        Assert.Equal("true", SaveButton(cut, 1).GetAttribute("aria-pressed"));
        Assert.Equal(2, client.Stored.Count);
        Assert.Contains(alder.Id, client.Stored);
        Assert.Contains(status.Id, client.Stored);
    }

    [Fact]
    public void Mount_WhenTheFirstPressAfterARefusedBootstrapIsOnASavedRow_ThenItIsNotRemoved()
    {
        // The refused read draws every row as unsaved, and the press refills the set mid-call. A
        // press that then asked the refilled set which way to go would delete a variable whose
        // button says "Lagre"; the direction comes from the row as it was drawn instead.
        var status = Variable("Skjemastatus", "V_BDR.FORMSTATUS");
        var client = new ListClient(OnePage(status)) { RateLimitMembership = true };

        client.Stored.Add(status.Id);

        var cut = RenderSignedIn(client);

        Assert.Equal("false", SaveButton(cut).GetAttribute("aria-pressed"));
        Assert.Contains("Lagre", SaveButton(cut).TextContent);

        client.RateLimitMembership = false;
        SaveButton(cut).Click();

        Assert.Contains(status.Id, client.Stored);
        Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed"));
        Assert.Equal(0, client.RemoveCalls);
        Assert.Empty(cut.Find("[role='alert']").TextContent.Trim());
    }

    // -----------------------------------------------------------------------
    // The trap.
    // -----------------------------------------------------------------------

    [Fact]
    public void Row_WhenTheResultsAreRebuilt_ThenTheSavedStateSurvives()
    {
        // The rows are redrawn whenever the facet counts change. A button that remembered "saved"
        // itself would forget it here. Asserted after a real refetch, not after one click.
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")));
        var cut = RenderSignedIn(client);

        SaveButton(cut).Click();
        Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed"));

        var before = client.SearchCalls;
        cut.Find("button[type=submit]").Click();
        Assert.True(client.SearchCalls > before, "the search did not refetch, so nothing was rebuilt");

        Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed"));
        Assert.Equal(1, client.AddCalls);
    }

    // ---- no save column: not in the header, not in the picker (Fhi.Metadata-35w0p.78) ----

    /// <summary>The header row's cell names, in order.</summary>
    private static IReadOnlyList<string> HeaderNames(IRenderedComponent<VariableSearch> cut) =>
        [.. cut.FindAll(".munin-explorer-dataitem-header [role=columnheader]").Select(h => h.TextContent.Trim())];

    /// <summary>The picker's column names, in the order it lists them.</summary>
    private static IReadOnlyList<string> PickerNames(IRenderedComponent<VariableSearch> cut) =>
        [.. cut.FindAll(".dropdown-choicepicker__item .form-control__label").Select(l => l.TextContent.Trim())];

    private static IElement PickerBox(IRenderedComponent<VariableSearch> cut, string label) =>
        cut.FindAll(".dropdown-choicepicker__item")
            .Single(i => i.QuerySelector(".form-control__label")!.TextContent.Trim() == label)
            .QuerySelector("input[type=checkbox]")!;

    private static void PressColumn(IRenderedComponent<VariableSearch> cut, string label)
    {
        var box = PickerBox(cut, label);
        box.Change(!box.HasAttribute("checked"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Header_WhateverTheSignIn_ThenThereIsNoSaveColumnInTheHeaderThePickerOrTheRow(bool signedIn)
    {
        var cut = RenderSignedIn(new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER"))), signedIn);

        Assert.StartsWith("Navn", HeaderNames(cut)[0], StringComparison.Ordinal);
        Assert.DoesNotContain("Variabelliste", HeaderNames(cut));
        Assert.DoesNotContain("Variabelliste", PickerNames(cut));
        Assert.Contains("Kilde", PickerNames(cut));
        Assert.Empty(cut.FindAll(".munin-explorer-dataitem-header__save"));
        Assert.Empty(cut.FindAll(".munin-explorer-dataitem-main__save"));
        Assert.Equal("munin-explorer-dataitem-main__name", cut.Find(".munin-explorer-dataitem-main").Children[0].ClassName);
    }

    [Fact]
    public void Picker_WhenOneDataColumnIsLeftForASignedInReader_ThenItIsLocked()
    {
        // The lock exists so a row never shows nothing but a name; the save column that once sat
        // outside it is gone, so the rule is the same signed in and out.
        var cut = RenderSignedIn(new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER"))));

        foreach (var column in new[] { "Kilde", "Datasamling", "Variabelgruppe", "Datatype" })
        {
            PressColumn(cut, column);
        }

        Assert.Equal("true", PickerBox(cut, "Dataperiode").GetAttribute("aria-disabled"));
    }

    [Fact]
    public void SaveButton_WhenDrawnInEitherState_ThenItWearsThePanelsOwnButtonShape()
    {
        // The same ghost square button as Vis hele variabelen beside it, so the two read as one
        // row of actions rather than a primary one and a secondary one.
        var cut = RenderSignedIn(new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER"))));

        var neighbour = SaveButton(cut).PreviousElementSibling!.ClassName;

        Assert.Equal(neighbour, SaveButton(cut).ClassName);

        SaveButton(cut).Click();

        Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed"));
        Assert.Equal(neighbour, SaveButton(cut).ClassName);
    }

    [Fact]
    public void Row_WhenTheButtonIsDrawnInBothStates_ThenEveryClassNameHasARuleInTheHostStylesheet()
    {
        // The package ships no CSS, so a name with no rule behind it renders unstyled in the host.
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")));
        var cut = RenderSignedIn(client);
        Open(cut);

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));

        SaveButton(cut).Click();

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }
}
