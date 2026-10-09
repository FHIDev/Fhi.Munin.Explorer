using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.Display;
using Fhi.Munin.Explorer.Logging;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    private ILogger? _log;

    /// <summary>The host's logger, or none — see <see cref="ExplorerLog"/>.</summary>
    private ILogger? Log => _log ??= ExplorerLog.For<VariableListView>(ServiceProvider);

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
    /// Per-mount discriminator for every id this component renders, in the shape the explorer's ids use
    /// (<c>VariableExplorer.razor.cs</c>). A host can mount this twice, and shared ids point both labels
    /// at the first field, which only shows with two mounts; the guard for it renders two.
    /// </summary>
    private readonly string _instance = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// The title of this surface: an anchor within one page load, not a deep link. Minted per mount
    /// like every other id here, so no URL fragment and no host-written <c>aria-labelledby</c> can
    /// name it — nothing mounts this view inside a region that would need to.
    /// </summary>
    private string ListHeadingId => $"munin-explorer-list-heading-{_instance}";

    /// <summary>The name field of the create form, which its label points at.</summary>
    private string NewListNameId => $"munin-explorer-new-list-{_instance}";

    /// <summary>The name field of the rename form, which its own label points at.</summary>
    private string RenameListNameId => $"munin-explorer-rename-list-{_instance}";

    /// <summary>
    /// The two controls that reveal the create and the rename field. Ids for the reason
    /// <see cref="DeleteButtonId"/> is one: the words follow <see cref="Language"/>.
    /// </summary>
    private string CreateToggleId => $"munin-explorer-create-toggle-{_instance}";

    private string RenameToggleId => $"munin-explorer-rename-toggle-{_instance}";

    /// <summary>The one control that arms the deletion question and stands it down again.</summary>
    private string DeleteButtonId => $"munin-explorer-delete-list-{_instance}";

    /// <summary>The question, which the button that acts on it is described by.</summary>
    private string ConfirmDeleteId => $"munin-explorer-confirm-delete-{_instance}";

    /// <summary>The sentence a refused annotation is explained by, which its field points at.</summary>
    private string DesiredDataRefusalId => $"munin-explorer-list-desired-data-refusal-{_instance}";

    /// <summary>The word "Ønskede data" in one row, which that row's field is named from.</summary>
    private string DesiredDataLabelId(VariableListItem item) =>
        $"munin-explorer-list-desired-data-label-{_instance}-{ItemSuffix(item)}";

    /// <summary>
    /// The annotation field's name: the column's word, then the row's, so forty fields do not all just
    /// say "Ønskede data" (WCAG 4.1.2). Two elements rather than one <c>aria-label</c>, so our word and
    /// Munin's Norwegian name keep their own languages (WCAG 3.1.2).
    /// </summary>
    private string DesiredDataLabelledBy(VariableListItem item) =>
        $"{DesiredDataLabelId(item)} {RowNameId(item)}";

    private Page<VariableListItem>? _page;
    private Guid? _shownList;

    // Moved on every change of the list on screen, so a copy can tell the reader left and came back.
    private int _shownListMoves;
    private int _pageNumber = 1;

    /// <summary>The holder's filter version as of the last change this view acted on.</summary>
    private int _seenKildeFilter;
    private bool _loading;
    private bool _failed;
    private bool _askedOnMount;
    private bool _retryingLists;

    // Set while a copy or a shared save runs its own follow-up reads; it reads the page itself,
    // once, if the page it leaves on screen needs reading.
    private bool _holdingReloads;
    private bool _focusAfterRetry;
    private bool _focusAfterCreate;
    private bool _focusAfterRename;
    private int _formsOpenAtCreate;
    private int _formsOpenAtRename;
    private int _movesAtRename;
    private ElementReference _listPicker;
    private ElementReference _createToggle;
    private ElementReference _listRegion;

    private Dictionary<string, string>? _dataTypeNames;

    private string? _dataTypeNamesLanguage;
    private string _newName = "";
    private string _renameName = "";

    // Both start closed and fold once their write succeeds. The reader is standing on the button
    // inside the block, so TakeFocusTarget moves focus on rather than letting the fold drop it.
    private bool _creating;

    private bool _renaming;
    private bool _confirmingDelete;
    private ListActionFailure _actionFailure;
    private bool _includeKodeverk;
    private bool _downloading;
    private DownloadFailure _downloadFailure;
    private ListActionFailure _createFailure;

    /// <summary>
    /// What the reader has typed, per row. The fields render from here rather than from <c>_page</c>,
    /// so a refusal does not empty the field they were just told is too long. Reseeded on every page
    /// read, since only the API knows what was actually saved.
    /// </summary>
    private readonly Dictionary<VariableDatasamlingKey, string> _desiredData = [];

    private DesiredDataFailure _desiredDataFailure;

    /// <summary>How many writes each row has had, so an older answer can be told from the newest.</summary>
    /// <remarks>
    /// Blur saves, so a corrected note overlaps the first write. Ordered by arrival, the first write's
    /// answer could land last and mark an accepted text, with nothing after it to take the mark away.
    /// </remarks>
    private readonly Dictionary<VariableDatasamlingKey, int> _desiredDataWrites = [];

    /// <summary>How many times the fields have been seeded from a page read.</summary>
    /// <remarks>
    /// A read landing under a write reseeds every field from the API, which drops the draft — so an
    /// answer from before it is one about a text the reader can no longer see.
    /// </remarks>
    private int _desiredDataSeeds;

    /// <summary>The row the API refused for length, the list it was refused in, and the ceiling.</summary>
    /// <remarks>
    /// Outside <see cref="ForgetFailures"/>: it stands until its unsaved row is written again or leaves
    /// the list. The list is kept because a variable can sit in two, and the mark must not follow it.
    /// </remarks>
    private DesiredDataRefusal? _desiredDataRefusal;

    /// <summary>One refused row: which list it is in, which row, and the ceiling to shorten to.</summary>
    private sealed record DesiredDataRefusal(Guid ListId, VariableDatasamlingKey VariableId, int MaxLength);

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

    /// <summary>How the last annotation write ended, when it ended badly.</summary>
    /// <remarks>
    /// A length refusal is its own state rather than one of these: it is the only one the reader can act
    /// on, so they are told the ceiling instead of retrying a length that will be refused again.
    /// </remarks>
    private enum DesiredDataFailure
    {
        /// <summary>Nothing has gone wrong — what an untried, or a since retried, write reads as.</summary>
        None = 0,

        /// <summary>It threw, or the API refused it without naming a ceiling.</summary>
        Failed,

        /// <summary>The API refused it because too many requests arrived — HTTP 429.</summary>
        Throttled
    }

    /// <summary>What the alert says about the last annotation write, or null after none.</summary>
    private string? DesiredDataMessage => _desiredDataFailure switch
    {
        DesiredDataFailure.Throttled => T.RateLimitError,
        DesiredDataFailure.Failed => T.DesiredDataError,
        _ => null
    };

    /// <summary>
    /// The sentence naming the ceiling, or <see langword="null"/> while no row stands refused. Its own
    /// region, not the shared one a later failure would take over, leaving a field marked invalid with
    /// nothing saying why (WCAG 3.3.1). The field points at this region, so the two are read together.
    /// </summary>
    private string? DesiredDataRefusalMessage =>
        _desiredDataRefusal is { } refusal ? T.DesiredDataTooLong(refusal.MaxLength) : null;

    private IReadOnlyList<VariableList> Lists => State?.Lists ?? [];

    /// <summary>The heading's text, which through its id also names the table and its region: one
    /// name from one source. The reader's own word, since "Mine variabellister" would title every
    /// list the same; that is the fallback only before the lists arrive or when there are none.</summary>
    private string ShownListName =>
        ShownList?.Name is { Length: > 0 } name
            ? name
            : T.MyListsHeading;

    private VariableList? ShownList => Lists.FirstOrDefault(l => l.Id == _shownList);

    /// <summary>The kind above the name, as the other detail pages carry it. Keyed on a named list
    /// being shown rather than on the heading's words, since a reader may name a list exactly that.</summary>
    private string? ShownListEyebrow => ShownList?.Name is { Length: > 0 } ? T.MyListsHeading : null;

    /// <summary>
    /// The years a variable has data for, through the helper the result rows and detail panel use, so a
    /// variable reads alike here. Null for neither date: the cell writes "Ikke oppgitt" itself, as it
    /// does for every column the catalogue has no value for.
    /// </summary>
    private string? Period(VariableListItem item) =>
        CatalogueDate.Period(item.DataFrom, item.DataTo, Language, T, DateWidth.Narrow);

    /// <summary>
    /// The name column's text: the variable's name, or the sentence shown for one no longer in the
    /// catalogue. The remove button is named from the rendered cell rather than from a second call to
    /// this, so the two cannot drift.
    /// </summary>
    private string RowName(VariableListItem item) =>
        string.IsNullOrWhiteSpace(item.VariableName) ? T.VariableNoLongerAvailable : item.VariableName;

    /// <summary>
    /// The columns of one row, drawn by the helper the search results use. A fragment gets its own
    /// sequence-number region, so these numbers, spaced <see cref="RowCell.Slots"/> apart, stand clear
    /// of the surrounding markup's, as the helper's note about rising numbers requires.
    /// </summary>
    private RenderFragment Cells(VariableListItem item) => builder =>
    {
        // tableCell: real <td>s under <th scope="col">s take neither the explorer's per-cell field name
        // nor its flex column class. Trimmed rather than `??`: a missing kortnavn is null or "", and
        // RowCell would draw the "" as "Ikke oppgitt" over a name it is holding.
        if (Shown(ListColumn.Source))
        {
            RowCell.Write(builder, 200, T.FieldSource, DisplayText.Trimmed(item.KildeShortName) ?? item.KildeName, "source", T.NotSpecified, tooltip: item.KildeName, tableCell: true);
        }

        if (Shown(ListColumn.DataCollection))
        {
            // The component's words for an item whose datasamling is not chosen, so unmarked; its panel holds the picker.
            if (IsUnresolved(item))
            {
                RowCell.Write(builder, 300, T.FieldDataCollection, T.DatasamlingNotChosen, "dataCollection", T.NotSpecified, catalogue: false, tableCell: true);
            }
            else
            {
                RowCell.Write(builder, 300, T.FieldDataCollection, item.DatasamlingName, "dataCollection", T.NotSpecified, tableCell: true);
            }
        }

        if (Shown(ListColumn.VariableGroup))
        {
            RowCell.Write(builder, 400, T.FieldVariableGroup, item.VariabelgruppeName, "theme", T.NotSpecified, tableCell: true);
        }

        // Unmarked: the API resolves the name in the reader's language (Fhi.Metadata-13xf8).
        if (Shown(ListColumn.DataType))
        {
            RowCell.Write(builder, 500, T.FieldDataType, DataTypeName(item.DataType), "dataType", T.NotSpecified, catalogue: false, tableCell: true);
        }

        // The component's words rather than the catalogue's — the dates are formatted for the
        // reader — so it is left unmarked, exactly as the explorer leaves it.
        if (Shown(ListColumn.DataPeriod))
        {
            RowCell.Write(builder, 600, T.FieldDataPeriod, Period(item), "period", T.NotSpecified, catalogue: false, tableCell: true);
        }

        // A word rather than a mark, in the reader's language. Unknown is not "Nei": an entry the
        // read model lost, and every shared snapshot, carry no answer and say so.
        if (Shown(ListColumn.Kodeverk))
        {
            RowCell.Write(builder, 700, T.FieldKodeverk, Flag(item.HasKodeverk), "kodeverk", T.NotSpecified, catalogue: false, tableCell: true);
        }

        if (Shown(ListColumn.Statistics))
        {
            RowCell.Write(builder, 800, T.FieldStatistics, Flag(item.HasStatistics), "statistikk", T.NotSpecified, catalogue: false, tableCell: true);
        }
    };

    private string? Flag(bool? value) => value switch
    {
        true => T.FlagYes,
        false => T.FlagNo,
        null => null
    };

    /// <summary>
    /// The list on screen: its size and last change off <c>my/lists</c>, so neither can contradict
    /// the picker, and its kilde count off the membership walk — left out until that walk is done,
    /// since empty is how a refused one reads, and at zero, which "0 variabler" has already said.
    /// </summary>
    private string? ListMeta
    {
        get
        {
            if (Lists.FirstOrDefault(l => l.Id == _shownList) is not { } shown)
            {
                return null;
            }

            List<string> parts = [T.ListVariableCount(shown.VariableCount)];

            // The tally is the active list's, so it is only this list's while the two agree: a
            // switch clears it before it awaits, but a notification can still render between them.
            if (State is { KilderInListKnown: true, KilderInList.Count: > 0 } state
                && state.ActiveListId == _shownList)
            {
                parts.Add(T.ListKildeCount(state.KilderInList.Count));
            }

            if (CatalogueDate.DayOrNothing(shown.UpdatedAt, Language, DateWidth.Narrow) is { } day)
            {
                parts.Add(T.ListLastModified(day));
            }

            return string.Join(" · ", parts);
        }
    }

    /// <summary>What one list reads as in the picker: its name, then how many variables it holds.</summary>
    /// <remarks>
    /// helsedata's own overview says this for every list rather than only for the one being looked
    /// at, and an <c>&lt;option&gt;</c> holds text and nothing else, so the two are one string here.
    /// </remarks>
    private string ListOption(VariableList list) =>
        $"{list.Name} ({T.ListVariableCount(list.VariableCount)})";

    // The gestures RowPress calls a selection, less the drag it takes a press to tell. All three
    // controls ask it: a double-click used to shut the form its own first click opened, and on the
    // delete control to re-arm the confirmation it had just cancelled. (Fhi.Metadata-zel47)
    private void Toggle(MouseEventArgs released, ref bool open)
    {
        if (!RowPress.WasSelectionStandingStill(released))
        {
            open = !open;
            _readerMoves++;
        }
    }

    // The form opens under the row, where an open fold's panel would cover it.
    private void ToggleCreatingFromControl(MouseEventArgs released)
    {
        Toggle(released, ref _creating);
        CloseFolds();
    }

    private void ToggleRenamingFromControl(MouseEventArgs released) => Toggle(released, ref _renaming);

    private void ToggleConfirmingDeleteFromControl(MouseEventArgs released) =>
        Toggle(released, ref _confirmingDelete);

    /// <summary>Written the way <see cref="AriaDisabled"/> is, so the two toggles read alike.</summary>
    private static string Expanded(bool open) => open ? "true" : "false";

    /// <summary>The name cell of one row, which the row's remove button is named from.</summary>
    private string RowNameId(VariableListItem item) =>
        $"munin-explorer-list-name-{_instance}-{ItemSuffix(item)}";

    /// <summary>The remove button of one row, which names itself from its own words first.</summary>
    private string RemoveButtonId(VariableListItem item) =>
        $"munin-explorer-list-remove-{_instance}-{ItemSuffix(item)}";

    /// <summary>
    /// The remove button's name: its own word, then the row's, so forty "Fjern" buttons say which row
    /// (WCAG 4.1.2). Two elements keep each in its own language (3.1.2), the word first for speech
    /// input (2.5.3). An orphan borrows its cell's sentence; two alike pass, as 4.1.2 asks no uniqueness.
    /// </summary>
    private string RemoveLabelledBy(VariableListItem item) =>
        $"{RemoveButtonId(item)} {RowNameId(item)}";

    /// <summary>
    /// <c>"no"</c> for a value the catalogue wrote, which is Norwegian whatever the page's language,
    /// and nothing for our NotSpecified fallback: it is in the reader's language, and an English "Not
    /// specified" marked Norwegian is mispronounced by a screen reader (WCAG 3.1.2).
    /// </summary>
    private static string? CatalogueLang(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : "no";

    /// <summary>
    /// The API's own answer, not ours. The client already derives it when the envelope omits it
    /// (this endpoint's does), and deliberately leaves it alone when present — so recomputing here
    /// would put our arithmetic in front of the API's number on the day the two disagree.
    /// </summary>
    private int TotalPages => Math.Max(1, _page?.TotalPages ?? 1);

    /// <summary>Written the way the result list writes it, so the pager reads the same on both.</summary>
    private static string AriaDisabled(bool enabled) => enabled ? "false" : "true";

    /// <summary>
    /// What a page with no rows on it means: an empty list, or a narrowing nothing survived.
    /// "Denne listen er tom" over a list of 247 sends the reader looking for variables they still
    /// have. (Fhi.Metadata-mm4hu)
    /// </summary>
    private string EmptyMessage =>
        State?.KildeFilter.Count > 0 ? T.NoVariablesForTheseKilder : T.EmptyList;

    private bool EmptyRowsShown => _page is { Items.Count: 0 } && !_loading;

    // With a kilde ticked the sentence says none come from those kilder, which is not why the buttons are refused.
    private bool ListEmptySaidBelow => EmptyRowsShown && EmptyMessage == T.EmptyList;

    [CascadingParameter] internal ShowSearchTab? ShowSearch { get; set; }

    /// <summary>The one sentence the view's alert region says, the most recent failure first.</summary>
    private string? AlertText =>
        _failed ? T.ListLoadError
        : SharedListMessage ?? ShareMessage ?? CopyMessage ?? EmptyingMessage ?? DownloadMessage ?? ActionMessage
          ?? CreateMessage ?? DesiredDataMessage ?? NotesMessage ?? (State is { ListsReadFailed: true, Lists.Count: 0 } ? T.ListLoadError : null);

    /// <summary>The lists are still being read, so an empty list says nothing about the reader yet.</summary>
    private bool ListsPending =>
        !_failed && ((State is { IsReadingLists: true } && Lists.Count == 0) || (_retryingLists && _page is null));

    /// <summary>No list is shown because a read failed and nothing is reading now: the lists, the chosen
    /// list's membership, or its first page. The reader is offered a retry.</summary>
    private bool ListsFailureShown =>
        State is { IsReadingLists: false } && !_retryingLists && (_failed || State.ListsReadFailed)
        && _page is null && (Lists.Count > 0 || !State.HasLoaded);

    /// <summary>Reads the lists again for a reader who asked, the one retry a render never makes.</summary>
    /// <remarks>Inert while it reads, so the pressed button keeps focus; once the list is shown the
    /// button goes and focus moves to the list, or to "Legg til ny liste" when there is none.</remarks>
    private async Task RetryListsAsync()
    {
        if (State is null || !ListsFailureShown)
        {
            return;
        }

        _retryingLists = true;
        _failed = false;

        try
        {
            // A refused refresh leaves the old lists in place, which EnsureActiveListAsync would take as read.
            if (State.ListsReadFailed)
            {
                await State.RefreshAsync();
            }

            await State.EnsureActiveListAsync(readerAsked: true);
            await ShowActiveListAsync();
        }
        catch (Exception ex)
        {
            if (ex is MuninExplorerRateLimitedException or MuninExplorerUnauthorizedException)
            {
                Log?.LogWarning(ex, "the API refused the reader's lists on retry");
            }
            else
            {
                Log?.LogError(ex, "could not read the reader's lists on retry");
            }

            _page = null;
            _failed = true;
        }
        finally
        {
            // The button leaves with its offer; the focus it held must go somewhere, not to <body>.
            _retryingLists = false;
            _focusAfterRetry = !ListsFailureShown;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // All taken, so a flag none acts on does not linger to move focus on a later render.
        var fromSource = TakeSourceFocusTarget();
        var fromMenu = TakeMenuFocusTarget();
        var fromWrite = TakeFocusTarget();

        if ((fromSource ?? fromMenu ?? fromWrite) is not { } target)
        {
            return;
        }

        try
        {
            await target.FocusAsync();
        }
        catch (Exception ex)
        {
            // A focus nicety must not take the circuit down with it.
            Log?.LogWarning(ex, "could not move focus after the reader's action");
        }
    }

    /// <summary>Where focus goes once the control that held it has left, or null to leave it be.</summary>
    private ElementReference? TakeFocusTarget()
    {
        var afterCreate = _focusAfterCreate;
        var afterRename = _focusAfterRename;
        var afterRetry = _focusAfterRetry;
        _focusAfterCreate = _focusAfterRename = _focusAfterRetry = false;

        if ((!afterCreate && !afterRename && !afterRetry) || ShowsSharedList || State?.IsAuthenticated != true)
        {
            return null;
        }

        // Never while the reader has moved on to a form of their own; for a create or rename, only forms
        // opened during that call count, as the two can overlap. A rename counts fold moves too (a shut
        // fold leaves no open state); a create does not: it closes the folds and may draw no «Last ned».
        var since = afterCreate ? _formsOpenAtCreate : afterRename ? _formsOpenAtRename : 0;
        var movesSince = afterRename ? _movesAtRename : _readerMoves;
        var movedOn = (OpenForms() & ~since) != 0 || _readerMoves != movesSince;

        if (movedOn)
        {
            return null;
        }

        if (afterCreate)
        {
            return Lists.Count > 1 ? _listPicker : _createToggle;
        }

        if (afterRename)
        {
            return _shownList is not null ? _menuToggle : null;
        }

        return _page is not null && (_page.Items.Count > 0 || _loading) ? _listRegion : _createToggle;
    }

    private int OpenForms() =>
        (_creating ? 1 : 0) | (_renaming ? 2 : 0) | (_openingShared ? 4 : 0) | (_copying ? 8 : 0) | (_confirmingDelete ? 16 : 0) | (_sharingList ? 32 : 0)
        | (_menuOpen ? 64 : 0) | (_downloadOpen ? 128 : 0);

    /// <summary>A read has answered, so an empty list means the reader has none rather than that we never found out.</summary>
    private bool ListsAnswered => !_failed && State is { HasLoaded: true };

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

        // Caught: an exception out of a lifecycle method takes the circuit, in helsedata's legacy host the
        // whole CMS page. The mount fires this read with the search and facet refresh, the burst the
        // limiter counts, so a 429 is ordinary. Said on screen, since silence would pass for an empty list.
        try
        {
            // Mounting is opening the tab, a reader asking; a later parameter set is only a render.
            // Spent before the await, so a refusal does not leave every later render asking too.
            var asking = !_askedOnMount;
            _askedOnMount = true;
            await State.EnsureActiveListAsync(readerAsked: asking);
            await ShowActiveListAsync();
        }
        catch (Exception ex)
        {
            // Split the way the comment above says it has to be: the burst this read is part of is
            // what the limiter counts, and a 401 is the host's own token, so both are expected
            // outcomes the reader is told about and neither is a fault to go and find.
            if (ex is MuninExplorerRateLimitedException or MuninExplorerUnauthorizedException)
            {
                Log?.LogWarning(ex, "the API refused the reader's lists on mount");
            }
            else
            {
                Log?.LogError(ex, "could not read the reader's lists on mount");
            }

            _page = null;
            _failed = true;
        }

        await FollowShareCodeAsync();
        await LoadDataTypeNamesAsync();
    }

    /// <summary>
    /// The datatype display names, read once per mount and with no search or filter: a scoped read omits
    /// codes the search matched none of, and saved rows carry codes from any search. Failure leaves the
    /// map empty and falls back to the code, rather than losing the whole view over a label.
    /// </summary>
    private async Task LoadDataTypeNamesAsync()
    {
        // Nothing is asked for a reader who is not signed in. This view renders nothing for them, and
        // a call whose answer nobody sees is still a call the limiter counts - the same reason the
        // list itself is not read either.
        if (!IsAuthenticated && _openCode is null)
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
        catch (Exception ex)
        {
            // Warning, not Error: the reader sees the raw codes rather than nothing. Recorded as
            // attempted for this language, so a failing endpoint is asked once rather than on
            // every parameter change.
            Log?.LogWarning(ex, "could not load the datatype names for {Language}", Language);

            _dataTypeNames = new Dictionary<string, string>(StringComparer.Ordinal);
            _dataTypeNamesLanguage = Language;
        }
    }

    /// <summary>
    /// The readable name for a datatype code. A shared snapshot keeps whatever spelling was stored, so
    /// the value is canonicalised before the lookup and the code is the fallback. AGENTS.md, "The API
    /// names a datatype, not this package".
    /// </summary>
    private string? DataTypeName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return code;
        }

        var canonical = T.CanonicalDataTypeCode(code);
        return _dataTypeNames is not null && _dataTypeNames.TryGetValue(canonical, out var name)
            ? name
            : canonical;
    }


    /// <summary>
    /// Another surface changed a list. Re-read the page rather than only re-render: the rows come from
    /// <c>_page</c>, which the holder does not own, so a variable a save button removed would stay here.
    /// </summary>
    private void OnStateChanged(VariableListState.ListChange? change)
    {
        InvokeAsync(async () =>
        {
            // A narrowing shortens the list, so the page number has to go back to the start: a
            // reader on page 4 of ten who ticks a kilde with two pages would otherwise be handed
            // an empty page and no sign of why. Every other change leaves them where they stood.
            if (State is { KildeFilterVersion: var version } && version != _seenKildeFilter)
            {
                _seenKildeFilter = version;
                _pageNumber = 1;
            }

            // A fresh read of the lists supersedes this view's last failure; its own outcome is told when it lands.
            if (State is { IsReadingLists: true })
            {
                _failed = false;
            }

            // A retry reads the page itself once its lists are in; reloading here too would ask twice.
            if (ShouldReloadFor(change) && !_retryingLists && !_holdingReloads)
            {
                await LoadPageAsync();
            }

            StateHasChanged();
        });
    }

    /// <summary>Whether this notification could have changed the rows on screen, identified by the
    /// list it names rather than counted — a count an unrelated notification could also spend
    /// (Fhi.Metadata-wuxkn).</summary>
    private bool ShouldReloadFor(VariableListState.ListChange? change) =>
        change is not { } known || (known.AffectsRows && (known.ListId is null || known.ListId == _shownList));

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

            // The open row went with its list, and with it anything opened from that row.
            CloseRow();

            // Cleared here as well now that a superseded read returns without touching it: this
            // branch is the one place a read in flight can be abandoned by a caller.
            _loading = false;
            SeedDesiredData();
            return;
        }

        // What this read is for, taken before the await: several run at once, since the holder raises
        // Changed mid-create and each notification starts one. The narrowing is included because a tick
        // raises Changed too, so an answer can land for kilder the reader has already unticked.
        var readList = _shownList.Value;
        var readPage = _pageNumber;
        var readKilder = State.KildeFilter;
        var readFilter = State.KildeFilterVersion;

        _loading = true;
        _failed = false;

        Page<VariableListItem>? read = null;
        var failed = false;

        try
        {
            // Narrowed by the API, not here: the endpoint pages, so a sieve over the page it
            // answered with would leave TotalCount — and the pager on it — describing the whole
            // list. The ticks live in the holder; the boxes are in the other grid column.
            read = await Client.GetMyListVariablesAsync(readList, readPage, PageSize, readKilder);
        }
        catch (Exception ex)
        {
            // Said here rather than thrown on: an unhandled exception out of a lifecycle method
            // takes the circuit down, which is a worse answer than a line of text. A page turn is
            // one of the calls the limiter counts, so the level splits the way every other does.
            if (ex is MuninExplorerRateLimitedException or MuninExplorerUnauthorizedException)
            {
                Log?.LogWarning(
                    ex, "the API refused page {Page} of list {ListId}", readPage, readList);
            }
            else
            {
                Log?.LogError(
                    ex, "could not read page {Page} of list {ListId}", readPage, readList);
            }

            failed = true;
        }

        // An answer for a list, page or narrowing the view has since left is dropped, _loading included.
        // Create repoints _shownList while reads for the old list are out, so a late answer draws old rows
        // under the new name; a tick races the same way, drawing rows for kilder no longer ticked.
        if (_shownList != readList || _pageNumber != readPage || State.KildeFilterVersion != readFilter)
        {
            return;
        }

        _loading = false;
        _failed = failed;
        _page = failed ? null : read;

        // Removed elsewhere, or paged away from: an open panel must not come back with stale detail.
        if (_openId is { } open && _page?.Items.Any(item => VariableDatasamlingKey.Of(item) == open) != true)
        {
            CloseRow();
        }

        SeedDesiredData();
    }

    /// <summary>
    /// Fills the annotation fields from the API's answer, emptied first so a draft cannot be sent later
    /// to a list it was not typed into. The refused row's text outlives every reload, as nothing else
    /// holds it while the notice is up; the mark goes when the row leaves the list or the page.
    /// </summary>
    private void SeedDesiredData()
    {
        _desiredDataSeeds++;
        ForgetSavedNotes();

        if (_desiredDataRefusal is { } stale && stale.ListId != _shownList)
        {
            _desiredDataRefusal = null;
        }

        var refused = _desiredDataRefusal;
        var refusedText = refused is not null && _desiredData.TryGetValue(refused.VariableId, out var typed)
            ? typed
            : null;

        _desiredData.Clear();

        if (_page is null)
        {
            // No page is no answer about the rows, so the refused draft is held rather than
            // dropped: a list read that fails or is skipped must not empty the field under a
            // reader who has just been told to shorten it.
            if (refused is not null && refusedText is not null)
            {
                _desiredData[refused.VariableId] = refusedText;
            }

            return;
        }

        foreach (var item in _page.Items)
        {
            _desiredData[VariableDatasamlingKey.Of(item)] = item.DesiredDataFreeText ?? "";
        }

        if (refused is null)
        {
            return;
        }

        if (!_desiredData.ContainsKey(refused.VariableId))
        {
            _desiredDataRefusal = null;
            return;
        }

        if (refusedText is not null)
        {
            _desiredData[refused.VariableId] = refusedText;
        }
    }

    /// <summary>What the annotation field for one row shows.</summary>
    private string DesiredDataOf(VariableListItem item) =>
        _desiredData.TryGetValue(VariableDatasamlingKey.Of(item), out var text) ? text : item.DesiredDataFreeText ?? "";

    /// <summary>
    /// <c>"true"</c> for the one row the API last refused, and nothing at all for the rest.
    /// </summary>
    /// <remarks>
    /// A null leaves the attribute off. <c>aria-invalid="false"</c> on every other field is not
    /// wrong, but it is announced on some readers, so forty rows would say "valid" forty times.
    /// </remarks>
    private string? DesiredDataInvalid(VariableListItem item) =>
        _desiredDataRefusal?.VariableId == VariableDatasamlingKey.Of(item) ? "true" : null;

    /// <summary>
    /// The refusal sentence's id for the one refused field, so <c>aria-invalid</c> comes with its reason
    /// (WCAG 3.3.1). Absent elsewhere, or every field would point at a region describing another row's
    /// text.
    /// </summary>
    private string? DesiredDataDescribedBy(VariableListItem item) =>
        _desiredDataRefusal?.VariableId == VariableDatasamlingKey.Of(item) ? DesiredDataRefusalId : null;

    /// <summary>
    /// Writes one row's annotation, or clears it, and says so when the API will not have it. The text is
    /// kept before the call and left alone after a refusal, so the reader reads why their words were not
    /// saved while still looking at them; an emptied field is the one thing they cannot recover from.
    /// </summary>
    private async Task SaveDesiredDataAsync(VariableListItem item, string? text)
    {
        var variableId = VariableDatasamlingKey.Of(item);

        if (_shownList is null)
        {
            return;
        }

        // Trimmed once, then both shown and sent: the API trims before it measures. Lines are joined,
        // from either textarea: the API keeps one line, and the column would run them together.
        var list = _shownList.Value;
        var trimmed = string.Join(", ", (text ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        _desiredData[variableId] = trimmed;

        // Numbered per row rather than once for the component: blur saves a row, so a reader typing
        // down the list has several writes out at once and each row's answer is still about it.
        var sequence = _desiredDataWrites.GetValueOrDefault(variableId) + 1;
        _desiredDataWrites[variableId] = sequence;
        var seeded = _desiredDataSeeds;

        ForgetFailures();

        if (_desiredDataRefusal?.VariableId == variableId)
        {
            // This row is being tried again, so the older refusal is about a text nobody is
            // looking at. Another row's refusal stands: its text is still on screen, still unsaved.
            _desiredDataRefusal = null;
        }

        var failure = DesiredDataFailure.None;
        int? ceiling = null;

        try
        {
            // By item where the API names one: the variable's own route refuses once it is in the list twice.
            var result = item.ItemId is { } itemId
                ? await Client.SetMyListItemDesiredDataAsync(list, itemId, trimmed)
                : await Client.SetMyListDesiredDataAsync(list, item.VariableId, trimmed);

            switch (result)
            {
                case { Outcome: DesiredDataOutcome.Saved }:
                    break;

                case { Outcome: DesiredDataOutcome.Refused, MaxLength: { } maxLength }:
                    // The only path that marks the field itself: aria-invalid is a claim about the
                    // text, and a throttled or failed write never had its text looked at.
                    ceiling = maxLength;
                    break;

                default:
                    // A refusal that named no ceiling, or a list the API says is not the reader's
                    // — both leave the annotation unwritten, and neither is something the reader
                    // can be told to shorten.
                    failure = DesiredDataFailure.Failed;
                    break;
            }
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            // Typing down a list saves one row after another, which is exactly the rhythm the
            // per-address limiter counts — so a throttled annotation is ordinary rather than rare.
            Log?.LogWarning(
                ex,
                "the rate limiter refused the annotation of variable {VariableId} in list {ListId}",
                item.VariableId,
                list);

            failure = DesiredDataFailure.Throttled;
        }
        catch (MuninExplorerUnauthorizedException ex)
        {
            // As above: the caller was declined, which is the host's token rather than a fault.
            Log?.LogWarning(
                ex,
                "the API refused the annotation of variable {VariableId} in list {ListId} as unauthorised",
                item.VariableId,
                list);

            failure = DesiredDataFailure.Failed;
        }
        catch (Exception ex)
        {
            // Uncaught, this leaves the event handler and takes the circuit with it: a blank page
            // and a reconnect banner in place of the note the reader was writing. The annotation
            // itself is the reader's own text and stays out of the log.
            Log?.LogError(
                ex,
                "could not write the annotation of variable {VariableId} in list {ListId}",
                item.VariableId,
                list);

            failure = DesiredDataFailure.Failed;
        }

        if (_shownList != list || _desiredDataWrites.GetValueOrDefault(variableId) != sequence)
        {
            // The write stands against the list and row it named, but the mark and the sentence are
            // keyed by row alone: a reader who has switched lists, or written this row again since,
            // would have this older answer land on a text nobody is looking at.
            return;
        }

        if (ceiling is { } maxLengthToSay)
        {
            // Not over a page read that landed under the write: that reseeds every field from the
            // API, so the refused text is gone and the mark would sit on the value the server
            // holds — telling the reader to shorten a text that is no longer on screen.
            if (_desiredDataSeeds == seeded)
            {
                _desiredDataRefusal = new DesiredDataRefusal(list, variableId, maxLengthToSay);
            }

            return;
        }

        if (failure is not DesiredDataFailure.None)
        {
            // A write that succeeded says nothing: the region is shared, and it was cleared for
            // this row before the call — so assigning None here would take away the sentence
            // another row's failure put there while this one was in flight.
            _desiredDataFailure = failure;
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
        _shownListMoves++;
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
        _sharingList = false;
        _shareMade = null;

        // Folded, not merely emptied: an open rename field over a list the reader did not open it
        // for reads as a rename under way. No caller here has focus inside it.
        _renaming = false;
        ForgetCopyAndEmptyControls();
        CloseRow();
        _menuOpen = false;
        _downloadOpen = false;
    }

    /// <summary>
    /// Empties the alert region — the one place that does. Five conditions share it, so a handler clearing
    /// only its own leaves an older one answering for what the reader just did. A refused annotation has
    /// its own region and stays: its text is still too long and still unsaved.
    /// </summary>
    private void ForgetFailures()
    {
        _failed = false;
        _createFailure = ListActionFailure.None;
        _actionFailure = ListActionFailure.None;
        _downloadFailure = DownloadFailure.None;
        _desiredDataFailure = DesiredDataFailure.None;
        _notesFailure = DesiredDataFailure.None;
        ForgetSharingFailures();
        ForgetCopyAndEmptyFailures();
    }

    private async Task ChooseListAsync(ChangeEventArgs e)
    {
        if (State is null || !Guid.TryParse(e.Value?.ToString(), out var id))
        {
            return;
        }

        _shownList = id;
        _shownListMoves++;
        _pageNumber = 1;
        ForgetListControls();
        ForgetFailures();

        try
        {
            await State.SetActiveListAsync(id);
        }
        catch (Exception ex)
        {
            // Same reason as the lifecycle read above: an uncaught throw out of an event handler
            // takes the circuit with it. LoadPageAsync below has its own catch and will say so.
            if (ex is MuninExplorerRateLimitedException or MuninExplorerUnauthorizedException)
            {
                Log?.LogWarning(ex, "the API refused the switch to list {ListId}", id);
            }
            else
            {
                Log?.LogError(ex, "could not switch to list {ListId}", id);
            }

            _failed = true;

            // And the rows go with it. _shownList has already moved, so rows left on screen from
            // the list before it are rows every write here would address to the list now chosen —
            // an annotation typed into one would land on that list's own row for the variable.
            _page = null;
            SeedDesiredData();

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
        _formsOpenAtCreate = OpenForms();

        VariableList? created;

        // The two notifications this raises - one from State.CreateAsync, one from the membership
        // walk inside SetActiveListAsync - both name the new list, never _shownList, so
        // ShouldReloadFor skips them on that identity and nothing here has to account for them.
        try
        {
            created = await State.CreateAsync(name);
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            // Creating meets the same limiter the saves do, and "prøv igjen om litt" is advice
            // a throttled reader cannot use.
            Log?.LogWarning(ex, "the rate limiter refused a list creation");
            _createFailure = ListActionFailure.Throttled;
            return;
        }
        catch (MuninExplorerUnauthorizedException ex)
        {
            // The API's own answer to a host that says the reader is signed in, which is a token
            // to go and fix rather than a fault here — the same reading the save button gives it.
            // The reader is told what every other failure tells them, since there is no more.
            Log?.LogWarning(ex, "the API refused a list creation as unauthorised");
            _createFailure = ListActionFailure.Failed;
            return;
        }
        catch (Exception ex)
        {
            // Uncaught, this leaves the event handler and takes the circuit with it: a blank
            // page and a reconnect banner in place of the list the reader was building. The name
            // the reader typed stays out of the log.
            Log?.LogError(ex, "could not create a list");
            _createFailure = ListActionFailure.Failed;
            return;
        }

        if (created is null)
        {
            // Signed out mid-call.
            return;
        }

        _newName = "";
        ForgetListControls();

        try
        {
            await State.SetActiveListAsync(created.Id);
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            // The list was made and the switch met the limiter. Told apart from the ordinary
            // failure for the reason the create half above gives: the remedy is to wait.
            Log?.LogWarning(
                ex,
                "the rate limiter refused the switch to the new list {ListId}",
                created.Id);
            _createFailure = ListActionFailure.Throttled;
            return;
        }
        catch (MuninExplorerUnauthorizedException ex)
        {
            // Expected in the same way the creation's own 401 is, and told apart from a fault for
            // the same reason. The list was made either way, so the reader sees ListLoadError.
            Log?.LogWarning(
                ex, "the API refused the switch to the new list {ListId} as unauthorised", created.Id);
            _failed = true;
            return;
        }
        catch (Exception ex)
        {
            // Same reason as ChooseListAsync above. The list was created; it is the switch to
            // it that did not happen, which is what ListLoadError says.
            Log?.LogError(ex, "could not switch to the new list {ListId}", created.Id);
            _failed = true;
            return;
        }

        _shownList = created.Id;
        _shownListMoves++;
        _pageNumber = 1;
        await LoadPageAsync();

        // Folded once the list exists, as Skuld has no standing form; focus goes to what names it.
        _creating = false;
        _focusAfterCreate = true;
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
        _formsOpenAtRename = OpenForms();
        _movesAtRename = _readerMoves;

        // Renaming never reads the page again: its own notification names _shownList but carries
        // AffectsRows: false, so ShouldReloadFor skips it without anything armed here for it.
        try
        {
            if (await State.RenameAsync(_shownList.Value, name))
            {
                _renameName = "";
                _renaming = false;
                _focusAfterRename = true;
            }
            else
            {
                _actionFailure = ListActionFailure.Failed;
            }
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            // These writes go through the client every read on the page uses, and meet the same
            // per-address limiter, so a refusal here is ordinary rather than rare.
            Log?.LogWarning(
                ex, "the rate limiter refused the rename of list {ListId}", _shownList);

            _actionFailure = ListActionFailure.Throttled;
        }
        catch (MuninExplorerUnauthorizedException ex)
        {
            // A write the API declined to accept the caller for, not a write that broke.
            Log?.LogWarning(
                ex, "the API refused the rename of list {ListId} as unauthorised", _shownList);

            _actionFailure = ListActionFailure.Failed;
        }
        catch (Exception ex)
        {
            // An uncaught throw out of an event handler takes the whole circuit down, which is a
            // far worse answer to a failed rename than a line of text. The name the reader typed
            // stays out of the log.
            Log?.LogError(ex, "could not rename list {ListId}", _shownList);

            _actionFailure = ListActionFailure.Failed;
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
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(
                ex, "the rate limiter refused the deletion of list {ListId}", _shownList);

            _actionFailure = ListActionFailure.Throttled;
        }
        catch (MuninExplorerUnauthorizedException ex)
        {
            // As above: the caller was declined, which is the host's token rather than a fault.
            Log?.LogWarning(
                ex, "the API refused the deletion of list {ListId} as unauthorised", _shownList);

            _actionFailure = ListActionFailure.Failed;
        }
        catch (Exception ex)
        {
            // Caught for the reason the rename above gives. The list may well be gone on the
            // server, so the view is repointed below whichever of the two calls threw.
            Log?.LogError(ex, "could not delete list {ListId}", _shownList);

            _actionFailure = ListActionFailure.Failed;
        }

        await ShowActiveListAsync();
    }

    private async Task RemoveAsync(VariableListItem item)
    {
        var variableId = item.VariableId;
        var key = VariableDatasamlingKey.Of(item);

        if (State is null || _shownList is null)
        {
            return;
        }

        ForgetFailures();

        var list = _shownList.Value;

        try
        {
            // The holder raises Changed, and OnStateChanged re-reads the page — so no fetch here.
            // By item from an API that keys items by datasamling, so the variable's other items stay.
            var removed = item.ItemId is null
                ? await State.RemoveVariablesAsync(list, [variableId])
                : await State.RemoveItemsAsync(list, [key]);

            if (removed)
            {
                // Only the row it was removed from: the same variable may be open in another list by now.
                if (_openId == key && _openListId == list)
                {
                    CloseRow();
                }

                ForgetNotesFor(list, key);

                await RetreatFromEmptyPageAsync();
            }
            else
            {
                // Unlike rename and delete, the holder runs no staleness guard on this path, so a
                // false is never a call that merely arrived late: it is a list the API will not
                // write to, or a reader signed out under the press.
                _actionFailure = ListActionFailure.Failed;
            }
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            // Removing is one of the writes the limiter counts, and "prøv igjen om litt" is
            // advice a throttled reader cannot use.
            Log?.LogWarning(
                ex,
                "the rate limiter refused the removal of variable {VariableId} from list {ListId}",
                variableId,
                _shownList);

            _actionFailure = ListActionFailure.Throttled;
        }
        catch (MuninExplorerUnauthorizedException ex)
        {
            // As above: the caller was declined, which is the host's token rather than a fault.
            Log?.LogWarning(
                ex,
                "the API refused the removal of variable {VariableId} from list {ListId} as unauthorised",
                variableId,
                _shownList);

            _actionFailure = ListActionFailure.Failed;
        }
        catch (Exception ex)
        {
            // Uncaught, this leaves the event handler and takes the circuit with it: a blank
            // page and a reconnect banner in place of the row the reader wanted gone.
            Log?.LogError(
                ex,
                "could not remove variable {VariableId} from list {ListId}",
                variableId,
                _shownList);

            _actionFailure = ListActionFailure.Failed;
        }
    }

    /// <summary>
    /// Steps back when the page being looked at no longer exists. Emptying the last page (page three,
    /// say) puts the empty state in place of the pager, so the reader is told the list is empty and has
    /// no control left to reach the pages that still have rows.
    /// </summary>
    private async Task RetreatFromEmptyPageAsync()
    {
        while (_pageNumber > 1 && _page is not null && _page.Items.Count == 0)
        {
            _pageNumber--;
            await LoadPageAsync();
        }
    }

    /// <summary>
    /// Fetches the whole list from the API and hands it to the browser. Every id, not the page on screen:
    /// a download quietly holding only the rows in view would go unnoticed until the file was opened.
    /// </summary>
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
            // The list itself rather than its ids, so the file carries each row's "Ønskede data" (Fhi.Metadata-fiht4).
            // Narrowed by the same kilder as the table, so it holds what the reader was looking at.
            var file = await Client.ExportMyListAsync(_shownList.Value, format, _includeKodeverk, State?.KildeFilter);

            if (file is null)
            {
                _downloadFailure = DownloadFailure.Failed;
                return;
            }

            await BrowserDownload.OfferAsync(Js, file);
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            // The export sits under the browse policy, not the write one the saves use, keyed per user
            // since the view only renders signed in. The generic sentence names no cause; this one does.
            Log?.LogWarning(
                ex,
                "the rate limiter refused the {Format} export of list {ListId}",
                format,
                _shownList);

            _downloadFailure = DownloadFailure.Throttled;
        }
        catch (MuninExplorerUnauthorizedException ex)
        {
            // The export reads the reader's own list, so a declined caller lands here rather than
            // on a broken download.
            Log?.LogWarning(
                ex,
                "the API refused the {Format} export of list {ListId} as unauthorised",
                format,
                _shownList);

            _downloadFailure = DownloadFailure.Failed;
        }
        catch (Exception ex)
        {
            // Includes the browser refusing the blob — a Content-Security-Policy without blob:
            // would land here. Said out loud rather than left as a button that does nothing.
            Log?.LogError(
                ex, "could not export list {ListId} as {Format}", _shownList, format);

            _downloadFailure = DownloadFailure.Failed;
        }
        finally
        {
            _downloading = false;
        }
    }

    private enum SaveNameProblem
    {
        None = 0,
        Required,
        TooLong,
        Taken
    }

    /// <summary>The API's own ceiling on a list name.</summary>
    private const int MaxListNameLength = 200;

    /// <summary>
    /// Why a name cannot be given to a new list, or <see cref="SaveNameProblem.None"/>: empty, over
    /// the API's ceiling, or one the reader already uses — compared trimmed and case-insensitively.
    /// </summary>
    /// <remarks>The lists are read afresh, since another tab may have made one since.</remarks>
    private async Task<SaveNameProblem> NameProblemAsync(string trimmed)
    {
        if (trimmed.Length == 0)
        {
            return SaveNameProblem.Required;
        }

        if (trimmed.Length > MaxListNameLength)
        {
            return SaveNameProblem.TooLong;
        }

        var existing = await Client.GetMyListsAsync();

        return existing.Any(l => string.Equals(l.Name.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
            ? SaveNameProblem.Taken
            : SaveNameProblem.None;
    }

    /// <summary>What the field under a refused name says, for saving a shared list and for a copy.</summary>
    private string? NameProblemMessage(SaveNameProblem problem) => problem switch
    {
        SaveNameProblem.Required => T.ListNameRequired,
        SaveNameProblem.TooLong => T.ListNameTooLong,
        SaveNameProblem.Taken => T.ListNameTaken,
        _ => null
    };

    /// <summary>Every item in the list, unnarrowed, 1000 at a time; null when the list is gone.</summary>
    private async Task<List<VariableListItem>?> ReadWholeListAsync(Guid list)
    {
        var seen = new HashSet<VariableDatasamlingKey>();
        var items = new List<VariableListItem>();
        var page = 1;

        while (true)
        {
            var slice = await Client.GetMyListVariablesAsync(list, page, 1000);

            if (slice is null)
            {
                return null;
            }

            if (slice.Items.Count == 0)
            {
                break;
            }

            items.AddRange(slice.Items.Where(item => seen.Add(VariableDatasamlingKey.Of(item))));

            if (items.Count >= slice.TotalCount)
            {
                break;
            }

            page++;
        }

        return items;
    }

    public void Dispose()
    {
        if (_state is not null)
        {
            _state.Changed -= OnStateChanged;
        }
    }
}
