using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The reader's saved variable lists: which lists they have, what is in the one they are looking
/// at, and the two things they can do to it — take a variable out, or make another list.
/// </summary>
/// <remarks>
/// <para>
/// A separate root component rather than a tab inside <see cref="VariableExplorer"/>, because the
/// host decides where it goes: helsedata's stories put "mine variabellister" on its own page, and a
/// component that assumed otherwise would be unmountable there.
/// </para>
/// <para>
/// It shares <see cref="VariableListState"/> with the explorer's save button, so removing a
/// variable here is reflected there without either surface refetching. What it does not share is
/// paging: the holder deliberately does not wrap <c>GetMyListVariablesAsync</c>, because which page
/// is being looked at belongs to the surface looking at it, not to a holder three surfaces read.
/// </para>
/// </remarks>
public sealed partial class VariableListView : ComponentBase, IDisposable
{
    [Inject] private IServiceProvider ServiceProvider { get; set; } = null!;
    [Inject] private IMuninExplorerClient Client { get; set; } = null!;
    [Inject] private IJSRuntime Js { get; set; } = null!;

    private VariableListState? _state;
    private VariableListState? State => _state ??= ServiceProvider.GetService<VariableListState>();

    /// <inheritdoc cref="VariableExplorer.Language"/>
    [Parameter] public string Language { get; set; } = "no";

    /// <inheritdoc cref="VariableExplorer.IsAuthenticated"/>
    [Parameter] public bool IsAuthenticated { get; set; }

    /// <summary>Heading level for this component's own title, 1–6. Defaults to <c>2</c>.</summary>
    [Parameter] public int HeadingLevel { get; set; } = 2;

    /// <summary>Entries per page. The API clamps to 1000; its own default is 100.</summary>
    [Parameter] public int PageSize { get; set; } = 25;

    /// <summary>
    /// Clamped, the way the explorer clamps its own: the parameter is documented as 1-6, and a host
    /// that passes 0 or 7 would otherwise get an &lt;h0&gt; - not a heading at all, and invisible to the
    /// heading navigation the level exists to keep intact.
    /// </summary>
    private int TitleLevel => Math.Clamp(HeadingLevel, 1, 6);

    private Texts T => Texts.For(Language);

    /// <summary>
    /// Per-mount discriminator for every id this component renders: the create form's name field,
    /// and per row the name cell and the remove button that is named from it. The shape the
    /// explorer's own ids use — see <c>VariableExplorer.razor.cs</c>, where the convention lives.
    /// </summary>
    /// <remarks>
    /// The host decides where this component goes and can mount it twice on one page. Two fields
    /// sharing one id would leave both labels pointing at the first, so the second field is
    /// unnamed again — the very defect the label was added to fix, and invisible in a page with
    /// one mount, which is why the guard for it renders two.
    /// </remarks>
    private readonly string _instance = Guid.NewGuid().ToString("N")[..8];

    /// <summary>The name field of the create form, which its label points at.</summary>
    private string NewListNameId => $"munin-explorer-new-list-{_instance}";

    private Page<VariableListItem>? _page;
    private Guid? _shownList;
    private int _pageNumber = 1;
    private bool _loading;
    private bool _failed;

    private Dictionary<string, string>? _dataTypeNames;

    private string? _dataTypeNamesLanguage;
    private string _newName = "";
    private bool _includeKodeverk;
    private bool _downloading;
    private bool _downloadFailed;

    private IReadOnlyList<VariableList> Lists => State?.Lists ?? [];

    /// <summary>The name of the list on screen, which is what names the table of its variables.</summary>
    /// <remarks>
    /// The reader's own word for the list rather than this component's heading: with several lists
    /// saved, "Mine variabellister" would name every one of them the same and a screen reader
    /// moving between tables could not tell which is on screen. Falls back to the heading before
    /// the lists have arrived, so the table is never nameless.
    /// </remarks>
    private string ShownListName =>
        Lists.FirstOrDefault(l => l.Id == _shownList)?.Name is { Length: > 0 } name
            ? name
            : T.MyListsHeading;

    /// <summary>
    /// The years a variable has data for, written the way the result rows and the detail panel
    /// write it — the same words as the explorer's own period column, so a variable does not
    /// read differently here than where it was saved from. Only the words: the explorer draws a
    /// block with a coverage bar beside it, and this is one cell in a row.
    /// </summary>
    private string Period(VariableListItem item)
    {
        if (item.DataFrom is null && item.DataTo is null)
        {
            return T.NotSpecified;
        }

        var from = item.DataFrom is { } f ? MonthYear(f) : "?";
        var to = item.DataTo is { } t ? MonthYear(t) : T.Ongoing;

        return $"{from} – {to}";
    }

