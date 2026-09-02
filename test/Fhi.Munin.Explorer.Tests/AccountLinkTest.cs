using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The account-link entry in the header actions: who sees it, that nothing is redeemed until the
/// reader confirms, and that each refusal the API distinguishes reaches them as its own sentence.
/// </summary>
public class AccountLinkTest : BunitContext
{
    private static Page<VariableSummary> OnePage() =>
        new()
        {
            Items =
            [
                new VariableSummary
                {
                    Id = Guid.NewGuid(),
                    Code = "V_BDR.ALDER",
                    PreferredTerm = "Alder ved diagnose",
                    KildeName = "Als registeret"
                }
            ],
            TotalCount = 1,
            PageNumber = 1,
            Size = 25,
            TotalPages = 1
        };

    private sealed class LinkClient : EmptyMuninExplorerClient
    {
        private readonly Page<VariableSummary> _page = OnePage();

        public int RedeemCalls { get; private set; }
        public int SearchCalls { get; private set; }
        public string? LastCode { get; private set; }

        /// <summary>What the API answers a redemption with, when it answers at all.</summary>
        public IdentityLinkOutcome Outcome { get; init; } = IdentityLinkOutcome.Linked;

        /// <summary>Refuse the redemption with the API's 429.</summary>
        public bool Throttle { get; init; }

        /// <summary>
        /// Fail the redemption the way anything unplanned fails. Its own switch beside
        /// <see cref="Throttle"/> so the pair can be asserted against each other: the panel has to
        /// say something different for each, and one flag could not show that.
        /// </summary>
        public bool Throw { get; init; }

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search,
            VariableFilter? filter = null,
            int page = 1,
            int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            SearchCalls++;
            return Task.FromResult(_page);
        }

