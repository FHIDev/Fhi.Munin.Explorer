using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The row's save action. The state it shows belongs to the circuit, not to the row: results are
/// rebuilt whenever the facet counts change, so a button holding its own answer shows the wrong
/// word for a variable that is in the list.
/// </summary>
public class SaveToListTest : BunitContext
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
            Guid id, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default)
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

    private IRenderedComponent<VariableExplorer> RenderSignedIn(ListClient client, bool signedIn = true)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        return Render<VariableExplorer>(p => p.Add(c => c.IsAuthenticated, signedIn));
    }

    private static IElement SaveButton(IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll(".munin-explorer-dataitem-main button[aria-pressed]")[0];

    // -----------------------------------------------------------------------

    [Fact]
    public void Row_WhenTheReaderIsSignedOut_ThenThereIsNoSaveButtonAndNoListCall()
    {
        // Not a disabled button: a control that can never do anything is worse than no control.
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")));

        var cut = RenderSignedIn(client, signedIn: false);

        Assert.Empty(cut.FindAll(".munin-explorer-dataitem-main button[aria-pressed]"));
        Assert.Equal(0, client.MyListsCalls);
        Assert.Equal(0, client.AddCalls);
    }

    [Fact]
    public void Row_WhenTheReaderIsSignedIn_ThenEveryRowOffersToSave()
    {
        var client = new ListClient(OnePage(
            Variable("Alder ved diagnose", "V_BDR.ALDER"),
            Variable("Skjemastatus", "V_BDR.FORMSTATUS")));

        var cut = RenderSignedIn(client);

        Assert.Equal(2, cut.FindAll(".munin-explorer-dataitem-main button[aria-pressed]").Count);
        Assert.Equal("false", SaveButton(cut).GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Row_WhenEveryRowOffersToSave_ThenEachButtonNamesItsOwnVariable()
    {
        // Two rows, because the weak version of this assertion — "the button has an accessible
        // name" — is satisfied by a constant label on all 25 of them, which is the same "Lagre i
        // liste, Lagre i liste, Lagre i liste" a screen reader hears from the visible words alone.
        // Distinctness is what makes the assertion mean anything.
        var client = new ListClient(OnePage(
            Variable("Alder ved diagnose", "V_BDR.ALDER"),
            Variable("Skjemastatus", "V_BDR.FORMSTATUS")));

        var cut = RenderSignedIn(client);

        var names = cut.FindAll(".munin-explorer-dataitem-main button[aria-pressed]")
            .Select(AccessibleName.Of)
            .ToList();

        Assert.Equal(2, names.Count);
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
        // The reason the name is two elements rather than one aria-label, and the rule this
        // package already wrote down for the toggle in this same row: "Save to list" is ours and
        // follows Language, "Alder ved diagnose" is Munin's and is Norwegian whatever the
        // surrounding UI is. One aria-label string would hand the whole sentence to an English
        // voice, which pronounces the Norwegian half with English phonetics. WCAG 3.1.2.
        Services.AddSingleton<IMuninExplorerClient>(
            new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER"))));
        Services.AddScoped<VariableListState>();

        var cut = Render<VariableExplorer>(p => p
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
    public void Row_WhenAVariableHasNoPreferredTerm_ThenItsButtonStillAnnouncesWhatItDoes()
    {
        // PreferredTerm defaults to "" and the row already renders it blank, so this is a shape
        // the page can reach. Naming the button by pointing at that empty span rather than by
        // interpolating the term into a sentence is what keeps it safe: the empty half
        // contributes nothing and the button falls back to its own words, where "Lagre i liste: "
        // would announce with a hole on the end.
        var client = new ListClient(OnePage(Variable("", "V_BDR.ALDER")));
        var cut = RenderSignedIn(client);

        Assert.Equal("Lagre i liste", AccessibleName.Of(SaveButton(cut)));
    }

    [Fact]
    public void Row_WhenAVariableIsSaved_ThenItsButtonStillNamesItInTheOtherState()
    {
        // One control in two states, and the accessible name has to follow the word the same way
        // aria-pressed does. A name computed once for the unsaved state would tell a screen reader
        // user the button saves a variable that is already in the list.
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")));
        var cut = RenderSignedIn(client);

        SaveButton(cut).Click();

        var name = AccessibleName.Of(SaveButton(cut));

        Assert.Equal("Fjern fra liste Alder ved diagnose", name);
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
        // saved yesterday offers «Lagre i liste» and the press takes it out — the label and the
        // action disagreeing about the same variable, on the very first render.
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
    public void Row_WhenNothingHasFailed_ThenTheAlertContainerIsAlreadyInTheDom()
    {
        // A role="alert" inserted and filled in the same update is announced unreliably. The
        // container is here from the start, empty — the shape the component's own alert region uses.
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")));

        var cut = RenderSignedIn(client);

        var alert = cut.FindAll(".munin-explorer-dataitem-main [role=alert]");
        Assert.Single(alert);
        Assert.Equal("", alert[0].TextContent.Trim());
    }

    [Fact]
    public void Row_WhenTheSaveIsRateLimited_ThenTheRowSaysSoRatherThanThatSavingFailed()
    {
        // The writes go through the same client and the same per-address limiter as the reads, and
        // saving one row after another is the rhythm that meets it. "Prøv igjen om litt" beside the
        // button would be advising the reader to do the one thing that keeps the window full.
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")))
        {
            RateLimitAdd = true
        };
        var cut = RenderSignedIn(client);

        SaveButton(cut).Click();

        var alert = cut.Find(".munin-explorer-dataitem-main [role=alert]");

        Assert.Contains("for mange forespørsler", alert.TextContent);
        Assert.DoesNotContain("Kunne ikke lagre", alert.TextContent);
    }

    [Fact]
    public void Row_WhenTheSaveFailsForAnyOtherReason_ThenTheRowStillSaysTheSaveFailed()
    {
        // The other half of the pair: the throttled sentence must not swallow the ordinary one, or
        // a reader whose save really did fail is told to wait for a window that was never the
        // problem.
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")))
        {
            FailAdd = true
        };
        var cut = RenderSignedIn(client);

        SaveButton(cut).Click();

        var alert = cut.Find(".munin-explorer-dataitem-main [role=alert]");

        Assert.Contains("Kunne ikke lagre", alert.TextContent);
        Assert.DoesNotContain("for mange forespørsler", alert.TextContent);
    }

    [Fact]
    public void Mount_WhenTheListBootstrapIsRateLimited_ThenThePageStillRenders()
    {
        // The mount fires the search, the facet refresh and this list read together, which is
        // exactly the burst the per-address limiter counts — so a 429 here is ordinary. Left
        // uncaught it leaves OnParametersSetAsync as an unhandled exception, and in helsedata's
        // legacy Blazor Server host that tears down the circuit for the whole CMS page rather than
        // showing the sentence this component has for it.
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")))
        {
            RateLimitMembership = true
        };

        var cut = RenderSignedIn(client);

        Assert.Equal(1, client.MembershipCalls);
        Assert.Single(cut.FindAll("ul.munin-explorer-data-list > li"));
        Assert.Single(cut.FindAll(".munin-explorer-dataitem-main button[aria-pressed]"));

        // Nothing is claimed about the list either way: the reader has touched nothing yet, so
        // there is nothing for a page-wide alert to tell them to do.
        Assert.Empty(cut.Find("[role='alert']").TextContent.Trim());
    }

    [Fact]
    public void Mount_WhenTheListBootstrapWasRateLimited_ThenTheNextSavePutsTheOtherRowsRight()
    {
        // Why "wait and try again" is honest advice rather than a sentence that repairs nothing.
        // The refused read leaves membership empty, so a variable saved yesterday renders unsaved;
        // the next press reads it again, and every row's label agrees with the stored list once
        // more.
        var alder = Variable("Alder ved diagnose", "V_BDR.ALDER");
        var status = Variable("Skjemastatus", "V_BDR.FORMSTATUS");
        var client = new ListClient(OnePage(alder, status)) { RateLimitMembership = true };

        client.Stored.Add(status.Id);

        var cut = RenderSignedIn(client);

        // Wrong, and knowably so: the read that would have said otherwise was refused.
        Assert.Equal(["false", "false"],
                     cut.FindAll(".munin-explorer-dataitem-main button[aria-pressed]")
                        .Select(b => b.GetAttribute("aria-pressed")));

        client.RateLimitMembership = false;
        SaveButton(cut).Click();

        Assert.Equal(["true", "true"],
                     cut.FindAll(".munin-explorer-dataitem-main button[aria-pressed]")
                        .Select(b => b.GetAttribute("aria-pressed")));
        Assert.Equal(2, client.Stored.Count);
        Assert.Contains(alder.Id, client.Stored);
        Assert.Contains(status.Id, client.Stored);
    }

    [Fact]
    public void Mount_WhenTheFirstPressAfterARefusedBootstrapIsOnASavedRow_ThenItIsNotRemoved()
    {
        // The other half of the repair, and the one that costs the reader something if it is wrong.
        // The refused read draws every row as unsaved, so a variable saved yesterday shows "Lagre";
        // the press reads membership again, which fills the set in the middle of the call. A press
        // that then asked the freshly filled set which way to go would find the variable saved and
        // delete it — the button doing the opposite of the word on it, and saying nothing about it
        // afterwards. The direction comes from the row as it was drawn instead.
        var status = Variable("Skjemastatus", "V_BDR.FORMSTATUS");
        var client = new ListClient(OnePage(status)) { RateLimitMembership = true };

        client.Stored.Add(status.Id);

        var cut = RenderSignedIn(client);

        Assert.Equal("false", SaveButton(cut).GetAttribute("aria-pressed"));
        Assert.Contains("Lagre", SaveButton(cut).TextContent);

        client.RateLimitMembership = false;
        SaveButton(cut).Click();

        // Still the reader's, and now labelled as such.
        Assert.Contains(status.Id, client.Stored);
        Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed"));
        Assert.Equal(0, client.RemoveCalls);

        // Nothing was said, because nothing went wrong.
        Assert.Empty(cut.Find("[role='alert']").TextContent.Trim());
    }

    // -----------------------------------------------------------------------
    // The trap.
    // -----------------------------------------------------------------------

    [Fact]
    public void Row_WhenTheResultsAreRebuilt_ThenTheSavedStateSurvives()
    {
        // The rows are redrawn whenever the facet counts change. A button that remembered "saved"
        // itself would forget it here and show "Lagre i liste" for a variable that IS in the list.
        // Asserted after a re-render, not after one click.
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")));
        var cut = RenderSignedIn(client);

        SaveButton(cut).Click();
        Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed"));

        // A real refetch, not just a re-render: the rows come back as new objects with the same
        // ids, which is what a refiltering does. A row that had stashed "saved" on the summary it
        // was handed would lose it exactly here.
        var before = client.SearchCalls;
        cut.Find("button[type=submit]").Click();
        Assert.True(client.SearchCalls > before, "the search did not refetch, so nothing was rebuilt");

        Assert.Equal("true", SaveButton(cut).GetAttribute("aria-pressed"));
        Assert.Equal(1, client.AddCalls);
    }

    [Fact]
    public void Row_WhenTheButtonIsDrawnInBothStates_ThenEveryClassNameHasARuleInTheHostStylesheet()
    {
        // The package ships no CSS, so a name with no rule behind it renders unstyled in the host.
        var client = new ListClient(OnePage(Variable("Alder ved diagnose", "V_BDR.ALDER")));
        var cut = RenderSignedIn(client);

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));

        SaveButton(cut).Click();

        Assert.Equal([], HostClassNames.Orphans(HostClassNames.Of(cut.FindAll("[class]"))));
    }
}
