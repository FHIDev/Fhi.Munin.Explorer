using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

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

    private Texts T => Texts.For(Language);

    private Page<VariableListItem>? _page;
    private Guid? _shownList;
    private int _pageNumber = 1;
    private bool _loading;
    private bool _failed;
    private string _newName = "";

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

    private int TotalPages => _page is null || _page.Size <= 0
        ? 1
        : Math.Max(1, (int)Math.Ceiling(_page.TotalCount / (double)_page.Size));

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

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

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

        if (await State.RemoveVariablesAsync(_shownList.Value, [variableId]))
        {
            // Read the page again rather than dropping the row locally: removing the last entry on
            // a page changes which page exists, and the totals the pager shows come from the API.
            await LoadPageAsync();
        }
    }

    public void Dispose()
    {
        if (_state is not null)
        {
            _state.Changed -= OnStateChanged;
        }
    }
}