    /// <summary>
    /// What the row calls its variable — its name, or the sentence shown in place of one that is
    /// no longer in the catalogue.
    /// </summary>
    /// <remarks>
    /// The name column's own text, so there is one place that decides what an orphaned row reads
    /// as. It used to be decided twice, inline in the markup and here. The remove button is named
    /// from the rendered cell rather than from a second call to this, so the two cannot drift.
    /// </remarks>
    private string RowName(VariableListItem item) =>
        string.IsNullOrWhiteSpace(item.VariableName) ? T.VariableNoLongerAvailable : item.VariableName;

    /// <summary>The name cell of one row, which the row's remove button is named from.</summary>
    private string RowNameId(VariableListItem item) =>
        $"munin-explorer-list-name-{_instance}-{item.VariableId:N}";

    /// <summary>The remove button of one row, which names itself from its own words first.</summary>
    private string RemoveButtonId(VariableListItem item) =>
        $"munin-explorer-list-remove-{_instance}-{item.VariableId:N}";

    /// <summary>
    /// The remove button's accessible name, as two elements: its own word, then the row's name.
    /// </summary>
    /// <remarks>
    /// Every one of these buttons says the single word "Fjern", so a list of forty is forty
    /// controls announcing the same thing with nothing to say which row the reader is on (WCAG
    /// 4.1.2). Pointing at two elements rather than writing one <c>aria-label</c> is the rule the
    /// explorer's save button follows and for the same reason: "Fjern" is ours and follows
    /// <see cref="Language"/>, the variable's name is Munin's and is marked <c>lang="no"</c> in
    /// the cell, and a single string would hand both to one voice (WCAG 3.1.2). The button first,
    /// so the visible word opens the name and speech input still reaches it (WCAG 2.5.3).
    /// <para>
    /// An orphaned row has no name, and borrows the sentence its cell shows instead — so the
    /// button still says what it removes, and says it without spelling out a GUID the row does not
    /// display anywhere. Two orphans then announce alike, which is a duplicate name and not a
    /// failure: 4.1.2 asks for a name and 2.4.6 asks that it describe, neither that it be unique.
    /// </para>
    /// </remarks>
    private string RemoveLabelledBy(VariableListItem item) =>
        $"{RemoveButtonId(item)} {RowNameId(item)}";

    /// <summary>A value, or the catalogue's own words for one that was never set.</summary>
    /// <remarks>
    /// The explorer writes NotSpecified in this column rather than leaving it blank, and an empty cell
    /// beside a filled one reads as data that went missing rather than data nobody entered.
    /// </remarks>
    private string Or(string? value) =>
        string.IsNullOrWhiteSpace(value) ? T.NotSpecified : value;

