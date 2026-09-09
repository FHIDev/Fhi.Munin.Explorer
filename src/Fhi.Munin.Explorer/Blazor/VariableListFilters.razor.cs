using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.Display;
using Fhi.Munin.Explorer.Logging;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The filter panel for the saved-list tab: the kilder the reader's active list draws from, each
/// with a count, and a tick that narrows the rows beside it.
/// </summary>
/// <remarks>
/// <para>
/// A component of its own rather than part of <see cref="VariableListView"/>, because the two sit
/// in different places on the page. Stiler lays the explorer out as a grid of a filter column and
/// a result column, and a panel is only in the filter column while it is a child of the explorer's
/// own section — the list itself renders inside the tab panel, which is in the other column. They
/// meet through <see cref="VariableListState"/>, which both resolve from the circuit.
/// </para>
/// <para>
/// The tally is the whole list's, collected by the membership walk the state already makes, and
/// never the page on screen: a facet built from a page names kilder the reader cannot see and hides
/// ones they can. The narrowing itself is the API's, so the pager and the count stay its answer.
/// </para>
/// </remarks>
public sealed partial class VariableListFilters : ComponentBase, IDisposable
{
    [Inject] private IServiceProvider ServiceProvider { get; set; } = null!;

    private ILogger? _log;

    /// <summary>The host's logger, or none — see <see cref="ExplorerLog"/>.</summary>
    private ILogger? Log => _log ??= ExplorerLog.For<VariableListFilters>(ServiceProvider);

    private VariableListState? _state;
    private VariableListState? State => _state ??= ServiceProvider.GetService<VariableListState>();

    /// <inheritdoc cref="VariableExplorer.Language"/>
    [Parameter] public string Language { get; set; } = "no";

    /// <inheritdoc cref="VariableExplorer.IsAuthenticated"/>
    [Parameter] public bool IsAuthenticated { get; set; }

    /// <summary>Heading level for the panel's own title, 1–6. Defaults to <c>2</c>.</summary>
    [Parameter] public int HeadingLevel { get; set; } = 2;

    private Texts T => Texts.For(Language);

    /// <summary>Clamped the way every other heading level in this package is: an &lt;h0&gt; is not a heading.</summary>
    private int TitleLevel => Math.Clamp(HeadingLevel, 1, 6);

    /// <summary>The facet's own heading, one step under the panel's and never past 6.</summary>
    private int SubTitleLevel => Math.Min(TitleLevel + 1, 6);

    /// <summary>
    /// The heading the kilde group is named by. Per mount, because a host may put two explorers on
    /// one page and two groups sharing an id would both be named from the first.
    /// </summary>
    private string KildeHeadingId => $"munin-explorer-list-kilde-{_instance}";

    private readonly string _instance = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// The list's kilder, in the catalogue's own order rather than the reader's. These are
    /// Norwegian names whoever is reading, so æ, ø and å belong at the end of the alphabet.
    /// </summary>
    /// <remarks>
    /// Ordered by the label rather than by <see cref="KildeInList.Name"/>, which is empty for a
    /// kilde the list names nowhere: that sorts before every letter, putting the one checkbox
    /// whose text is this package's own at the top of the catalogue's alphabet.
    /// </remarks>
    private IReadOnlyList<KildeInList> Kilder =>
        [.. (State?.KilderInList ?? []).OrderBy(Label, CatalogueProperties.CatalogueOrder)];

    /// <summary>
    /// What the checkbox is called. <see cref="T"/>'s "Ikke oppgitt" where the list names the
    /// kilde neither long nor short, as the two Kilde columns say it — the alternative is a
    /// checkbox whose whole accessible name is its count.
    /// </summary>
    private string Label(KildeInList kilde) => DisplayText.Trimmed(kilde.Name) ?? T.NotSpecified;

    private IReadOnlyCollection<Guid> Chosen => State?.KildeFilter ?? [];

    private bool IsChosen(Guid kildeId) => State?.IsKildeChosen(kildeId) == true;

    /// <summary>Whether the whole list has been read, which is what makes the empty sentence true.</summary>
    private bool Known => State?.KilderInListKnown == true;

    /// <summary>"(3)" alone; the separating space is emitted beside it — see Kelda's own facets.</summary>
    private static string Count(int count) => $"({count})";

    /// <summary>
    /// <c>"no"</c> for a name the catalogue wrote — the same marking the table's cells carry.
    /// Passed the stored name and not the label, so the "Ikke oppgitt" standing in for a missing
    /// one goes unmarked: that word is this package's, and in English it is not Norwegian at all.
    /// </summary>
    private static string? CatalogueLang(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : "no";

    protected override void OnInitialized()
    {
        if (State is not null)
        {
            // The tally arrives from a walk this component did not start, and the ticks are read by
            // the view in the other column. Both reach here as Changed.
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

        // Asked for here as well as by the view, so a host that mounts this panel on a page of its
        // own gets a tally rather than an empty heading. The ask joins a walk already running and
        // never retries one that was refused, which is the same shape VariableSearch already has.
        try
        {
            await State.EnsureActiveListAsync();
        }
        catch (Exception ex)
        {
            // Caught for the reason the view catches its own: a throw out of a lifecycle method
            // takes the circuit down, and on the legacy host that is the whole CMS page. The panel
            // draws nothing under the heading, and the view beside it says what went wrong.
            if (ex is MuninExplorerRateLimitedException or MuninExplorerUnauthorizedException)
            {
                Log?.LogWarning(ex, "the API refused the reader's list membership");
            }
            else
            {
                Log?.LogError(ex, "could not read the reader's list membership");
            }
        }
    }

    private void OnStateChanged(VariableListState.ListChange? change) => InvokeAsync(StateHasChanged);

    private void Toggle(Guid kildeId) => State?.ToggleKildeFilter(kildeId);

    private void ClearAll() => State?.ClearKildeFilter();

    public void Dispose()
    {
        if (_state is not null)
        {
            _state.Changed -= OnStateChanged;
        }
    }
}
