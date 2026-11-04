using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// «Ønskede data» from a variable's code lists: one tab per list, a small list ticked whole, a large one searched and
/// browsed as a tree, with a node chosen with everything under it and, in a health code system, a range.
/// </summary>
/// <remarks>
/// The choices are a draft until <see cref="Save"/> answers: a refusal leaves them on screen to fix. Design note:
/// Fhi.Metadata-emsc0.3.1 (Inge, 2026-11-04).
/// </remarks>
public sealed partial class DesiredCodePicker : ComponentBase
{
    /// <summary>The most choices the API stores for one item.</summary>
    internal const int MaxChoices = 200;

    // Lists up to this many codes are ticked whole; longer ones are searched (the API's own threshold in the note).
    internal const int SmallList = 200;

    private const int PageSize = 50;

    private readonly string _instance = Guid.NewGuid().ToString("N")[..8];
    private readonly List<Choice> _choices = [];
    private readonly Dictionary<KodeverkKey, ListState> _lists = [];
    private int _tab;
    private string? _status;
    private bool _statusIsError;
    private bool _saving;
    private string _rangeFrom = "";
    private string _rangeTo = "";

    [Inject] private IMuninExplorerClient Client { get; set; } = null!;

    [Inject] private IServiceProvider ServiceProvider { get; set; } = null!;

    /// <summary>The variable whose code lists are offered.</summary>
    [Parameter, EditorRequired] public Guid VariableId { get; set; }

    /// <summary>The variable's code lists; only those whose values can be fetched get a tab.</summary>
    [Parameter, EditorRequired] public IReadOnlyList<KodeverkLink> Links { get; set; } = [];

    /// <summary>What is stored now, the starting draft.</summary>
    [Parameter] public IReadOnlyList<DesiredCodeView> Chosen { get; set; } = [];

    /// <summary>
    /// Stores the draft (with the row's free text, which the caller holds), each choice with what the picker knows of
    /// it, so the caller can show what was saved without reading the list again. Saved closes the picker.
    /// </summary>
    [Parameter, EditorRequired] public Func<IReadOnlyList<DesiredCodeView>, Task<DesiredDataResult>> Save { get; set; } = null!;

    /// <summary>Raised on «Avbryt», and after a save the API took.</summary>
    [Parameter] public EventCallback Closed { get; set; }

    [Parameter] public string Language { get; set; } = "no";

    private Texts T => Texts.For(Language);

    private ILogger? Log => ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger<DesiredCodePicker>();

    private TimeProvider Clock => ServiceProvider.GetService<TimeProvider>() ?? TimeProvider.System;

    internal IReadOnlyList<KodeverkLink> Tabs => [.. Links.Where(link => link.HasCodeValues)];

    private KodeverkLink? Current => Tabs.Count == 0 ? null : Tabs[Math.Clamp(_tab, 0, Tabs.Count - 1)];

    private bool Full => _choices.Count >= MaxChoices;

    private string Id(string part) => $"munin-explorer-picker-{_instance}-{part}";

    protected override void OnInitialized()
    {
        foreach (var view in Chosen.Take(MaxChoices))
        {
            _choices.Add(new Choice(view.ToChoice(), view.Name, view.ToName, view.Expired, view.Count));
        }
    }

    protected override Task OnInitializedAsync() => Current is { } link ? OpenAsync(link) : Task.CompletedTask;

    // ------------------------------------------------------------------------------------------------ the lists

    private sealed class ListState
    {
        public bool Loading { get; set; }
        public bool Failed { get; set; }
        public IReadOnlyList<KodeverkCode>? All { get; set; }
        public string Query { get; set; } = "";
        public string? Searched { get; set; }
        public List<KodeverkCodeHit> Hits { get; } = [];
        public int HitTotal { get; set; }
        public Dictionary<string, List<KodeverkCodeHit>> Children { get; } = [];
        public Dictionary<string, int> ChildTotals { get; } = [];
        public HashSet<string> Expanded { get; } = [];
        public bool IsLarge => All is null;
    }

    private const string Top = "";

    private ListState StateOf(KodeverkLink link) =>
        _lists.TryGetValue(KodeverkKey.Of(link), out var state) ? state : _lists[KodeverkKey.Of(link)] = new ListState();

    private static bool IsHealth(KodeverkLink link) =>
        string.Equals(link.KodeverkType, "HelsefagligKodeverk", StringComparison.OrdinalIgnoreCase);