    /// <summary>
    /// <c>"no"</c> for a value the catalogue wrote, and nothing at all for our own fallback.
    /// </summary>
    /// <remarks>
    /// Catalogue text is Norwegian whatever language the page is in, which is why these cells carry
    /// lang="no" at all. NotSpecified is not catalogue text - it is ours, in the reader's language -
    /// and marking an English "Not specified" as Norwegian makes a screen reader pronounce it as
    /// Norwegian. WCAG 3.1.2. A null here leaves the attribute off entirely.
    /// </remarks>
    private static string? CatalogueLang(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : "no";

    /// <summary>A date as month and year, in the reader's language.</summary>
    /// <remarks>The same format string as the explorer's own, so the two read alike.</remarks>
    private string MonthYear(DateTimeOffset date) =>
        date.ToString("MMM yyyy", CatalogueProperties.Culture(Language));

    /// <summary>
    /// The API's own answer, not ours. The client already derives it when the envelope omits it
    /// (this endpoint's does), and deliberately leaves it alone when present — so recomputing here
    /// would put our arithmetic in front of the API's number on the day the two disagree.
    /// </summary>
    private int TotalPages => Math.Max(1, _page?.TotalPages ?? 1);

    /// <summary>Written the way the result list writes it, so the pager reads the same on both.</summary>
    private static string AriaDisabled(bool enabled) => enabled ? "false" : "true";

    protected override void OnInitialized()
    {
        if (State is not null)
        {
            // The holder tells every surface when one of them changes a list. Without this the save
            // button could remove a variable and this view would go on showing it.
            State.Changed += OnStateChanged;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (State is null)
        {
            return;
        }

        State.SetAuthenticated(IsAuthenticated);

        // Caught here, like the save button's own read in VariableExplorer.Lists.cs:38. An exception
        // out of a lifecycle method takes the whole circuit down with it, which in helsedata's
        // legacy host means the entire CMS page — and the mount fires this read alongside the search
        // and the facet refresh, which is exactly the burst the per-address limiter counts. A 429
        // here is an ordinary event, not a rare one.
        //
        // Said on screen rather than swallowed: unlike the save button, this component has nothing
        // to show if the read failed, so silence would be an empty list that looks like an empty
        // list.
        try
        {
            await State.EnsureActiveListAsync();
            await ShowActiveListAsync();
        }
        catch (Exception)
        {
            _page = null;
            _failed = true;
        }

        await LoadDataTypeNamesAsync();
    }

    /// <summary>The datatype display names, read once per mount.</summary>
    /// <remarks>
    /// <para>
    /// Asked for without a search or a filter, unlike the facet refresh in the explorer beside this
    /// view. That call is scoped to what the reader is looking at, so a search matching no integers
    /// comes back with no entry for the integer code - and this view has to name codes the reader
    /// saved at some other time, under some other search.
    /// </para>
    /// <para>
    /// Failure leaves the map empty, and an empty map renders the code. A list saying 2 where it
    /// could say Heltall is worse than the explorer beside it, but it is still the reader's list;
    /// losing the whole view over a label would not be.
    /// </para>
    /// </remarks>
    private async Task LoadDataTypeNamesAsync()
    {
        // Nothing is asked for a reader who is not signed in. This view renders nothing for them, and
        // a call whose answer nobody sees is still a call the limiter counts - the same reason the
        // list itself is not read either.
        if (!IsAuthenticated)
        {
            return;
        }

        // Keyed on the language it was read in, not merely on having been read. The host can change
        // Language after mount, and names fetched for the previous one would sit there until the
        // component is recreated.
        if (_dataTypeNames is not null
            && string.Equals(_dataTypeNamesLanguage, Language, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            // Search and filter left at their defaults on purpose - see the remarks above.
            var facets = await Client.GetFiltersAsync(
                language: ReaderLanguage.ForApi(Language));

            _dataTypeNames = facets.DataTypes
                .Where(d => !string.IsNullOrWhiteSpace(d.Value)
                    && !string.IsNullOrWhiteSpace(d.DisplayName))
                .GroupBy(d => d.Value!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().DisplayName!, StringComparer.Ordinal);

            _dataTypeNamesLanguage = Language;
            StateHasChanged();
        }
        catch (Exception)
        {
            // Recorded as attempted for this language, so a failing endpoint is asked once rather
            // than on every parameter change. The reader sees the codes until the language changes
            // or the page is loaded again, which is the same fallback an empty map gives.
            _dataTypeNames = new Dictionary<string, string>(StringComparer.Ordinal);
            _dataTypeNamesLanguage = Language;
        }
    }

    /// <summary>The readable name for a datatype code, or the code when there is no name.</summary>
    /// <remarks>
    /// The same shape as <c>VariableExplorer.DataTypeName</c>, and for the same reason: the codes are
    /// editable master data on the API's side, so the names are read from it rather than written into
    /// a table that ships to other people and goes stale where nobody is looking.
    /// </remarks>
    private string? DataTypeName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || _dataTypeNames is null)
        {
            return code;
        }

        return _dataTypeNames.TryGetValue(code, out var named) ? named : code;
    }


    /// <summary>
    /// Another surface changed a list. Re-read the page rather than only re-rendering: the rows
    /// come from <c>_page</c>, which the holder does not own, so a save button that removed a
    /// variable would otherwise leave it on screen here — the very thing this subscription exists
    /// to prevent.
    /// </summary>
    private void OnStateChanged() => InvokeAsync(async () =>
    {
        await LoadPageAsync();
        StateHasChanged();
    });

    /// <summary>Reads the page currently being looked at. Signed out this calls nothing.</summary>
    private async Task LoadPageAsync()
    {
        if (State?.IsAuthenticated != true || _shownList is null)
        {
            _page = null;
            return;
        }

        _loading = true;
        _failed = false;

        try
        {
            _page = await Client.GetMyListVariablesAsync(_shownList.Value, _pageNumber, PageSize);
        }
        catch (Exception)
        {
            // Said here rather than thrown on: an unhandled exception out of a lifecycle method
            // takes the circuit down, which is a worse answer than a line of text.
            _page = null;
            _failed = true;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task ShowActiveListAsync()
    {
        var target = State?.ActiveListId;

        if (target == _shownList && _page is not null)
        {
            return;
        }

        _shownList = target;
        _pageNumber = 1;
        await LoadPageAsync();
    }

    private async Task ChooseListAsync(ChangeEventArgs e)
    {
        if (State is null || !Guid.TryParse(e.Value?.ToString(), out var id))
        {
            return;
        }

        _shownList = id;
        _pageNumber = 1;

        try
        {
            await State.SetActiveListAsync(id);
        }
        catch (Exception)
        {
            // Same reason as the lifecycle read above: an uncaught throw out of an event handler
            // takes the circuit with it. LoadPageAsync below has its own catch and will say so.
            _failed = true;
            return;
        }

        await LoadPageAsync();
    }

    private async Task GoToPageAsync(int page)
    {
        if (page < 1 || page > TotalPages || page == _pageNumber)
        {
            return;
        }

        _pageNumber = page;
        await LoadPageAsync();
    }

    private async Task CreateListAsync()
    {
        var name = _newName.Trim();

        if (State is null || name.Length == 0)
        {
            return;
        }

        var created = await State.CreateAsync(name);

        if (created is null)
        {
            return;
        }

        _newName = "";
        await State.SetActiveListAsync(created.Id);
        _shownList = created.Id;
        _pageNumber = 1;
        await LoadPageAsync();
    }

    private async Task RemoveAsync(Guid variableId)
    {
        if (State is null || _shownList is null)
        {
            return;
        }

        // The holder raises Changed, and OnStateChanged re-reads the page — so no fetch here.
        if (await State.RemoveVariablesAsync(_shownList.Value, [variableId]))
        {
            await RetreatFromEmptyPageAsync();
        }
    }

    /// <summary>
    /// Steps back when the page being looked at no longer exists.
    /// </summary>
    /// <remarks>
    /// Taking the last row off page three leaves a page three with nothing on it, and the empty
    /// state replaces the pager along with the rows — so the reader is told the list is empty and
    /// has no control left to reach the two pages that still have things on them.
    /// </remarks>
    private async Task RetreatFromEmptyPageAsync()
    {
        while (_pageNumber > 1 && _page is not null && _page.Items.Count == 0)
        {
            _pageNumber--;
            await LoadPageAsync();
        }
    }

    /// <summary>
    /// Fetches the whole list from the API and hands it to the browser.
    /// </summary>
    /// <remarks>
    /// Every id, not the page on screen: the reader asked for their list, and a download that
    /// quietly contained only the 25 rows they happened to be looking at would be wrong in a way
    /// nobody would notice until they opened the file.
    /// </remarks>
    private async Task DownloadAsync(ExportFormat format)
    {
        if (_shownList is null || _downloading)
        {
            return;
        }

        _downloading = true;
        _downloadFailed = false;

        try
        {
            var ids = await AllVariableIdsAsync();

            if (ids.Count == 0)
            {
                return;
            }

            var file = await Client.ExportListAsync(ids, format, _includeKodeverk);
            await BrowserDownload.OfferAsync(Js, file);
        }
        catch (Exception)
        {
            // Includes the browser refusing the blob — a Content-Security-Policy without blob:
            // would land here. Said out loud rather than left as a button that does nothing.
            _downloadFailed = true;
        }
        finally
        {
            _downloading = false;
        }
    }

    /// <summary>Every id in the list, read a page at a time.</summary>
    private async Task<List<Guid>> AllVariableIdsAsync()
    {
        // A set, not a list, for the reason LoadMembershipAsync uses one: the list can be changed
        // in another tab while these pages are being read, and an entry that drifts across a page
        // boundary would otherwise appear twice in the downloaded file.
        var ids = new HashSet<Guid>();
        var page = 1;

        while (true)
        {
            // The API's own ceiling per page, so a long list costs few round trips.
            var slice = await Client.GetMyListVariablesAsync(_shownList!.Value, page, 1000);

            if (slice is null || slice.Items.Count == 0)
            {
                break;
            }

            foreach (var item in slice.Items)
            {
                ids.Add(item.VariableId);
            }

            if (ids.Count >= slice.TotalCount)
            {
                break;
            }

            page++;
        }

        return [.. ids];
    }

    public void Dispose()
    {
        if (_state is not null)
        {
            _state.Changed -= OnStateChanged;
        }
    }
}