        public override Task<IdentityLinkOutcome> RedeemIdentityLinkAsync(
            string? code,
            CancellationToken cancellationToken = default)
        {
            RedeemCalls++;
            LastCode = code;

            if (Throttle)
            {
                throw new MuninExplorerRateLimitedException(null);
            }

            if (Throw)
            {
                throw new InvalidOperationException("the call never arrived");
            }

            return Task.FromResult(Outcome);
        }
    }

    private IRenderedComponent<VariableExplorer> RenderSignedIn(LinkClient client, bool signedIn = true)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        return Render<VariableExplorer>(p => p.Add(c => c.IsAuthenticated, signedIn));
    }

    private static IElement Panel(IRenderedComponent<VariableExplorer> cut) =>
        cut.Find(".munin-explorer-account-link");

    private static IElement Trigger(IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll("summary").Single(s => s.TextContent.Contains("Koble konto"));

    /// <summary>Types a code and presses Fortsett, which is the whole of the first step.</summary>
    private static void EnterCode(IRenderedComponent<VariableExplorer> cut, string code)
    {
        cut.Find(".munin-explorer-account-link input").Change(code);
        cut.FindAll(".munin-explorer-account-link__actions button")
            .Single(b => b.TextContent.Contains("Fortsett"))
            .Click();
    }

    private static void PressConfirm(IRenderedComponent<VariableExplorer> cut) =>
        cut.FindAll(".munin-explorer-account-link__actions button")
            .Single(b => b.TextContent.Contains("Koble kontoene"))
            .Click();

    private static string Alert(IRenderedComponent<VariableExplorer> cut) =>
        cut.Find(".munin-explorer-account-link p[role='alert']").TextContent.Trim();

    // -----------------------------------------------------------------------

    /// <summary>
    /// Not a disabled control: redeeming is an authenticated write, so signed out there is nothing
    /// it could ever do, and the header row is the host's page furniture rather than ours to fill.
    /// </summary>
    [Fact]
    public void Panel_WhenTheReaderIsSignedOut_ThenThereIsNoEntryAtAll()
    {
        var client = new LinkClient();

        var cut = RenderSignedIn(client, signedIn: false);

        Assert.Empty(cut.FindAll(".munin-explorer-account-link"));
        Assert.DoesNotContain("Koble konto", cut.Markup);
        Assert.Equal(0, client.RedeemCalls);
    }

    [Fact]
    public void Panel_WhenTheReaderIsSignedIn_ThenTheEntryIsInTheHeaderActions()
    {
        var cut = RenderSignedIn(new LinkClient());

        Assert.Contains("Koble konto", Trigger(cut).TextContent);
        Assert.NotNull(Panel(cut).Closest(".munin-explorer-header__actions"));
    }

    /// <summary>
    /// The point of the two steps. Linking two accounts is not undoable from here, so the code
    /// must not be spent by the press that finishes typing it.
    /// </summary>
    [Fact]
    public void Panel_WhenTheReaderPressesContinue_ThenNothingIsRedeemedYet()
    {
        var client = new LinkClient();
        var cut = RenderSignedIn(client);

        EnterCode(cut, "ABC123");

        Assert.Equal(0, client.RedeemCalls);
        Assert.Contains("Vil du koble", Panel(cut).TextContent);
    }

    /// <summary>
    /// What the confirmation is allowed to claim. It says what linking does, and deliberately
    /// names neither account: no endpoint previews a code, and the component is told only that
    /// somebody is signed in, never who (Fhi.Metadata-bl448).
    /// </summary>
    [Fact]
    public void Panel_WhenItAsksForConfirmation_ThenItSaysWhatLinkingDoesToTheLists()
    {
        var cut = RenderSignedIn(new LinkClient());

        EnterCode(cut, "ABC123");

        Assert.Contains("synlige begge steder", Panel(cut).TextContent);
    }

    [Fact]
    public void Panel_WhenTheReaderConfirms_ThenTheCodeTheyTypedIsWhatIsRedeemed()
    {
        var client = new LinkClient();
        var cut = RenderSignedIn(client);

        EnterCode(cut, "ABC123");
        PressConfirm(cut);

        Assert.Equal(1, client.RedeemCalls);
        Assert.Equal("ABC123", client.LastCode);
    }

    [Fact]
    public void Panel_WhenTheReaderCancels_ThenNothingIsRedeemedAndTheCodeIsStillThere()
    {
        var client = new LinkClient();
        var cut = RenderSignedIn(client);

        EnterCode(cut, "ABC123");
        cut.FindAll(".munin-explorer-account-link__actions button")
            .Single(b => b.TextContent.Contains("Avbryt"))
            .Click();

        Assert.Equal(0, client.RedeemCalls);
        Assert.Equal("ABC123", cut.Find(".munin-explorer-account-link input").GetAttribute("value"));
    }

    [Fact]
    public void Panel_WhenTheLinkSucceeds_ThenItSaysSoAndStopsOfferingTheCodeField()
    {
        var cut = RenderSignedIn(new LinkClient { Outcome = IdentityLinkOutcome.Linked });

        EnterCode(cut, "ABC123");
        PressConfirm(cut);

        Assert.Contains("Kontoene er koblet", Alert(cut));
        Assert.Empty(cut.FindAll(".munin-explorer-account-link input"));
    }

    /// <summary>
    /// The distinctions the API draws, reaching the reader intact. Each refusal sends them
    /// somewhere different, and a shared "noe gikk galt" would leave them retrying a code that can
    /// never work again — which is the whole reason the endpoint spells them out separately.
    /// </summary>
    [Theory]
    [InlineData(IdentityLinkOutcome.InvalidCode, "Koden stemmer ikke")]
    [InlineData(IdentityLinkOutcome.ExpiredCode, "Koden er utløpt")]
    [InlineData(IdentityLinkOutcome.CodeAlreadyUsed, "allerede brukt")]
    [InlineData(IdentityLinkOutcome.CannotLinkToSelf, "allerede er logget inn med her")]
    [InlineData(IdentityLinkOutcome.BothIdentitiesAlreadyLinked, "hver sin person")]
    public void Panel_WhenTheApiRefuses_ThenTheReaderIsToldWhichRefusalItWas(
        IdentityLinkOutcome outcome,
        string expected)
    {
        var cut = RenderSignedIn(new LinkClient { Outcome = outcome });

        EnterCode(cut, "ABC123");
        PressConfirm(cut);

        Assert.Contains(expected, Alert(cut));
    }

    /// <summary>
    /// A refusal puts the reader back at the field with what they typed still in it, so a mistyped
    /// character is one edit away rather than a whole code retyped.
    /// </summary>
    [Fact]
    public void Panel_WhenTheApiRefuses_ThenTheCodeFieldComesBackWithTheCodeStillInIt()
    {
        var cut = RenderSignedIn(new LinkClient { Outcome = IdentityLinkOutcome.InvalidCode });

        EnterCode(cut, "ABC123");
        PressConfirm(cut);

        Assert.Equal("ABC123", cut.Find(".munin-explorer-account-link input").GetAttribute("value"));
    }

    /// <summary>
    /// Throttling is not an answer about the code. "Sjekk koden" here would send the reader to
    /// mint a new one for a code that was never the problem.
    /// </summary>
    [Fact]
    public void Panel_WhenTheApiThrottles_ThenItSaysSoRatherThanBlamingTheCode()
    {
        var cut = RenderSignedIn(new LinkClient { Throttle = true });

        EnterCode(cut, "ABC123");
        PressConfirm(cut);

        Assert.Contains("for mange forespørsler", Alert(cut));
        Assert.DoesNotContain("Koden stemmer ikke", Alert(cut));
    }

    /// <summary>
    /// The call never arriving, told apart from the API refusing. An unhandled exception out of an
    /// EventCallback would take the whole circuit down with it.
    /// </summary>
    [Fact]
    public void Panel_WhenTheCallFails_ThenItSaysSoRatherThanBlamingTheCode()
    {
        var cut = RenderSignedIn(new LinkClient { Throw = true });

        EnterCode(cut, "ABC123");
        PressConfirm(cut);

        Assert.Contains("Kunne ikke koble kontoene", Alert(cut));
        Assert.DoesNotContain("Koden stemmer ikke", Alert(cut));
    }

    /// <summary>
    /// A client whose redemption never finishes on its own, so the in-flight render can be looked
    /// at. Every other fake here answers synchronously, and a synchronous answer never yields —
    /// so the working state would be invisible to a test and dead code nobody noticed.
    /// </summary>
    private sealed class StallingLinkClient : EmptyMuninExplorerClient
    {
        private readonly TaskCompletionSource<IdentityLinkOutcome> _redeem =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search,
            VariableFilter? filter = null,
            int page = 1,
            int pageSize = 25,
            SortField sort = SortField.Default,
            SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(OnePage());

        public int RedeemCalls { get; private set; }

        public override Task<IdentityLinkOutcome> RedeemIdentityLinkAsync(
            string? code,
            CancellationToken cancellationToken = default)
        {
            RedeemCalls++;
            return _redeem.Task;
        }

        public void Finish() => _redeem.SetResult(IdentityLinkOutcome.Linked);
    }

    private IRenderedComponent<VariableExplorer> RenderStalling(StallingLinkClient client)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddScoped<VariableListState>();
        return Render<VariableExplorer>(p => p.Add(c => c.IsAuthenticated, true));
    }

    /// <summary>
    /// While the redemption is in flight the reader is told so, and both buttons are still in the
    /// document. Removing the one they just pressed would drop focus to <c>&lt;body&gt;</c>, which
    /// is the failure <c>aria-disabled</c> is used for here as it is on the pager.
    /// </summary>
    [Fact]
    public void Panel_WhileTheRedemptionIsInFlight_ThenItSaysSoAndTheButtonsStayPutButInert()
    {
        var client = new StallingLinkClient();
        var cut = RenderStalling(client);

        EnterCode(cut, "ABC123");
        PressConfirm(cut);

        Assert.Contains("Kobler kontoene", Panel(cut).TextContent);

        var buttons = cut.FindAll(".munin-explorer-account-link__actions button");
        Assert.Equal(2, buttons.Count);
        Assert.All(buttons, b => Assert.Equal("true", b.GetAttribute("aria-disabled")));

        client.Finish();
        cut.WaitForAssertion(() => Assert.Contains("Kontoene er koblet", Alert(cut)));
    }

    /// <summary>
    /// The second press of a one-shot credential. Letting it through would redeem the same code
    /// twice, and the API's <c>code_already_used</c> would tell the reader their own successful
    /// link had failed.
    /// </summary>
    [Fact]
    public void Panel_WhenConfirmIsPressedAgainWhileInFlight_ThenTheCodeIsOnlyRedeemedOnce()
    {
        var client = new StallingLinkClient();
        var cut = RenderStalling(client);

        EnterCode(cut, "ABC123");
        PressConfirm(cut);
        PressConfirm(cut);

        Assert.Equal(1, client.RedeemCalls);

        client.Finish();
        cut.WaitForAssertion(() => Assert.Contains("Kontoene er koblet", Alert(cut)));
    }

    /// <summary>
    /// Cancel is inert once the code is on its way: the credential is already spent, so going back
    /// to the field would offer a second press of something that can never work again.
    /// </summary>
    [Fact]
    public void Panel_WhenCancelIsPressedWhileInFlight_ThenItDoesNotReturnToTheCodeField()
    {
        var client = new StallingLinkClient();
        var cut = RenderStalling(client);

        EnterCode(cut, "ABC123");
        PressConfirm(cut);
        cut.FindAll(".munin-explorer-account-link__actions button")
            .Single(b => b.TextContent.Contains("Avbryt"))
            .Click();

        Assert.Empty(cut.FindAll(".munin-explorer-account-link input"));
        Assert.Contains("Kobler kontoene", Panel(cut).TextContent);

        client.Finish();
        cut.WaitForAssertion(() => Assert.Contains("Kontoene er koblet", Alert(cut)));
    }

    /// <summary>
    /// The component only ever <em>receives</em> a link. It runs inside a CMS page that is not
    /// ours, so an entry that unfolded itself — on mount, or because the reader searched — would
    /// be our panel opening over somebody else's page (Fhi.Metadata-bl448).
    /// </summary>
    [Fact]
    public void Panel_WhenTheReaderHasNotAskedForIt_ThenItStaysFoldedAndNothingIsRedeemed()
    {
        var client = new LinkClient();
        var cut = RenderSignedIn(client);

        // Every other test here reaches straight into the panel, which bUnit finds whether or not
        // the <details> around it is open — so an entry that unfolded itself would pass them all.
        Assert.False(Panel(cut).Closest("details")!.HasAttribute("open"));

        // Scoped to the search box on purpose: the code field wears searchbox__freetext too.
        cut.Find(".munin-explorer-search .searchbox__freetext").Change("alder");
        cut.Find("form").Submit();

        // Typing alone changes nothing; the search that results have arrived from is the moment
        // an entry that unfolded itself would do so.
        Assert.Equal(2, client.SearchCalls); // initial load + this one
        Assert.False(Panel(cut).Closest("details")!.HasAttribute("open"));
        Assert.Equal(0, client.RedeemCalls);
    }

    /// <summary>
    /// The false→true crossing, where the entry first appears — the place a later change would
    /// most naturally open it to draw attention to it. The sibling above already covers a crossing
    /// hooked where the panel is drawn; what only this one reaches is a crossing hooked somewhere a
    /// first signed-in render never goes, such as an <c>OnAfterRenderAsync</c> guarded by
    /// <c>!firstRender</c> (Fhi.Metadata-bl448).
    /// </summary>
    [Fact]
    public void Panel_WhenTheReaderSignsInWhileTheExplorerIsOnScreen_ThenTheEntryAppearsFolded()
    {
        var client = new LinkClient();
        var cut = RenderSignedIn(client, signedIn: false);

        cut.Render(p => p.Add(c => c.IsAuthenticated, true));

        Assert.False(Panel(cut).Closest("details")!.HasAttribute("open"));
        Assert.Equal(0, client.RedeemCalls);
    }

    /// <summary>
    /// A circuit outlives a sign-out, and the panel's stage and code field are plain component
    /// fields. Carried across, the reader who signs in next is told somebody else's link succeeded
    /// — in an assertive alert, with no code field left to redeem their own (Fhi.Metadata-bl448).
    /// </summary>
    [Fact]
    public void Panel_WhenTheReaderSignsOutAfterLinkingAndBackIn_ThenItStartsOverForTheNextReader()
    {
        var client = new LinkClient();
        var cut = RenderSignedIn(client);

        EnterCode(cut, "ABC123");
        PressConfirm(cut);
        Assert.Contains("Kontoene er koblet", Alert(cut));

        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        cut.Render(p => p.Add(c => c.IsAuthenticated, true));

        Assert.Equal("", Alert(cut));
        Assert.Equal("", cut.Find(".munin-explorer-account-link input").GetAttribute("value"));
    }

    /// <summary>
    /// The same crossing with a code typed but never spent. RedeemAsync clears a spent code so it
    /// is not left in the DOM for the rest of the circuit; an unspent one belongs to the reader who
    /// typed it just as much (Fhi.Metadata-bl448).
    /// </summary>
    [Fact]
    public void Panel_WhenTheReaderSignsOutWithACodeTypedAndBackIn_ThenTheFieldIsEmpty()
    {
        var client = new LinkClient();
        var cut = RenderSignedIn(client);

        cut.Find(".munin-explorer-account-link input").Change("ABC123");

        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        cut.Render(p => p.Add(c => c.IsAuthenticated, true));

        Assert.Equal("", cut.Find(".munin-explorer-account-link input").GetAttribute("value"));
        Assert.Equal(0, client.RedeemCalls);
    }

    /// <summary>
    /// The redemption that is still in flight when the reader leaves. Its answer is theirs, so
    /// writing it into the panel would announce it to whoever signs in next — the same reason
    /// <see cref="VariableListState"/> bumps a generation on the crossing (Fhi.Metadata-bl448).
    /// </summary>
    [Fact]
    public async Task Panel_WhenTheReaderLeavesMidRedemption_ThenTheNextReaderNeverSeesTheAnswer()
    {
        var client = new StallingLinkClient();
        var cut = RenderStalling(client);

        EnterCode(cut, "ABC123");
        PressConfirm(cut);

        // Back before the answer arrives, so it lands on a panel that is already the next
        // reader's. Resetting on the crossing is not enough on its own for this order.
        cut.Render(p => p.Add(c => c.IsAuthenticated, false));
        cut.Render(p => p.Add(c => c.IsAuthenticated, true));
        client.Finish();

        // Queued behind the redemption's own continuation on the renderer's dispatcher, so the
        // assertions run after the answer has arrived rather than racing it.
        await cut.InvokeAsync(() => { });

        Assert.Equal("", Alert(cut));
        Assert.Equal("", cut.Find(".munin-explorer-account-link input").GetAttribute("value"));
    }

    /// <summary>
    /// The alert element is in the DOM from the first render. One inserted and filled in the same
    /// update is announced unreliably; one already there and gaining text is announced.
    /// </summary>
    [Fact]
    public void Panel_BeforeAnythingHasHappened_ThenTheAlertRegionIsAlreadyThereAndEmpty()
    {
        var cut = RenderSignedIn(new LinkClient());

        Assert.Equal("", Alert(cut));
    }

    /// <summary>
    /// The field is a control a screen reader has to be able to name, and a placeholder is not a
    /// name — <see cref="AccessibleName"/> refuses to count one.
    /// </summary>
    [Fact]
    public void Panel_TheCodeField_ThenItHasAnAccessibleName()
    {
        var cut = RenderSignedIn(new LinkClient());

        var field = cut.Find(".munin-explorer-account-link input");

        Assert.Equal("Koblingskode", AccessibleName.Of(field));
    }

    /// <summary>
    /// English is a supported reader language, so a host rendering the component in it must not
    /// get a panel speaking Norwegian.
    /// </summary>
    [Fact]
    public void Panel_WhenTheReaderLanguageIsEnglish_ThenTheEntryIsInEnglishToo()
    {
        Services.AddSingleton<IMuninExplorerClient>(new LinkClient());
        Services.AddScoped<VariableListState>();

        var cut = Render<VariableExplorer>(p => p
            .Add(c => c.IsAuthenticated, true)
            .Add(c => c.Language, "en"));

        Assert.Contains(
            "Link account",
            cut.FindAll("summary").Single(s => s.TextContent.Contains("Link account")).TextContent);
    }
}
