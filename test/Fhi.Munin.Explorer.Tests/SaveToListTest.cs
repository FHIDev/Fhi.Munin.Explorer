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
            Guid id, int page = 1, int pageSize = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [.. Stored.Select(v => new VariableListItem { VariableId = v })],
                TotalCount = Stored.Count,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1
            });

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
