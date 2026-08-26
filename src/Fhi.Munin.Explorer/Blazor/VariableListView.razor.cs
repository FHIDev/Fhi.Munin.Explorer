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

    private Page<VariableListItem>? _page;
    private Guid? _shownList;
    private int _pageNumber = 1;
    private bool _loading;
    private bool _failed;
    private string _newName = "";
    private bool _includeKodeverk;
    private bool _downloading;
    private bool _downloadFailed;

    private IReadOnlyList<VariableList> Lists => State?.Lists ?? [];

    /// <summary>
    /// The years a variable has data for, written the way the result rows and the detail panel
    /// write it — same shape as <c>VariableExplorer.Period</c>, so a variable does not read
    /// differently here than where it was saved from.
    /// </summary>
    private static string? Period(VariableListItem item)
    {
        var from = item.DataFrom?.Year.ToString();
        var to = item.DataTo?.Year.ToString();

        return (from, to) switch
        {
            (null, null) => null,
            (not null, null) => $"{from}–",
            (null, not null) => $"–{to}",
            _ => from == to ? from! : $"{from}–{to}"
        };
    }

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
        await State.EnsureActiveListAsync();
        await ShowActiveListAsync();
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

        await State.SetActiveListAsync(id);
        _shownList = id;
        _pageNumber = 1;
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
