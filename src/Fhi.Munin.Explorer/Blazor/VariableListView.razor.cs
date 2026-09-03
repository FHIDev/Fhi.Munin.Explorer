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
/// A separate root component rather than a tab inside <see cref="VariableSearch"/>, because the
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

    /// <inheritdoc cref="VariableSearch.Language"/>
    [Parameter] public string Language { get; set; } = "no";

    /// <inheritdoc cref="VariableSearch.IsAuthenticated"/>
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
    /// explorer's own ids use — see <c>VariableSearch.razor.cs</c>, where the convention lives.
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

    /// <summary>The sentence a refused annotation is explained by, which its field points at.</summary>
    private string DesiredDataRefusalId => $"munin-explorer-list-desired-data-refusal-{_instance}";

    /// <summary>The word "Ønskede data" in one row, which that row's field is named from.</summary>
    private string DesiredDataLabelId(VariableListItem item) =>
        $"munin-explorer-list-desired-data-label-{_instance}-{item.VariableId:N}";

    /// <summary>
    /// The annotation field's accessible name, as two elements: the column's word, then the row's
    /// name.
    /// </summary>
    /// <remarks>
    /// The rule the remove button beside it follows, and for the same reasons: forty fields all
    /// announcing "Ønskede data" say nothing about which variable the reader is annotating (WCAG
    /// 4.1.2), and one <c>aria-label</c> would hand our word and Munin's Norwegian name to a single
    /// voice (WCAG 3.1.2). The column's word first, so the field opens with what it is.
    /// </remarks>
    private string DesiredDataLabelledBy(VariableListItem item) =>
        $"{DesiredDataLabelId(item)} {RowNameId(item)}";

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

    /// <summary>
    /// What the reader has typed against each variable, keyed by variable id.
    /// </summary>
    /// <remarks>
    /// The fields render from here rather than from <c>_page</c>, because a refusal must not revert
    /// the text under the reader: re-rendering the item's stored value would empty the field they
    /// have just been told is too long, and they would have to type all 500-odd characters again.
    /// Reseeded from the API on every page read, which is the only thing that knows what was
    /// actually saved.
    /// </remarks>
    private readonly Dictionary<Guid, string> _desiredData = [];

    private DesiredDataFailure _desiredDataFailure;

    /// <summary>How many writes each row has had, so an older answer can be told from the newest.</summary>
    /// <remarks>
    /// Blur is what saves, so two writes to one row overlap whenever a reader corrects a note and
    /// leaves before the first answer is back. Ordered by arrival, the first answer wins and marks
    /// a text that was accepted — with nothing after it to take the mark away again.
    /// </remarks>
    private readonly Dictionary<Guid, int> _desiredDataWrites = [];

    /// <summary>How many times the fields have been seeded from a page read.</summary>
    /// <remarks>
    /// A read landing under a write reseeds every field from the API, which drops the draft — so an
    /// answer from before it is one about a text the reader can no longer see.
    /// </remarks>
    private int _desiredDataSeeds;

    /// <summary>The row the API refused for length, the list it was refused in, and the ceiling.</summary>
    /// <remarks>
    /// Held apart from the failures the alert region shares, and outside what
    /// <see cref="ForgetFailures"/> drops: this one is a claim about a text still on screen and
    /// still unsaved, so it stands until that row is written again or leaves the list. The list is
    /// carried with it because a variable can sit in two of them, and a mark left over from one
    /// would land on the same row in the other.
    /// </remarks>
    private DesiredDataRefusal? _desiredDataRefusal;

    /// <summary>One refused row: which list it is in, which row, and the ceiling to shorten to.</summary>
    private sealed record DesiredDataRefusal(Guid ListId, Guid VariableId, int MaxLength);

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

    /// <summary>How the last annotation write ended, when it ended badly.</summary>
    /// <remarks>
    /// A length refusal is its own state rather than a failure like the others, because it is the
    /// only one the reader can act on: the API names the ceiling and the sentence repeats it, so
    /// they are told what to shorten to instead of being asked to try again at a length that will
    /// be refused identically.
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
    /// The sentence naming the ceiling, or <see langword="null"/> while no row stands refused.
    /// </summary>
    /// <remarks>
    /// Its own region rather than a sixth claimant on the shared one: a refused row keeps its
    /// <c>aria-invalid</c> until it is written again, and a download or a rename failing meanwhile
    /// would take the sentence away and leave a field marked wrong with nothing saying why —
    /// WCAG 3.3.1. The field points at this region, so the two are read together.
    /// </remarks>
    private string? DesiredDataRefusalMessage =>
        _desiredDataRefusal is { } refusal ? T.DesiredDataTooLong(refusal.MaxLength) : null;

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

        // Caught here, like the save button's own read in VariableSearch.Lists.cs:38. An exception
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
    /// The same shape as <c>VariableSearch.DataTypeName</c>, and for the same reason: the codes are
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
        // Mounted beside the explorer, both surfaces reach EnsureActiveListAsync while the lists
        // read is still out, and the loser finds no list to show. Re-reading the page would go on
        // reading nothing, so a list that has appeared since is shown (Fhi.Metadata-l9l2n.39).
        if (State?.ActiveListId != _shownList)
        {
            _skipOnePageRead = false;
            await ShowActiveListAsync();
        }
        else if (_skipOnePageRead)
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
            SeedDesiredData();
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

        SeedDesiredData();
    }

    /// <summary>
    /// Fills the annotation fields from what the API just answered with.
    /// </summary>
    /// <remarks>
    /// Emptied first, so a draft against a row that has left the page cannot be sent back later
    /// against a list it was never typed into. The refused row is the exception: its text outlives
    /// every reload, including the one with no page behind it, because the notice to shorten it is
    /// still on screen and nowhere else holds those 500-odd characters. The mark goes when the row
    /// does — off the list, or off the page — since neither leaves a field to mark.
    /// </remarks>
    private void SeedDesiredData()
    {
        _desiredDataSeeds++;

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
            _desiredData[item.VariableId] = item.DesiredDataFreeText ?? "";
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
        _desiredData.TryGetValue(item.VariableId, out var text) ? text : item.DesiredDataFreeText ?? "";

    /// <summary>
    /// <c>"true"</c> for the one row the API last refused, and nothing at all for the rest.
    /// </summary>
    /// <remarks>
    /// A null leaves the attribute off. <c>aria-invalid="false"</c> on every other field is not
    /// wrong, but it is announced on some readers, so forty rows would say "valid" forty times.
    /// </remarks>
    private string? DesiredDataInvalid(VariableListItem item) =>
        _desiredDataRefusal?.VariableId == item.VariableId ? "true" : null;

    /// <summary>
    /// The refusal sentence's id for the one refused field, and nothing at all for the rest.
    /// </summary>
    /// <remarks>
    /// What ties <c>aria-invalid</c> to the reason for it: the sentence sits above forty rows, and
    /// a field that only announces "invalid" leaves the reader to guess what is wrong with it
    /// (WCAG 3.3.1). Absent elsewhere, or every field would point at a region describing another
    /// row's text.
    /// </remarks>
    private string? DesiredDataDescribedBy(VariableListItem item) =>
        _desiredDataRefusal?.VariableId == item.VariableId ? DesiredDataRefusalId : null;

    /// <summary>
    /// Writes one row's annotation, or clears it, and says so when the API will not have it.
    /// </summary>
    /// <remarks>
    /// The text is kept here before the call and left alone after a refusal, so the reader is
    /// looking at their own words while they read why those words were not saved. Re-rendering the
    /// stored value instead would empty the field, which is the one thing a reader who has just
    /// typed 500 characters cannot recover from.
    /// </remarks>
    private async Task SaveDesiredDataAsync(Guid variableId, string? text)
    {
        if (_shownList is null)
        {
            return;
        }

        // Trimmed once, then both shown and sent: the API trims before it measures, and a draft
        // holding padding the request did not carry shows a value the server does not have.
        var list = _shownList.Value;
        var trimmed = text?.Trim() ?? "";

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
            var result = await Client.SetMyListDesiredDataAsync(list, variableId, trimmed);

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
        catch (MuninExplorerRateLimitedException)
        {
            // Typing down a list saves one row after another, which is exactly the rhythm the
            // per-address limiter counts — so a throttled annotation is ordinary rather than rare.
            failure = DesiredDataFailure.Throttled;
        }
        catch (Exception)
        {
            // Uncaught, this leaves the event handler and takes the circuit with it: a blank page
            // and a reconnect banner in place of the note the reader was writing.
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
    /// Empties the alert region — the one place that does. Five conditions share it, so a handler
    /// clearing only its own leaves an older one answering for what the reader just did.
    /// </summary>
    /// <remarks>
    /// A refused annotation is not among them: it has its own region because it outlives the
    /// action after it, and clearing it here would unmark a field whose text is still too long and
    /// still unsaved.
    /// </remarks>
    private void ForgetFailures()
    {
        _failed = false;
        _createFailure = ListActionFailure.None;
        _actionFailure = ListActionFailure.None;
        _downloadFailure = DownloadFailure.None;
        _desiredDataFailure = DesiredDataFailure.None;
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
        catch (MuninExplorerRateLimitedException)
        {
            // The list was made and the switch met the limiter. Told apart from the ordinary
            // failure for the reason the create half above gives: the remedy is to wait.
            _createFailure = ListActionFailure.Throttled;
            return;
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

        ForgetFailures();

        try
        {
            // The holder raises Changed, and OnStateChanged re-reads the page — so no fetch here.
            if (await State.RemoveVariablesAsync(_shownList.Value, [variableId]))
            {
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
        catch (MuninExplorerRateLimitedException)
        {
            // Removing is one of the writes the limiter counts, and "prøv igjen om litt" is
            // advice a throttled reader cannot use.
            _actionFailure = ListActionFailure.Throttled;
        }
        catch (Exception)
        {
            // Uncaught, this leaves the event handler and takes the circuit with it: a blank
            // page and a reconnect banner in place of the row the reader wanted gone.
            _actionFailure = ListActionFailure.Failed;
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
