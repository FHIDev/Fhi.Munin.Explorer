using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Sharing a saved list as a code and opening one somebody shared, from a <c>?delekode=</c> link or
/// the field in the list view (Fhi.Metadata-ntpbd.1). A shared list is read-only, signed out too.
/// </summary>
public class SharedListViewTest : ExplorerTestContext
{
    private const string ReplaceState = "history.replaceState";

    private static readonly Guid OwnListId = new("11111111-1111-1111-1111-111111111111");

    private static VariableListItem Item(string name, string code) => new()
    {
        VariableId = Guid.NewGuid(),
        VariableName = name,
        VariableCode = code,
        KildeName = "Als registeret",
        KildeShortName = "ALS",
        DatasamlingName = "Inklusjon",
        DataType = "2",
    };

    private static readonly VariableListItem[] Three =
    [
        Item("Alder ved diagnose", "ALDER"),
        Item("Kjønn", "KJONN"),
        Item("Bostedskommune", "KOMMUNE"),
    ];

    /// <summary>What the share endpoint stores, shared by every client that talks to it.</summary>
    private sealed class ShareStore
    {
        public Dictionary<string, SharedList> ByCode { get; } = [];
    }

    /// <summary>
    /// The reader's own lists and the share endpoints, with every my/lists call counted apart
    /// from the anonymous share ones.
    /// </summary>
    private sealed class ShareClient(ShareStore store) : EmptyMuninExplorerClient
    {
        private readonly Dictionary<Guid, List<VariableListItem>> _items = [];
        private readonly List<VariableList> _lists = [];

        public string OwnListName { get; init; } = "Mine hjertevariabler";

        public IReadOnlyList<VariableListItem> OwnItems
        {
            init
            {
                _lists.Add(new VariableList { Id = OwnListId, Name = OwnListName, VariableCount = value.Count });
                _items[OwnListId] = [.. value];
            }
        }

        /// <summary>An exception the share read throws, standing in for a 500 or a lost API.</summary>
        public Exception? SharedThrows { get; init; }

        public Exception? ShareThrows { get; init; }

        /// <summary>The first read of my/lists after a list is created throws, as a lost API would.</summary>
        public bool ReadAfterCreateThrows { get; init; }

        /// <summary>Every add answers false, as the API does for a list it no longer has.</summary>
        public bool AddsRefused { get; init; }

        public int MyListsCalls { get; private set; }
        public int CreateCalls { get; private set; }
        public int ShareCalls { get; private set; }
        public int SharedReads { get; private set; }
        public List<Guid> Added { get; } = [];

        private bool _readAfterCreateThrown;

        public override Task<string> ShareListAsync(
            string name, IReadOnlyCollection<VariableListItem> items, CancellationToken cancellationToken = default)
        {
            ShareCalls++;

            if (ShareThrows is not null)
            {
                throw ShareThrows;
            }

            var code = $"SHARE{store.ByCode.Count}";
            store.ByCode[code] = new SharedList(name, [.. items]);

            return Task.FromResult(code);
        }

        /// <summary>Reads left in flight, by code, so a test can finish them out of order.</summary>
        public Dictionary<string, TaskCompletionSource<SharedList?>> Hanging { get; } = [];

        public override Task<SharedList?> GetSharedListAsync(string? code, CancellationToken cancellationToken = default)
        {
            SharedReads++;

            if (code is not null && Hanging.TryGetValue(code, out var hanging))
            {
                return hanging.Task;
            }

            if (SharedThrows is not null)
            {
                throw SharedThrows;
            }

            return Task.FromResult(
                SharedList.NormalizeCode(code) is { } key && store.ByCode.TryGetValue(key, out var list) ? list : null);
        }

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default)
        {
            MyListsCalls++;

            if (ReadAfterCreateThrows && CreateCalls > 0 && !_readAfterCreateThrown)
            {
                _readAfterCreateThrown = true;
                throw new HttpRequestException("500");
            }

            // Counted on every read, as the API counts: a list answers with what it holds now.
            return Task.FromResult<IReadOnlyList<VariableList>>(
                [.. _lists.Select(l => l with { VariableCount = _items[l.Id].Count })]);
        }