    // A health code system is searched from the start; any other list is fetched whole and searched only when long.
    private async Task OpenAsync(KodeverkLink link)
    {
        var state = StateOf(link);
        if (state.Loading || state.All is not null || state.Children.ContainsKey(Top))
        {
            return;
        }

        state.Loading = true;
        state.Failed = false;
        try
        {
            if (!IsHealth(link)
                && await Client.GetKodeverkCodesAsync(VariableId, link.KodeverkType, link.KodeverkReference) is { } codes
                && codes.Codes.Count <= SmallList)
            {
                state.All = codes.Codes;
            }
            else
            {
                await LoadChildrenAsync(link, state, Top, skip: 0);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log?.LogWarning(ex, "the code list {Type}/{Reference} of variable {VariableId} could not be read", link.KodeverkType, link.KodeverkReference, VariableId);
            state.Failed = true;
        }
        finally
        {
            state.Loading = false;
        }
    }

    private async Task LoadChildrenAsync(KodeverkLink link, ListState state, string node, int skip)
    {
        var page = await Client.GetKodeverkCodeChildrenAsync(
            VariableId, link.KodeverkType, link.KodeverkReference, node == Top ? null : node, skip, PageSize);
        var list = state.Children.TryGetValue(node, out var known) && skip > 0 ? known : state.Children[node] = [];
        list.AddRange(page?.Codes ?? []);
        state.ChildTotals[node] = page?.Total ?? list.Count;
    }

    private async Task SelectTabAsync(int index)
    {
        _tab = index;
        if (Current is { } link)
        {
            await OpenAsync(link);
        }
    }

    // APG tabs: the arrow keys move between tabs, Home and End to the ends.
    private async Task TabKeyAsync(KeyboardEventArgs e)
    {
        var count = Tabs.Count;
        var next = e.Key switch
        {
            "ArrowRight" => (_tab + 1) % count,
            "ArrowLeft" => (_tab - 1 + count) % count,
            "Home" => 0,
            "End" => count - 1,
            _ => -1,
        };
        if (next >= 0 && count > 0)
        {
            await SelectTabAsync(next);
        }
    }

    private async Task SearchAsync(KodeverkLink link, int skip = 0)
    {
        var state = StateOf(link);
        var query = state.Query.Trim();
        if (query.Length == 0)
        {
            state.Searched = null;
            state.Hits.Clear();
            return;
        }

        state.Loading = true;
        try
        {
            var page = await Client.SearchKodeverkCodesAsync(VariableId, link.KodeverkType, link.KodeverkReference, query, skip, PageSize);
            if (skip == 0)
            {
                state.Hits.Clear();
            }

            state.Hits.AddRange(page?.Codes ?? []);
            state.HitTotal = page?.Total ?? state.Hits.Count;
            state.Searched = query;
            state.Failed = false;
            Announce(T.PickerHits(state.HitTotal), error: false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log?.LogWarning(ex, "a code search in {Type}/{Reference} failed", link.KodeverkType, link.KodeverkReference);
            Announce(T.PickerLoadError, error: true);
        }
        finally
        {
            state.Loading = false;
        }
    }

    private async Task SearchKeyAsync(KeyboardEventArgs e, KodeverkLink link)
    {
        if (e.Key == "Enter")
        {
            await SearchAsync(link);
        }
    }

    private async Task ToggleNodeAsync(MouseEventArgs press, KodeverkLink link, string node)
    {
        // The package's standing-gesture clause: a double-click's second click and a shift-click are not presses.
        if (RowPress.WasSelectionStandingStill(press))
        {
            return;
        }

        var state = StateOf(link);
        if (!state.Expanded.Add(node))
        {
            state.Expanded.Remove(node);
            return;
        }

        if (!state.Children.ContainsKey(node))
        {
            try
            {
                await LoadChildrenAsync(link, state, node, skip: 0);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log?.LogWarning(ex, "the codes under {Node} could not be read", node);
                state.Expanded.Remove(node);
                Announce(T.PickerLoadError, error: true);
            }
        }
    }

    private async Task MoreChildrenAsync(KodeverkLink link, string node)
    {
        var state = StateOf(link);
        try
        {
            await LoadChildrenAsync(link, state, node, state.Children.GetValueOrDefault(node)?.Count ?? 0);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log?.LogWarning(ex, "more codes under {Node} could not be read", node);
            Announce(T.PickerLoadError, error: true);
        }
    }

    // ---------------------------------------------------------------------------------------------- the choices

    /// <summary>A draft choice with what is known about it for display: its term(s), whether expired, its count.</summary>
    internal sealed record Choice(DesiredCodeChoice Value, string? Name, string? ToName, bool Expired, int? Count)
    {
        public DesiredCodeView ToView() => new()
        {
            KodeverkType = Value.KodeverkType,
            KodeverkReference = Value.KodeverkReference,
            Kind = Value.Kind,
            Code = Value.Code,
            To = Value.To,
            Name = Name,
            ToName = ToName,
            Expired = Expired,
            Count = Count,
        };
    }

    internal IReadOnlyList<Choice> Choices => _choices;

    private Choice? Find(KodeverkLink link, string kind, string code) =>
        _choices.FirstOrDefault(c => c.Value.Kind == kind && c.Value.Code == code
            && c.Value.KodeverkType == link.KodeverkType && c.Value.KodeverkReference == link.KodeverkReference);

    private bool IsChosen(KodeverkLink link, string kind, string code) => Find(link, kind, code) is not null;

    private bool Expired(DateTimeOffset? validTo) => validTo is { } end && end < Clock.GetUtcNow();

    private int? ExpiredYear(DateTimeOffset? validTo) => Expired(validTo) ? validTo!.Value.Year : null;

    // A code and the same code with everything under it are one choice or the other, never both.
    private async Task ToggleAsync(KodeverkLink link, string kind, string code, string name, DateTimeOffset? validTo)
    {
        if (Find(link, kind, code) is { } chosen)
        {
            _choices.Remove(chosen);
            return;
        }

        var other = Find(link, kind == DesiredCodeKinds.Node ? DesiredCodeKinds.Code : DesiredCodeKinds.Node, code);
        if (other is null && Full)
        {
            return;
        }

        var value = new DesiredCodeChoice(link.KodeverkType, link.KodeverkReference, kind, code);
        var choice = new Choice(value, name, null, Expired(validTo), kind == DesiredCodeKinds.Code ? 1 : null);
        if (other is not null)
        {
            _choices[_choices.IndexOf(other)] = choice;
        }
        else
        {
            _choices.Add(choice);
        }

        if (kind == DesiredCodeKinds.Node)
        {
            await CountAsync(choice);
        }
    }

    private async Task CountAsync(Choice choice)
    {
        try
        {
            var count = await Client.CountKodeverkCodesAsync(VariableId, choice.Value);
            var at = _choices.IndexOf(choice);
            if (at >= 0)
            {
                _choices[at] = choice with { Count = count };
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The count is a help, not a condition: the choice stands without it.
            Log?.LogWarning(ex, "the count of {Code} could not be read", choice.Value.Code);
        }
    }

    private async Task AddRangeAsync(KodeverkLink link)
    {
        var from = _rangeFrom.Trim();
        var to = _rangeTo.Trim();
        if (from.Length == 0 || to.Length == 0 || Full)
        {
            return;
        }

        var value = new DesiredCodeChoice(link.KodeverkType, link.KodeverkReference, DesiredCodeKinds.Range, from, to);
        if (_choices.Any(c => c.Value == value))
        {
            return;
        }

        try
        {
            // Asked first: a range the list does not have is refused here, not at «Lagre».
            if (await Client.CountKodeverkCodesAsync(VariableId, value) is not { } count)
            {
                Announce(T.PickerRangeUnknown, error: true);
                return;
            }

            _choices.Add(new Choice(value, null, null, false, count));
            _rangeFrom = _rangeTo = "";
            Announce(null, error: false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log?.LogWarning(ex, "the range {From}–{To} could not be counted", from, to);
            Announce(T.PickerRangeUnknown, error: true);
        }
    }

    private void Remove(Choice choice) => _choices.Remove(choice);

    /// <summary>A choice as words: the code and its term, «med alt under», or a range.</summary>
    internal static string Describe(Texts t, DesiredCodeChoice value, string? name, string? toName) => value.Kind switch
    {
        DesiredCodeKinds.Node => t.PickerWithAllUnder(value.Code),
        DesiredCodeKinds.Range => $"{value.Code}–{value.To}",
        _ => string.IsNullOrWhiteSpace(name) ? value.Code : $"{value.Code} {name}",
    };

    private int? TotalCodes => _choices.All(c => c.Count is not null) ? _choices.Sum(c => c.Count!.Value) : null;

    // ------------------------------------------------------------------------------------------------- saving

    private void Announce(string? text, bool error)
    {
        _status = text;
        _statusIsError = error;
    }

    private async Task SaveAsync()
    {
        if (_saving)
        {
            return;
        }

        _saving = true;
        Announce(null, error: false);
        try
        {
            var result = await Save([.. _choices.Select(c => c.ToView())]);
            switch (result)
            {
                case { Outcome: DesiredDataOutcome.Saved }:
                    Announce(T.PickerSaved, error: false);
                    await Closed.InvokeAsync();
                    break;
                case { Outcome: DesiredDataOutcome.Refused, InvalidIndex: { } index } when index >= 0 && index < _choices.Count:
                    var bad = _choices[index];
                    Announce(T.PickerInvalid(Describe(T, bad.Value, bad.Name, bad.ToName)), error: true);
                    break;
                case { Outcome: DesiredDataOutcome.Refused, MaxSelections: { } max }:
                    Announce(T.PickerTooMany(max), error: true);
                    break;
                case { Outcome: DesiredDataOutcome.ListUnavailable }:
                    Announce(T.PickerListUnavailable, error: true);
                    break;
                default:
                    Announce(T.DesiredDataError, error: true);
                    break;
            }
        }
        catch (MuninExplorerRateLimitedException ex)
        {
            Log?.LogWarning(ex, "the rate limiter refused the code choices of variable {VariableId}", VariableId);
            Announce(T.RateLimitError, error: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not MuninExplorerUnauthorizedException)
        {
            Log?.LogWarning(ex, "the code choices of variable {VariableId} could not be saved", VariableId);
            Announce(T.DesiredDataError, error: true);
        }
        finally
        {
            _saving = false;
        }
    }

    private Task CancelAsync() => Closed.InvokeAsync();
}
