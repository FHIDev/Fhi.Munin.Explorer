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

    /// <summary>The name field of the rename form, which its own label points at.</summary>
    private string RenameListNameId => $"munin-explorer-rename-list-{_instance}";

    /// <summary>The one control that arms the deletion question and stands it down again.</summary>
    private string DeleteButtonId => $"munin-explorer-delete-list-{_instance}";

    /// <summary>The question, which the button that acts on it is described by.</summary>
    private string ConfirmDeleteId => $"munin-explorer-confirm-delete-{_instance}";

    private Page<VariableListItem>? _page;
    private Guid? _shownList;
    private int _pageNumber = 1;
    private bool _loading;
    private bool _failed;

    private Dictionary<string, string>? _dataTypeNames;

    private string? _dataTypeNamesLanguage;
    private string _newName = "";
    private string _renameName = "";
    private bool _confirmingDelete;
    private ListActionFailure _actionFailure;
    private bool _includeKodeverk;
    private bool _downloading;
    private DownloadFailure _downloadFailure;
    private ListActionFailure _createFailure;

    /// <summary>How the last download ended, when it ended badly.</summary>
    /// <remarks>
    /// Worth telling the two apart for the same reason the save button does: a throttled reader
    /// told to try again shortly does the one thing that keeps the limiter's window full.
    /// </remarks>
    private enum DownloadFailure
    {
        /// <summary>Nothing has gone wrong — what an untried, or a since retried, download reads as.</summary>
        None = 0,

        /// <summary>The download threw for a reason the reader can only try again on.</summary>
        Failed,

        /// <summary>The API refused it because too many requests arrived — HTTP 429.</summary>
        Throttled
    }

    /// <summary>
    /// What the alert says about the last download, or <see langword="null"/> when it has nothing
    /// to say.
    /// </summary>
    private string? DownloadMessage => _downloadFailure switch
    {
        DownloadFailure.Throttled => T.RateLimitError,
        DownloadFailure.Failed => T.DownloadError,
        _ => null
    };

    /// <summary>What the alert says about a failed create, or <see langword="null"/> after none.</summary>
    private string? CreateMessage => _createFailure switch
    {
        ListActionFailure.Throttled => T.RateLimitError,
        ListActionFailure.Failed => T.SaveError,
        _ => null
    };

    /// <summary>
    /// One page read a rename may skip. Consumed by the first notification rather than held for the
    /// call, or a removal raised mid-rename would be swallowed with it.
    /// </summary>
    private bool _skipOnePageRead;

    /// <summary>Why a rename or a delete did not happen. The shape the save button uses.</summary>
    private enum ListActionFailure
    {
        /// <summary>Nothing has gone wrong.</summary>
        None = 0,

        /// <summary>It threw or was refused, for a reason the reader can only try again on.</summary>
        Failed,

        /// <summary>The API refused it because too many requests arrived — HTTP 429.</summary>
        Throttled
    }

    /// <summary>
    /// What the alert region says about the last rename or delete. A throttle is told apart from an
    /// ordinary failure because the remedy differs: wait, rather than try again.
    /// </summary>
    private string? ActionMessage => _actionFailure switch
    {
        ListActionFailure.Throttled => T.RateLimitError,
        ListActionFailure.Failed => T.ListActionError,
        _ => null
    };

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
    private string? Period(VariableListItem item)
    {
        // Null rather than "Ikke oppgitt": the cell writes that itself, the way it does for every
        // other column the catalogue has no value for.
        if (item.DataFrom is null && item.DataTo is null)
        {
            return null;
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

    /// <summary>
    /// The columns of one row, drawn by the same helper the search results use.
    /// </summary>
    /// <remarks>
    /// A fragment rather than markup because Blazor gives one its own sequence-number region, so
    /// these numbers stand clear of the surrounding markup's — spaced <see cref="RowCell.Slots"/>
    /// apart, which is what the helper's own note about rising numbers requires.
    /// </remarks>
    private RenderFragment Cells(VariableListItem item) => builder =>
    {
        RowCell.Write(builder, 100, T.FieldCode, item.VariableCode, "code", T.NotSpecified);
        RowCell.Write(builder, 200, T.FieldSource, item.KildeShortName ?? item.KildeName, "source", T.NotSpecified, tooltip: item.KildeName);
        RowCell.Write(builder, 300, T.FieldDataCollection, item.DatasamlingName, "dataCollection", T.NotSpecified);
        RowCell.Write(builder, 400, T.FieldVariableGroup, item.VariabelgruppeName, "theme", T.NotSpecified);
        RowCell.Write(builder, 500, T.FieldDataType, DataTypeName(item.DataType), "dataType", T.NotSpecified);

        // The only column whose words are this component's rather than the catalogue's — the dates
        // are formatted for the reader — so it is left unmarked, exactly as the explorer leaves it.
        RowCell.Write(builder, 600, T.FieldDataPeriod, Period(item), "period", T.NotSpecified, catalogue: false);
    };

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
        if (_skipOnePageRead)
        {
            _skipOnePageRead = false;
        }
        else
        {
            await LoadPageAsync();
        }

        StateHasChanged();
    });

    /// <summary>Reads the page currently being looked at. Signed out this calls nothing.</summary>
    private async Task LoadPageAsync()
    {
        // A list the holder no longer has is one somebody deleted. Asking for its variables gets
        // null back — "no such list of yours" — which renders as an empty table for a list that is
        // gone, so the ask is not made at all and the shown list is repointed by the caller.
        if (State?.IsAuthenticated != true
            || _shownList is null
            || !Lists.Any(l => l.Id == _shownList))
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
        ForgetListControls();
        await LoadPageAsync();
    }

    /// <summary>
    /// Drops what the rename and delete controls held for the list that has left the screen. The
    /// confirmation is the one that matters: armed on one list, it would delete another.
    /// </summary>
    private void ForgetListControls()
    {
        _confirmingDelete = false;
        _renameName = "";
    }

    /// <summary>
    /// Empties the alert region — the one place that does. Four conditions share it, so a handler
    /// clearing only its own leaves an older one answering for what the reader just did.
    /// </summary>
    private void ForgetFailures()
    {
        _failed = false;
        _createFailure = ListActionFailure.None;
        _actionFailure = ListActionFailure.None;
        _downloadFailure = DownloadFailure.None;
    }

    private async Task ChooseListAsync(ChangeEventArgs e)
    {
        if (State is null || !Guid.TryParse(e.Value?.ToString(), out var id))
        {
            return;
        }

        _shownList = id;
        _pageNumber = 1;
        ForgetListControls();
        ForgetFailures();

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

        ForgetFailures();

        VariableList? created;

        try
        {
            created = await State.CreateAsync(name);
        }
        catch (MuninExplorerRateLimitedException)
        {
            // Creating meets the same limiter the saves do, and "prøv igjen om litt" is advice
            // a throttled reader cannot use.
            _createFailure = ListActionFailure.Throttled;
            return;
        }
        catch (Exception)
        {
            // Uncaught, this leaves the event handler and takes the circuit with it: a blank
            // page and a reconnect banner in place of the list the reader was building.
            _createFailure = ListActionFailure.Failed;
            return;
        }

        if (created is null)
        {
            return;
        }

        _newName = "";
        ForgetListControls();

        try
        {
            await State.SetActiveListAsync(created.Id);
        }
        catch (Exception)
        {
            // Same reason as ChooseListAsync above. The list was created; it is the switch to
            // it that did not happen, which is what ListLoadError says.
            _failed = true;
            return;
        }

        _shownList = created.Id;
        _pageNumber = 1;
        await LoadPageAsync();
    }

    /// <summary>
    /// Gives the list on screen the name in the rename field. Nothing is read again: the holder
    /// patches its own copy and tells the other surfaces.
    /// </summary>
    private async Task RenameListAsync()
    {
        var name = _renameName.Trim();

        if (State is null || _shownList is null || name.Length == 0)
        {
            return;
        }

        ForgetFailures();
        _skipOnePageRead = true;

        try
        {
            if (await State.RenameAsync(_shownList.Value, name))
            {
                _renameName = "";
            }
            else
            {
                _actionFailure = ListActionFailure.Failed;
            }
        }
        catch (MuninExplorerRateLimitedException)
        {
            // These writes go through the client every read on the page uses, and meet the same
            // per-address limiter, so a refusal here is ordinary rather than rare.
            _actionFailure = ListActionFailure.Throttled;
        }
        catch (Exception)
        {
            // An uncaught throw out of an event handler takes the whole circuit down, which is a
            // far worse answer to a failed rename than a line of text.
            _actionFailure = ListActionFailure.Failed;
        }
        finally
        {
            // A rename that never reached the holder raised nothing, so the allowance would
            // otherwise sit here and be spent on somebody else's notification.
            _skipOnePageRead = false;
        }
    }

    /// <summary>
    /// Deletes the list on screen, once confirmed. The next list is taken from the holder rather
    /// than picked here, so this view and the explorer's save button stay on the same one.
    /// </summary>
    private async Task DeleteListAsync()
    {
        if (State is null || _shownList is null)
        {
            return;
        }

        _confirmingDelete = false;
        ForgetFailures();

        try
        {
            if (!await State.DeleteAsync(_shownList.Value))
            {
                _actionFailure = ListActionFailure.Failed;
                return;
            }

            await State.EnsureActiveListAsync();
        }
        catch (MuninExplorerRateLimitedException)
        {
            _actionFailure = ListActionFailure.Throttled;
        }
        catch (Exception)
        {
            // Caught for the reason the rename above gives. The list may well be gone on the
            // server, so the view is repointed below whichever of the two calls threw.
            _actionFailure = ListActionFailure.Failed;
        }

        await ShowActiveListAsync();
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
        ForgetFailures();

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
        catch (MuninExplorerRateLimitedException)
        {
            // The export sits under the browse policy, not the write one the saves use, and the
            // id walk in front of it counts against that same bucket — keyed per user here, since
            // the view only renders signed in. The generic sentence names no cause; this one does.
            _downloadFailure = DownloadFailure.Throttled;
        }
        catch (Exception)
        {
            // Includes the browser refusing the blob — a Content-Security-Policy without blob:
            // would land here. Said out loud rather than left as a button that does nothing.
            _downloadFailure = DownloadFailure.Failed;
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