        public override Task<VariableList> CreateMyListAsync(string name, CancellationToken cancellationToken = default)
        {
            MyListsCalls++;
            CreateCalls++;

            var created = new VariableList { Id = Guid.NewGuid(), Name = name };
            _lists.Add(created);
            _items[created.Id] = [];

            return Task.FromResult(created);
        }

        public override Task<bool> AddVariablesToMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            MyListsCalls++;

            if (AddsRefused)
            {
                return Task.FromResult(false);
            }

            Added.AddRange(variableIds);

            var known = store.ByCode.Values.SelectMany(l => l.Items).ToList();
            _items[id].AddRange(variableIds.Select(v => known.First(k => k.VariableId == v)));

            return Task.FromResult(true);
        }

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default)
        {
            MyListsCalls++;

            if (!_items.TryGetValue(id, out var items))
            {
                return Task.FromResult<Page<VariableListItem>?>(null);
            }

            return Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [.. items.Skip((page - 1) * pageSize).Take(pageSize)],
                TotalCount = items.Count,
                PageNumber = page,
                Size = pageSize,
                TotalPages = Math.Max(1, (items.Count + pageSize - 1) / pageSize),
            });
        }

        public override Task<bool> RemoveVariablesFromMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            MyListsCalls++;
            return Task.FromResult(true);
        }
    }

    private static void Register(BunitContext context, IMuninExplorerClient client)
    {
        context.Services.AddSingleton(client);
        context.Services.AddScoped<VariableListState>();
        context.SetRendererInfo(new RendererInfo("Server", true));
    }

    private IRenderedComponent<VariableListView> RenderView(
        ShareClient client, bool signedIn = true, string language = "no", string? shareCode = null)
    {
        Register(this, client);

        return Render<VariableListView>(p => p
            .Add(c => c.IsAuthenticated, signedIn)
            .Add(c => c.Language, language)
            .Add(c => c.ShareCode, shareCode)
            .Add(c => c.SharedListHref, code => $"https://helsedata.example/variabler?delekode={code}"));
    }

    private static IRenderedComponent<VariableExplorer> RenderExplorer(
        BunitContext context, ShareClient client, string url, bool signedIn)
    {
        Register(context, client);
        context.Services.GetRequiredService<NavigationManager>().NavigateTo(url);

        return context.Render<VariableExplorer>(p => p.Add(c => c.IsAuthenticated, signedIn));
    }

    private static IElement Button<T>(IRenderedComponent<T> cut, string text) where T : IComponent =>
        cut.FindAll("button").Single(b => b.TextContent.Trim() == text);

    private static bool HasButton<T>(IRenderedComponent<T> cut, string text) where T : IComponent =>
        cut.FindAll("button").Any(b => b.TextContent.Trim() == text);

    private static IElement Labelled<T>(IRenderedComponent<T> cut, string label) where T : IComponent
    {
        var target = cut.FindAll("label").Single(l => l.TextContent.Trim() == label).GetAttribute("for");
        return cut.Find($"#{target}");
    }

    private static string Alert<T>(IRenderedComponent<T> cut) where T : IComponent =>
        cut.Find("div[role=alert][aria-live=assertive]").TextContent.Trim();

    private static List<string> RowNames<T>(IRenderedComponent<T> cut) where T : IComponent =>
        [.. cut.FindAll("table.munin-explorer-data-list tbody th[scope=row]").Select(c => c.TextContent.Trim())];

    private static string? Mirrored(BunitContext context) =>
        context.JSInterop.Invocations[ReplaceState] is { Count: > 0 } calls
            ? calls[^1].Arguments[2] as string
            : null;

    // -----------------------------------------------------------------------
    // AC1: a code made in one session opens the same list in another

    [Fact]
    public void Share_WhenACodeMadeInOneSessionIsOpenedInAnother_ThenTheSameVariablesShowInOrder()
    {
        var store = new ShareStore();
        var cut = RenderView(new ShareClient(store) { OwnItems = Three });

        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));
        Button(cut, "Del liste").Click();

        var code = "";
        cut.WaitForAssertion(() =>
        {
            code = Labelled(cut, "Delekode").GetAttribute("value") ?? "";
            Assert.Matches("^[A-Z0-9]{6}$", code);
        });

        // A fresh context is a fresh circuit: its own VariableListState, the same share endpoint.
        using var other = new ExplorerTestContext();
        var opened = RenderExplorer(other, new ShareClient(store), $"/variabler?delekode={code.ToLowerInvariant()}", signedIn: false);

        opened.WaitForAssertion(() =>
            Assert.Equal(["Alder ved diagnose", "Kjønn", "Bostedskommune"], RowNames(opened)));
        Assert.Contains("Mine hjertevariabler", opened.Markup);
    }

    // -----------------------------------------------------------------------
    // AC4: an unknown code, or a failure, is said in the alert region and nothing throws

    [Fact]
    public void Open_WhenALinkNamesAnUnknownCode_ThenTheAlertSaysSoAndTheOwnListStays()
    {
        var cut = RenderView(new ShareClient(new ShareStore()) { OwnItems = Three }, shareCode: "ZZ99ZZ");

        cut.WaitForAssertion(() => Assert.Equal(
            "Fant ingen delt liste med koden ZZ99ZZ. En kode virker i 90 dager.", Alert(cut)));
        Assert.Equal(3, RowNames(cut).Count);
    }

    [Fact]
    public void Open_WhenALinkFailsToLoad_ThenTheAlertSaysTheListCouldNotBeOpened()
    {
        var client = new ShareClient(new ShareStore())
        {
            OwnItems = Three,
            SharedThrows = new HttpRequestException("500"),
        };

        var cut = RenderView(client, shareCode: "AB12CD");

        cut.WaitForAssertion(() => Assert.Equal(
            "Kunne ikke åpne den delte listen nå. Prøv igjen om litt.", Alert(cut)));
        Assert.Equal(3, RowNames(cut).Count);
    }

    [Fact]
    public void Open_WhenALinkNamesAnUnknownCodeSignedOut_ThenTheAlertSaysSoWithAWayToClose()
    {
        var client = new ShareClient(new ShareStore());
        var cut = RenderView(client, signedIn: false, shareCode: "ZZ99ZZ");

        cut.WaitForAssertion(() => Assert.Equal(
            "Fant ingen delt liste med koden ZZ99ZZ. En kode virker i 90 dager.", Alert(cut)));
        Assert.True(HasButton(cut, "Lukk delt liste"));
        Assert.Equal(0, client.MyListsCalls);
    }

    [Fact]
    public void Open_WhenTheFieldNamesAnUnknownCode_ThenTheAlertSaysSoWithTheCode()
    {
        var cut = RenderView(new ShareClient(new ShareStore()) { OwnItems = Three });
        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));

        Button(cut, "Åpne delt liste").Click();
        Labelled(cut, "Delekode for listen du vil åpne").Change("zz99zz");
        Button(cut, "Åpne listen").Click();

        cut.WaitForAssertion(() => Assert.Equal(
            "Fant ingen delt liste med koden ZZ99ZZ. En kode virker i 90 dager.", Alert(cut)));
        Assert.Equal(3, RowNames(cut).Count);
    }

    [Fact]
    public void Open_WhenTheFieldHoldsAMalformedCode_ThenTheAlertSaysSoWithoutAskingTheApi()
    {
        var client = new ShareClient(new ShareStore()) { OwnItems = Three };
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));

        Button(cut, "Åpne delt liste").Click();
        Labelled(cut, "Delekode for listen du vil åpne").Change("abc");
        Button(cut, "Åpne listen").Click();

        cut.WaitForAssertion(() => Assert.Equal(
            "Fant ingen delt liste med koden abc. En kode virker i 90 dager.", Alert(cut)));
        Assert.Equal(0, client.SharedReads);
    }

    [Fact]
    public void Open_WhenTheFieldsCodeFailsToLoad_ThenTheAlertSaysTheListCouldNotBeOpened()
    {
        var client = new ShareClient(new ShareStore())
        {
            OwnItems = Three,
            SharedThrows = new HttpRequestException("500"),
        };
        var cut = RenderView(client);
        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));

        Button(cut, "Åpne delt liste").Click();
        Labelled(cut, "Delekode for listen du vil åpne").Change("AB12CD");
        Button(cut, "Åpne listen").Click();

        cut.WaitForAssertion(() => Assert.Equal(
            "Kunne ikke åpne den delte listen nå. Prøv igjen om litt.", Alert(cut)));
        Assert.Equal(3, RowNames(cut).Count);
    }

    [Fact]
    public void Open_WhenTheFieldNamesAKnownCode_ThenTheSharedListShowsReadOnly()
    {
        var store = new ShareStore();
        store.ByCode["AB12CD"] = new SharedList("Kollegas liste", Three);
        var cut = RenderView(new ShareClient(store) { OwnItems = [Item("Min egen", "EGEN")] });
        cut.WaitForAssertion(() => Assert.Single(RowNames(cut)));

        Button(cut, "Åpne delt liste").Click();
        Labelled(cut, "Delekode for listen du vil åpne").Change("ab12cd");
        Button(cut, "Åpne listen").Click();

        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));
        Assert.Contains("Kollegas liste", cut.Markup);
        Assert.Contains("Delt liste", cut.Markup);
        Assert.False(HasButton(cut, "Fjern"));
    }

    [Fact]
    public async Task Open_WhenAnEarlierCodeFailsAfterALaterOneOpened_ThenTheLaterListStays()
    {
        var store = new ShareStore();
        store.ByCode["BBBBBB"] = new SharedList("Den nyere listen", Three);
        var client = new ShareClient(store) { OwnItems = [Item("Min egen", "EGEN")] };
        var earlier = new TaskCompletionSource<SharedList?>();
        client.Hanging["AAAAAA"] = earlier;

        var cut = RenderView(client, shareCode: "AAAAAA");

        Button(cut, "Åpne delt liste").Click();
        Labelled(cut, "Delekode for listen du vil åpne").Change("BBBBBB");
        Button(cut, "Åpne listen").Click();
        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));

        await cut.InvokeAsync(() => earlier.SetException(new HttpRequestException("500")));

        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));
        Assert.Contains("Den nyere listen", cut.Markup);
        Assert.Equal("", Alert(cut));
    }

    // -----------------------------------------------------------------------
    // AC5: signed out, a link shows the list read-only and asks nothing of my/lists

    [Fact]
    public void Open_WhenSignedOutAtADelekodeLink_ThenTheRowsShowReadOnlyWithTheSignInSentence()
    {
        var store = new ShareStore();
        store.ByCode["AB12CD"] = new SharedList("Kollegas liste", Three);
        var client = new ShareClient(store);

        var cut = RenderExplorer(this, client, "/variabler?delekode=AB12CD", signedIn: false);

        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));

        var listTab = cut.FindAll("[role=tab]").Single(t => t.TextContent.Trim() == "Variabelliste");
        Assert.Equal("true", listTab.GetAttribute("aria-selected"));

        Assert.False(HasButton(cut, "Fjern"));
        Assert.Empty(cut.FindAll("table.munin-explorer-data-list input"));
        Assert.False(HasButton(cut, "Lagre som min liste"));
        Assert.Contains("Logg inn for å lagre listen som din egen.", cut.Markup);
        Assert.Equal(0, client.MyListsCalls);
    }

    [Fact]
    public void Close_WhenSignedOut_ThenTheCodeLeavesTheAddressAndTheTabGoes()
    {
        var store = new ShareStore();
        store.ByCode["AB12CD"] = new SharedList("Kollegas liste", Three);

        var cut = RenderExplorer(this, new ShareClient(store), "/variabler?delekode=AB12CD", signedIn: false);
        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));

        Button(cut, "Lukk delt liste").Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[role=tab]")));
        Assert.DoesNotContain("delekode", Mirrored(this) ?? "");
    }

    // -----------------------------------------------------------------------
    // AC6: saving refuses a name the reader already uses, and otherwise makes the list theirs

    [Fact]
    public void Save_WhenThePrefilledNameMatchesAnOwnListIgnoringCaseAndSpaces_ThenItIsRefusedBeforeAnyWrite()
    {
        var store = new ShareStore();
        store.ByCode["AB12CD"] = new SharedList("  MINE hjertevariabler ", Three);
        var client = new ShareClient(store) { OwnItems = [Item("Min egen", "EGEN")] };

        var cut = RenderView(client, shareCode: "AB12CD");
        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));

        Button(cut, "Lagre som min liste").Click();
        var field = Labelled(cut, "Navn på din kopi av listen");
        Assert.Equal("  MINE hjertevariabler ", field.GetAttribute("value"));

        Button(cut, "Lagre listen").Click();

        cut.WaitForAssertion(() =>
        {
            var name = Labelled(cut, "Navn på din kopi av listen");
            Assert.Equal("true", name.GetAttribute("aria-invalid"));
            Assert.Equal(
                "Du har allerede en liste med dette navnet. Velg et annet navn.",
                cut.Find($"#{name.GetAttribute("aria-describedby")}").TextContent.Trim());
        });
        Assert.Equal(0, client.CreateCalls);
        Assert.Empty(client.Added);
    }

    [Fact]
    public void Save_WhenTheNameIsUnique_ThenTheListIsCreatedFilledAndShownAndTheCodeLeavesTheAddress()
    {
        var store = new ShareStore();
        store.ByCode["AB12CD"] = new SharedList("Mine hjertevariabler", Three);
        var client = new ShareClient(store) { OwnItems = [Item("Min egen", "EGEN")] };

        var cut = RenderExplorer(this, client, "/variabler?delekode=AB12CD", signedIn: true);
        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));
        Assert.Contains("delekode=AB12CD", Mirrored(this));

        Button(cut, "Lagre som min liste").Click();
        Labelled(cut, "Navn på din kopi av listen").Change("Delt hjerteliste");
        Button(cut, "Lagre listen").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, client.CreateCalls);
            Assert.Equal(Three.Select(i => i.VariableId), client.Added);
            Assert.Equal("Delt hjerteliste", cut.Find("[id^=munin-explorer-list-heading-]").TextContent.Trim());
            Assert.Equal(3, RowNames(cut).Count);
            Assert.True(HasButton(cut, "Del liste"));
        });
        Assert.DoesNotContain("delekode", Mirrored(this) ?? "");
    }

    [Fact]
    public void Save_WhenTheNameIsUnique_ThenTheHeaderCountsTheSharedItemsAndTheListActionsAreEnabled()
    {
        // The adds land while the new list is not active, so the holder counts none of them until
        // the lists are read again. A button that merely exists passes with that bug present.
        var store = new ShareStore();
        store.ByCode["AB12CD"] = new SharedList("Mine hjertevariabler", Three);
        var client = new ShareClient(store) { OwnItems = [Item("Min egen", "EGEN")] };

        var cut = RenderView(client, shareCode: "AB12CD");
        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));

        Button(cut, "Lagre som min liste").Click();
        Labelled(cut, "Navn på din kopi av listen").Change("Delt hjerteliste");
        Button(cut, "Lagre listen").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Delt hjerteliste", cut.Find("[id^=munin-explorer-list-heading-]").TextContent.Trim());
            Assert.StartsWith("3 variabler", cut.Find(".munin-explorer-page__header p.caption").TextContent.Trim());

            foreach (var action in (string[])["Del liste", "Kopier liste", "Tøm liste"])
            {
                var button = Button(cut, action);
                Assert.NotEqual("true", button.GetAttribute("aria-disabled"));
                Assert.Null(button.GetAttribute("aria-describedby"));
            }
        });
        Assert.DoesNotContain("Listen er tom", cut.Markup);
    }

    [Fact]
    public void Save_WhenReadingTheListsAgainFails_ThenTheSaveStillSucceedsAndTheHostIsWarned()
    {
        var recorder = new RecordingLoggerProvider();
        Services.AddLogging(b => b
            .AddProvider(recorder)
            .AddFilter((category, _) => category?.StartsWith("Fhi.Munin.Explorer", StringComparison.Ordinal) == true));

        var store = new ShareStore();
        store.ByCode["AB12CD"] = new SharedList("Mine hjertevariabler", Three);
        var client = new ShareClient(store) { OwnItems = [Item("Min egen", "EGEN")], ReadAfterCreateThrows = true };

        var cut = RenderView(client, shareCode: "AB12CD");
        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));

        Button(cut, "Lagre som min liste").Click();
        Labelled(cut, "Navn på din kopi av listen").Change("Delt hjerteliste");
        Button(cut, "Lagre listen").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Delt hjerteliste", cut.Find("[id^=munin-explorer-list-heading-]").TextContent.Trim());
            Assert.Equal(3, RowNames(cut).Count);
        });
        Assert.Equal("", Alert(cut));
        Assert.False(HasButton(cut, "Lagre som min liste"));

        var warning = Assert.Single(recorder.Entries, e => e.Exception is HttpRequestException);
        Assert.Equal(LogLevel.Warning, warning.Level);
    }

    [Fact]
    public void Save_WhenTheApiRefusesTheAdds_ThenTheAlertSaysSoAndTheSharedListStays()
    {
        var store = new ShareStore();
        store.ByCode["AB12CD"] = new SharedList("Mine hjertevariabler", Three);
        var client = new ShareClient(store) { OwnItems = [Item("Min egen", "EGEN")], AddsRefused = true };

        var cut = RenderView(client, shareCode: "AB12CD");
        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));

        Button(cut, "Lagre som min liste").Click();
        Labelled(cut, "Navn på din kopi av listen").Change("Delt hjerteliste");
        Button(cut, "Lagre listen").Click();

        cut.WaitForAssertion(() => Assert.Equal("Kunne ikke lagre nå. Prøv igjen om litt.", Alert(cut)));
        Assert.Equal(1, client.CreateCalls);
        Assert.True(HasButton(cut, "Lukk delt liste"));
    }

    // -----------------------------------------------------------------------
    // AC7: an empty list cannot be shared, and says why

    [Theory]
    [InlineData("no", "Del liste", "Listen er tom", "Åpne delt liste")]
    [InlineData("en", "Share list", "The list is empty", "Open shared list")]
    public void Share_WhenTheListIsEmpty_ThenTheButtonIsAriaDisabledWithAVisibleReasonAndSendsNothing(
        string language, string share, string reason, string open)
    {
        var client = new ShareClient(new ShareStore()) { OwnItems = [] };
        var cut = RenderView(client, language: language);

        cut.WaitForAssertion(() => Assert.True(HasButton(cut, share)));
        var button = Button(cut, share);

        Assert.Equal("true", button.GetAttribute("aria-disabled"));
        Assert.Equal(reason, cut.Find($"#{button.GetAttribute("aria-describedby")}").TextContent.Trim());
        Assert.Null(button.GetAttribute("title"));

        button.Click();

        Assert.Equal(0, client.ShareCalls);

        var opener = Button(cut, open);
        Assert.NotEqual("true", opener.GetAttribute("aria-disabled"));
        Assert.False(opener.HasAttribute("disabled"));
    }

    // -----------------------------------------------------------------------
    // AC8: every new string, in both languages, on the rendered page

    public sealed record Words(
        string ShareList, string CodeLabel, string LinkLabel, string ByEmail, string Subject, string Validity,
        string ShareError, string OpenShared, string FieldLabel, string Submit, string Eyebrow, string Save,
        string SaveName, string SaveSubmit, string Close, string SignIn, string NotFound, string OpenError,
        string Required, string TooLong, string Taken);

    public static TheoryData<string, Words> Languages => new()
    {
        {
            "no", new Words(
                "Del liste", "Delekode", "Lenke til listen", "Send koden på e-post", "Delt variabelliste: Mine hjertevariabler",
                "Koden virker i 90 dager.", "Kunne ikke dele listen nå. Prøv igjen om litt.", "Åpne delt liste",
                "Delekode for listen du vil åpne", "Åpne listen", "Delt liste", "Lagre som min liste",
                "Navn på din kopi av listen", "Lagre listen", "Lukk delt liste", "Logg inn for å lagre listen som din egen.",
                "Fant ingen delt liste med koden ZZ99ZZ. En kode virker i 90 dager.",
                "Kunne ikke åpne den delte listen nå. Prøv igjen om litt.", "Skriv et navn på listen.",
                "Navnet kan være høyst 200 tegn.", "Du har allerede en liste med dette navnet. Velg et annet navn.")
        },
        {
            "en", new Words(
                "Share list", "Share code", "Link to the list", "Send the code by email", "Shared variable list: Mine hjertevariabler",
                "The code works for 90 days.", "Could not share the list just now. Try again shortly.", "Open shared list",
                "Share code of the list to open", "Open the list", "Shared list", "Save as my list",
                "Name of your copy of the list", "Save the list", "Close shared list", "Sign in to save the list as your own.",
                "No shared list was found with the code ZZ99ZZ. A code works for 90 days.",
                "Could not open the shared list just now. Try again shortly.", "Enter a name for the list.",
                "The name can be at most 200 characters.", "You already have a list with this name. Choose another name.")
        },
    };

    [Theory]
    [MemberData(nameof(Languages))]
    public void Words_WhenSharingAList_ThenEveryStringRendersInTheReadersLanguage(string language, Words w)
    {
        var cut = RenderView(new ShareClient(new ShareStore()) { OwnItems = Three }, language: language);
        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));

        Button(cut, w.ShareList).Click();

        cut.WaitForAssertion(() => Assert.Matches("^SHARE0$", Labelled(cut, w.CodeLabel).GetAttribute("value")));
        Assert.Equal("https://helsedata.example/variabler?delekode=SHARE0", Labelled(cut, w.LinkLabel).GetAttribute("value"));
        Assert.Contains(w.Validity, cut.Markup);

        var mail = cut.FindAll("a").Single(a => a.TextContent.Trim() == w.ByEmail);
        var href = Uri.UnescapeDataString(mail.GetAttribute("href")!);
        Assert.StartsWith("mailto:", href);
        Assert.Contains(w.Subject, href);
        Assert.Contains("SHARE0", href);

        Button(cut, w.OpenShared).Click();
        Labelled(cut, w.FieldLabel).Change("zz99zz");
        Button(cut, w.Submit).Click();

        cut.WaitForAssertion(() => Assert.Equal(w.NotFound, Alert(cut)));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Words_WhenSharingOrOpeningFails_ThenTheSentencesRenderInTheReadersLanguage(string language, Words w)
    {
        var client = new ShareClient(new ShareStore())
        {
            OwnItems = Three,
            ShareThrows = new HttpRequestException("500"),
            SharedThrows = new HttpRequestException("500"),
        };
        var cut = RenderView(client, language: language);
        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));

        Button(cut, w.ShareList).Click();
        cut.WaitForAssertion(() => Assert.Equal(w.ShareError, Alert(cut)));

        Button(cut, w.OpenShared).Click();
        Labelled(cut, w.FieldLabel).Change("AB12CD");
        Button(cut, w.Submit).Click();
        cut.WaitForAssertion(() => Assert.Equal(w.OpenError, Alert(cut)));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Words_WhenASharedListIsShownAndSaved_ThenEveryStringRendersInTheReadersLanguage(string language, Words w)
    {
        var store = new ShareStore();
        store.ByCode["AB12CD"] = new SharedList("Mine hjertevariabler", Three);
        var cut = RenderView(new ShareClient(store) { OwnItems = [Item("Min egen", "EGEN")] }, language: language, shareCode: "AB12CD");
        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));

        Assert.Contains(w.Eyebrow, cut.Markup);
        Assert.True(HasButton(cut, w.Close));

        Button(cut, w.Save).Click();
        Assert.True(HasButton(cut, w.SaveSubmit));

        string ProblemAfter(string name)
        {
            Labelled(cut, w.SaveName).Change(name);
            Button(cut, w.SaveSubmit).Click();

            var field = Labelled(cut, w.SaveName);
            return cut.Find($"#{field.GetAttribute("aria-describedby")}").TextContent.Trim();
        }

        cut.WaitForAssertion(() => Assert.Equal(w.Taken, ProblemAfter("mine hjertevariabler")));
        cut.WaitForAssertion(() => Assert.Equal(w.Required, ProblemAfter("   ")));
        cut.WaitForAssertion(() => Assert.Equal(w.TooLong, ProblemAfter(new string('x', 201))));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Words_WhenASharedListIsShownSignedOut_ThenTheSignInSentenceRendersInTheReadersLanguage(string language, Words w)
    {
        var store = new ShareStore();
        store.ByCode["AB12CD"] = new SharedList("Kollegas liste", Three);
        var cut = RenderView(new ShareClient(store), signedIn: false, language: language, shareCode: "AB12CD");

        cut.WaitForAssertion(() => Assert.Equal(3, RowNames(cut).Count));
        Assert.Contains(w.SignIn, cut.Markup);
        Assert.Contains(w.Eyebrow, cut.Markup);
    }
}
