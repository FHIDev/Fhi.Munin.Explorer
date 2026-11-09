using System.Text;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.Logging;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// «Ønskede data» from a variable's code lists: one tab per list, each a tree whose parent boxes mean «with everything
/// under it», searched first when the list is long, with a read-only summary of what is chosen under it.
/// </summary>
/// <remarks>The choices are a draft until <see cref="Save"/> answers: a refusal leaves them on screen to fix.</remarks>
public sealed partial class DesiredCodePicker : ComponentBase, IDisposable
{
    /// <summary>The most choices the API stores for one item.</summary>
    internal const int MaxChoices = 200;

    // Lists up to this many codes open as a tree alone; longer ones open with the search field first.
    internal const int SmallList = 200;

    // The API's largest page; a level is read page by page until it is whole or reaches LevelCap.
    internal const int PageSize = 100;

    // A flat list's one level is shown up to this many codes; search finds the rest.
    internal const int LevelCap = 200;

    internal const int SearchCap = 100;

    internal static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(300);

    private const string Top = "";
    private const string TreeSection = "tree";
    private const string HitsSection = "hits";

    private readonly string _instance = Guid.NewGuid().ToString("N")[..8];
    private readonly List<Choice> _choices = [];

    // Choices the tree may not show, each with a «Fjern» in the summary. Munin sends no count also while a list cannot be
    // read, so _noCount holds only until a loaded level of the tree holds the code; not found on the way into the tree,
    // or named by a refused save, holds until the line goes.
    private readonly HashSet<DesiredCodeChoice> _noCount = [];
    private readonly HashSet<DesiredCodeChoice> _notInTree = [];
    private readonly Dictionary<KodeverkKey, ListState> _lists = [];
    private readonly Dictionary<int, ElementReference> _tabButtons = [];
    private readonly Dictionary<string, ElementReference> _rows = [];
    private int _tab;
    private string? _status;
    private string? _alert;
    private ElementReference _counter;
    private bool _focusTab;
    private bool _focusCounter;
    private string? _focusRow;
    private bool _saving;
    private int? _union;
    private int _unionRun;
    private int _revealRun;
    private bool _disposed;
    private Task _untick = Task.CompletedTask;

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

    private ILogger? _log;

    // The package's guarded logger: a host's failing provider must not turn a handled failure into a dead circuit.
    private ILogger? Log => _log ??= ExplorerLog.For<DesiredCodePicker>(ServiceProvider);

    private TimeProvider Clock => ServiceProvider.GetService<TimeProvider>() ?? TimeProvider.System;

    // One tab per code list: a payload can name one twice, and a tab keyed twice would end the circuit.
    internal IReadOnlyList<KodeverkLink> Tabs =>
        [.. Links.Where(link => link.HasCodeValues).DistinctBy(KodeverkKey.Of)];

    // The catalogue's names are Norwegian; marked so an English page reads them in Norwegian (WCAG 3.1.2).
    private string? CatalogueLang => ReaderLanguage.IsEnglish(Language) ? "no" : null;

    private KodeverkLink? Current => Tabs.Count == 0 ? null : Tabs[Math.Clamp(_tab, 0, Tabs.Count - 1)];

    private bool Full => _choices.Count >= MaxChoices;

    private string Id(string part) => $"munin-explorer-picker-{_instance}-{part}";

    protected override void OnInitialized()
    {
        foreach (var view in Chosen.Take(MaxChoices))
        {
            var choice = new Choice(view.ToChoice(), view.Name, view.ToName, view.Expired, view.Count, view.KodeverkName, view.Ancestors);
            if (Insert(_choices.Count, choice) && view.Count is null)
            {
                _noCount.Add(choice.Value);
            }
        }
    }

    // The summary keys its lines on the value, so two equal choices would end the circuit; saved data may repeat one.
    private bool Insert(int at, Choice choice)
    {
        if (_choices.Any(c => c.Value == choice.Value))
        {
            return false;
        }

        _choices.Insert(Math.Clamp(at, 0, _choices.Count), choice);
        return true;
    }

    // A saved range from an ICD-10 block's first category to its last (E10..E14 under «E10-E14») stops at E14, so it
    // leaves out E14's subcodes. It lies inside the block, so it is the block only when both count the same codes.
    private static string? BlockOf(Choice choice) =>
        choice.Value.Kind == DesiredCodeKinds.Range && choice.Count is not null
        && choice.Ancestors.Contains($"{choice.Value.Code}-{choice.Value.To}")
            ? $"{choice.Value.Code}-{choice.Value.To}"
            : null;

    // An unknown count, a failed call, or another number keeps the range as it was saved.
    private async Task AsBlocksAsync()
    {
        foreach (var range in _choices.Where(c => BlockOf(c) is not null).ToList())
        {
            var block = BlockOf(range)!;
            var value = range.Value with { Kind = DesiredCodeKinds.Node, Code = block, To = null };
            int? count;
            try
            {
                count = await Client.CountKodeverkCodesAsync(VariableId, value);
            }
            catch (Exception ex)
            {
                Log?.LogWarning(ex, "the block {Block} of variable {VariableId} could not be counted", block, VariableId);
                continue;
            }

            var at = _choices.IndexOf(range);
            if (count is null || count != range.Count || at < 0 || _saving)
            {
                continue;
            }

            if (_choices.Any(c => SameList(c, range) && IsDirect(c, block)))
            {
                _choices.RemoveAt(at);
            }
            else
            {
                _choices[at] = new Choice(value, null, null, false, count, range.ListName, [.. range.Ancestors.Take(IndexOf(range.Ancestors, block))]);
            }
        }
    }

    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
            {
                return i;
            }
        }

        return -1;
    }

    protected override async Task OnInitializedAsync()
    {
        var recount = RecountAsync();
        var blocks = AsBlocksAsync();
        if (Current is { } link)
        {
            await OpenAsync(link);
        }

        await recount;
        await blocks;
    }

    public void Dispose()
    {
        _disposed = true;
        foreach (var state in _lists.Values)
        {
            state.Debounce?.Cancel();
            state.Debounce?.Dispose();
        }
    }

    // ------------------------------------------------------------------------------------------------ the lists

    private sealed class Branch
    {
        public List<KodeverkCodeHit> Codes { get; } = [];
        public int Total { get; set; }
        public bool Loaded { get; set; }
        public bool Failed { get; set; }
        public Task<Branch?>? Pending { get; set; }
    }

    private sealed class ListState
    {
        public int? CodeCount { get; set; }
        public Dictionary<string, Branch> Branches { get; } = [];
        public Dictionary<string, KodeverkCodeHit> Known { get; } = [];
        public Dictionary<string, string> Parents { get; } = [];
        public HashSet<string> TreeOpen { get; } = [];
        public HashSet<string> HitsOpen { get; } = [];
        public Dictionary<string, string> Active { get; } = [];
        public string Query { get; set; } = "";
        public string? Searched { get; set; }
        public List<KodeverkCodeHit> Hits { get; } = [];
        public int HitTotal { get; set; }
        public int SearchRun { get; set; }
        public CancellationTokenSource? Debounce { get; set; }

        public Branch? TopBranch => Branches.GetValueOrDefault(Top);

        // The list's own size when the API says it, else the first level's: an older API sends no antallKoder.
        public bool IsLarge => (CodeCount ?? TopBranch?.Total ?? 0) > SmallList;

        public HashSet<string> OpenIn(string section) => section == HitsSection ? HitsOpen : TreeOpen;
    }

    private ListState StateOf(KodeverkLink link) =>
        _lists.TryGetValue(KodeverkKey.Of(link), out var state) ? state : _lists[KodeverkKey.Of(link)] = new ListState();

    private Task OpenAsync(KodeverkLink link) => BranchAsync(link, StateOf(link), Top);

    // A level is read whole, page after page, up to LevelCap; one read at a time per level, which later askers share.
    private Task<Branch?> BranchAsync(KodeverkLink link, ListState state, string node)
    {
        if (state.Branches.TryGetValue(node, out var known) && (known.Loaded || known.Pending is not null))
        {
            return known.Loaded ? Task.FromResult<Branch?>(known) : known.Pending!;
        }

        var branch = state.Branches[node] = new Branch();
        var load = LoadBranchAsync(link, state, node, branch);

        // A read that answered at once has already run its finally, so it is not pending.
        branch.Pending = load.IsCompleted ? null : load;
        return load;
    }

    private async Task<Branch?> LoadBranchAsync(KodeverkLink link, ListState state, string node, Branch branch)
    {
        try
        {
            // A page that adds nothing new ends the level, and the pages are capped, so a wrong total cannot loop.
            for (var pages = 0; pages < LevelCap / PageSize + 1; pages++)
            {
                var page = await Client.GetKodeverkCodeChildrenAsync(
                    VariableId, link.KodeverkType, link.KodeverkReference, node == Top ? null : node, branch.Codes.Count, PageSize)
                    ?? throw new InvalidOperationException("the code list or the code is unknown");
                var before = branch.Codes.Count;
                branch.Codes.AddRange(page.Codes.Where(hit => branch.Codes.All(seen => seen.Value != hit.Value)));
                branch.Total = page.Total;
                if (node == Top && page.CodeCount > 0)
                {
                    state.CodeCount = page.CodeCount;
                }

                if (branch.Codes.Count == before || branch.Codes.Count >= Math.Min(branch.Total, LevelCap))
                {
                    break;
                }
            }

            foreach (var hit in branch.Codes)
            {
                state.Known[hit.Value] = hit;
                if (node != Top)
                {
                    state.Parents[hit.Value] = node;
                }
            }

            branch.Loaded = true;
            return branch;
        }
        catch (Exception ex)
        {
            Log?.LogWarning(ex, "the codes under {Node} in {Type}/{Reference} of variable {VariableId} could not be read",
                node, link.KodeverkType, link.KodeverkReference, VariableId);
            branch.Failed = true;
            return null;
        }
        finally
        {
            branch.Pending = null;
        }
    }

    // A failed level is asked again from scratch: the press is the reader's retry.
    private Task RetryAsync(KodeverkLink link, string node)
    {
        var state = StateOf(link);
        if (state.Branches.TryGetValue(node, out var branch) && branch.Failed)
        {
            state.Branches.Remove(node);
        }

        return BranchAsync(link, state, node);
    }

    private async Task SelectTabAsync(int index, bool revealing = false)
    {
        if (!revealing)
        {
            _revealRun++;
        }

        _tab = index;
        _alert = null;
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
            _focusTab = true;
            await SelectTabAsync(next);
        }
    }

    // --------------------------------------------------------------------------------------------- the search

    // The API refuses a query without a letter or a digit, so one is not sent.
    private static bool Searchable(string query) => query.Length is > 0 and <= 100 && query.Any(char.IsLetterOrDigit);

    // The wait runs beside the input's handler, not in it, so a keystroke that cancels it costs no second render.
    private void QueryChanged(ChangeEventArgs e, KodeverkLink link)
    {
        var state = StateOf(link);
        state.Query = e.Value?.ToString() ?? "";
        if (state.Query.Trim() != state.Searched)
        {
            // Hits of the last query must not be chosen as if they were this one's, nor an answer still out land.
            state.SearchRun++;
            ClearHits(state);
        }

        state.Debounce?.Cancel();
        state.Debounce?.Dispose();
        state.Debounce = new CancellationTokenSource();
        _ = SearchLaterAsync(link, state.Debounce.Token);
    }

    private async Task SearchLaterAsync(KodeverkLink link, CancellationToken token)
    {
        try
        {
            // Off the renderer: a cancelled wait then posts nothing to it, and the search comes back through InvokeAsync.
            await Task.Delay(SearchDelay, Clock, token).ConfigureAwait(false);
            await InvokeAsync(async () =>
            {
                if (!token.IsCancellationRequested && !_disposed)
                {
                    await SearchAsync(link);
                    StateHasChanged();
                }
            });
        }
        catch (OperationCanceledException)
        {
            // A newer keystroke, Enter, or the picker closing took over.
        }
    }

    private async Task SearchKeyAsync(KeyboardEventArgs e, KodeverkLink link)
    {
        if (e.Key == "Enter")
        {
            StateOf(link).Debounce?.Cancel();
            await SearchAsync(link);
        }
    }

    // Only the newest answer lands: a cleared field also retires one still out, so it cannot bring old hits back.
    private async Task SearchAsync(KodeverkLink link)
    {
        var state = StateOf(link);
        var query = state.Query.Trim();
        var run = ++state.SearchRun;
        if (!Searchable(query))
        {
            ClearHits(state);
            return;
        }

        try
        {
            var page = await Client.SearchKodeverkCodesAsync(VariableId, link.KodeverkType, link.KodeverkReference, query, 0, SearchCap);
            if (run != state.SearchRun || _disposed)
            {
                return;
            }

            ClearHits(state);
            state.Hits.AddRange((page?.Codes ?? []).DistinctBy(hit => hit.Value));
            foreach (var hit in state.Hits)
            {
                state.Known.TryAdd(hit.Value, hit);
            }

            state.HitTotal = page?.Total ?? state.Hits.Count;
            state.Searched = query;
            Announce(T.PickerHits(state.HitTotal), error: false);
        }
        catch (Exception ex)
        {
            Log?.LogWarning(ex, "a code search in {Type}/{Reference} failed", link.KodeverkType, link.KodeverkReference);
            if (run == state.SearchRun && !_disposed)
            {
                ClearHits(state);
                Announce(T.PickerLoadError, error: true);
            }
        }
    }

    private static void ClearHits(ListState state)
    {
        state.Searched = null;
        state.Hits.Clear();
        state.HitsOpen.Clear();
        state.Active.Remove(HitsSection);
    }

    // What the search shows, and never more: a hit already chosen, or under a chosen parent, is left as it is.
    private async Task ChooseHitsAsync(KodeverkLink link)
    {
        await UntickDoneAsync();
        var state = StateOf(link);
        if (_saving)
        {
            return;
        }

        foreach (var hit in state.Hits.ToList())
        {
            if (TickOf(link, state, hit) == Tick.Checked)
            {
                continue;
            }

            if (!Choose(link, state, hit))
            {
                Announce(T.PickerFull(MaxChoices), error: true);
                break;
            }
        }

        await RecountAsync();
    }

    // Each hit as if its box were unticked, a hit under a whole parent included; stops at the first that cannot be.
    private async Task ClearHitsAsync(KodeverkLink link)
    {
        await UntickDoneAsync();
        var state = StateOf(link);
        if (_saving)
        {
            return;
        }

        foreach (var hit in state.Hits.ToList())
        {
            var tick = TickOf(link, state, hit);
            if (tick == Tick.Mixed)
            {
                _choices.RemoveAll(c => SameList(c, link) && IsUnder(c, hit.Value));
            }
            else if (tick == Tick.Checked && !await UntickAsync(link, state, hit))
            {
                break;
            }

            // A hit's untick may read; a save or closing the picker meanwhile ends the run there.
            if (_saving || _disposed)
            {
                return;
            }
        }

        await RecountAsync();
    }

    // ----------------------------------------------------------------------------------------------- the tree

    internal enum Tick
    {
        Empty,
        Mixed,
        Checked,
    }

    // A row is known by its path, not its code: an opened hit can list a code that is also a hit of its own.
    private readonly record struct Row(string Path, string Code, int Level, string? Parent, KodeverkCodeHit Hit, bool Open);

    internal static string PathOf(string? parent, string code) => parent is null ? code : $"{parent}\u001f{code}";

    // The rows a reader can see in one section, in order: what the arrow keys walk.
    private static List<Row> VisibleRows(ListState state, string section)
    {
        var rows = new List<Row>();
        var roots = section == HitsSection ? state.Hits : state.TopBranch?.Codes ?? [];
        Walk(roots, 1, null);
        return rows;

        void Walk(IReadOnlyList<KodeverkCodeHit> codes, int level, string? parent)
        {
            foreach (var hit in codes)
            {
                var path = PathOf(parent, hit.Value);
                var open = hit.HasChildren && state.OpenIn(section).Contains(hit.Value);
                rows.Add(new Row(path, hit.Value, level, parent, hit, open));
                if (open && state.Branches.GetValueOrDefault(hit.Value) is { Loaded: true } branch)
                {
                    Walk(branch.Codes, level + 1, path);
                }
            }
        }
    }

    // The row in the tab order: the last one moved to, or the first while that one is out of sight.
    private static string? ActivePath(ListState state, string section)
    {
        var rows = VisibleRows(state, section);
        return state.Active.TryGetValue(section, out var path) && rows.Any(r => r.Path == path) ? path : rows.FirstOrDefault().Path;
    }

    private static string RowKey(string section, string path) => $"{section}:{path}";

    // Codes carry dots and spaces; hex keeps the id valid and distinct per row.
    private string RowId(string section, string path) => Id($"{section}-{Convert.ToHexString(Encoding.UTF8.GetBytes(path))}");

    private static IReadOnlyList<string> AncestorsOf(ListState state, KodeverkCodeHit hit) =>
        hit.Ancestors.Count > 0 ? hit.Ancestors : AncestorsOf(state, hit.Value);

    private static IReadOnlyList<string> AncestorsOf(ListState state, string code)
    {
        if (state.Known.TryGetValue(code, out var hit) && hit.Ancestors.Count > 0)
        {
            return hit.Ancestors;
        }

        var path = new List<string>();
        for (var at = code; state.Parents.TryGetValue(at, out var parent) && !path.Contains(parent); at = parent)
        {
            path.Insert(0, parent);
        }

        return path;
    }

    private static bool SameList(Choice choice, KodeverkLink link) =>
        choice.Value.KodeverkType == link.KodeverkType && choice.Value.KodeverkReference == link.KodeverkReference;

    private static bool SameList(Choice choice, Choice other) =>
        choice.Value.KodeverkType == other.Value.KodeverkType && choice.Value.KodeverkReference == other.Value.KodeverkReference;

    private static bool IsDirect(Choice choice, string code) =>
        choice.Value.Code == code && choice.Value.Kind is DesiredCodeKinds.Code or DesiredCodeKinds.Node;

    // A range's parents are its first code's only, and it may run past the code, so it is never under one.
    private static bool IsUnder(Choice choice, string code) =>
        choice.Value.Kind != DesiredCodeKinds.Range && choice.Ancestors.Contains(code);

    private IEnumerable<Choice> ChoicesIn(KodeverkLink link) => _choices.Where(c => SameList(c, link));

    // A choice stands to a code as itself (IsDirect), over it (a whole parent: WholeOver) or under it (IsUnder); an older
    // range only half-ticks the codes above its first code. Ticking removes what is under and adds itself; Untick removes
    // itself and what is under it, and splits a whole parent over it.
    private Tick TickOf(KodeverkLink link, ListState state, KodeverkCodeHit hit)
    {
        var ancestors = AncestorsOf(state, hit);
        var chosen = ChoicesIn(link).ToList();
        if (chosen.Any(c => IsDirect(c, hit.Value) || (c.Value.Kind == DesiredCodeKinds.Node && ancestors.Contains(c.Value.Code))))
        {
            return Tick.Checked;
        }

        return chosen.Any(c => IsUnder(c, hit.Value) || (IsOlderRange(c) && c.Ancestors.Contains(hit.Value))) ? Tick.Mixed : Tick.Empty;
    }

    // «k av n» beside a parent: n from the API, k the sum of the choices under it, held at n since saved picks can
    // overlap. A range is left out of k, as its codes may reach outside; a code chosen alone, not whole, shows none.
    private string? OfCount(KodeverkLink link, ListState state, KodeverkCodeHit hit, Tick tick)
    {
        if (hit.Count is not { } all || tick == Tick.Empty)
        {
            return null;
        }

        if (tick == Tick.Checked)
        {
            return WholeOver(link, AncestorsOf(state, hit)) is not null
                || ChoicesIn(link).Any(c => c.Value.Kind == DesiredCodeKinds.Node && c.Value.Code == hit.Value)
                ? T.PickerOf(all, all)
                : null;
        }

        var under = ChoicesIn(link).Where(c => IsUnder(c, hit.Value)).ToList();
        return under.Count > 0 && under.All(c => c.Count is not null) ? T.PickerOf(Math.Min(under.Sum(c => c.Count!.Value), all), all) : null;
    }

    // The outermost parent chosen whole: saved picks can nest one whole parent inside another.
    private Choice? WholeOver(KodeverkLink link, IReadOnlyList<string> ancestors)
    {
        foreach (var ancestor in ancestors)
        {
            if (ChoicesIn(link).FirstOrDefault(c => c.Value.Kind == DesiredCodeKinds.Node && c.Value.Code == ancestor) is { } whole)
            {
                return whole;
            }
        }

        return null;
    }

    // A code with subcodes kept on its own, as the split of a whole parent keeps the codes on the way down.
    private static Choice CodeOnly(KodeverkLink link, KodeverkCodeHit hit, IReadOnlyList<string> ancestors) =>
        new(new DesiredCodeChoice(link.KodeverkType, link.KodeverkReference, DesiredCodeKinds.Code, hit.Value),
            hit.Name, null, hit.Expired, 1, ListNameOf(link), ancestors);

    // A group (an ICD-10 chapter or block) is only ever chosen with everything under it; the API refuses it as a code.
    private Choice ChoiceOf(KodeverkLink link, ListState state, KodeverkCodeHit hit, IReadOnlyList<string>? ancestors = null)
    {
        var node = hit.HasChildren || hit.IsGroup;
        var value = new DesiredCodeChoice(link.KodeverkType, link.KodeverkReference, node ? DesiredCodeKinds.Node : DesiredCodeKinds.Code, hit.Value);
        return new Choice(value, hit.Name, null, hit.Expired, node ? hit.Count : 1, ListNameOf(link), ancestors ?? AncestorsOf(state, hit));
    }

    // Chooses a code whole, taking in what was chosen under it; false when that would pass the ceiling.
    private bool Choose(KodeverkLink link, ListState state, KodeverkCodeHit hit)
    {
        var under = ChoicesIn(link).Where(c => IsUnder(c, hit.Value)).ToList();
        if (_choices.Count - under.Count + 1 > MaxChoices)
        {
            return false;
        }

        var at = under.Count > 0 ? _choices.IndexOf(under[0]) : _choices.Count;
        _choices.RemoveAll(under.Contains);
        var chosen = ChoiceOf(link, state, hit);
        _noCount.Remove(chosen.Value);
        _notInTree.Remove(chosen.Value);
        Insert(at, chosen);
        return true;
    }

    // A press while an untick reads waits for it, then acts on the draft that untick left.
    private async Task UntickDoneAsync()
    {
        while (!_untick.IsCompleted)
        {
            await _untick;
        }
    }

    private async Task ToggleTickAsync(KodeverkLink link, KodeverkCodeHit hit)
    {
        await UntickDoneAsync();

        // The draft holds still while it is being saved, so what the answer is about is what is on screen.
        if (_saving)
        {
            return;
        }

        var state = StateOf(link);
        if (TickOf(link, state, hit) != Tick.Checked)
        {
            if (!Choose(link, state, hit))
            {
                Announce(T.PickerFull(MaxChoices), error: true);
                return;
            }
        }
        else if (!await UntickAsync(link, state, hit))
        {
            return;
        }

        Announce(null, error: false);
        await RecountAsync();
    }

    // Unticks a ticked code: without a whole parent over it, the code and every pick under it go; with one, that parent
    // is split. False when nothing changed (a read failed, a save came in, or the ceiling); the reason is announced.
    private async Task<bool> UntickAsync(KodeverkLink link, ListState state, KodeverkCodeHit hit)
    {
        if (WholeOver(link, AncestorsOf(state, hit)) is null)
        {
            _choices.RemoveAll(c => SameList(c, link) && (IsDirect(c, hit.Value) || IsUnder(c, hit.Value)));
            return true;
        }

        var untick = UntickUnderAsync(link, state, hit);
        _untick = untick;
        return await untick;
    }

    // Unticking a code under a parent chosen whole: that parent and every pick under it become its other children, level
    // by level down to the code, never one already chosen, and each code on the way (not a group) is kept on its own.
    // Every level is read first; a failed read changes nothing.
    private async Task<bool> UntickUnderAsync(KodeverkLink link, ListState state, KodeverkCodeHit hit)
    {
        var ancestors = AncestorsOf(state, hit);
        if (WholeOver(link, ancestors) is not { } whole)
        {
            return false;
        }

        var from = IndexOf(ancestors, whole.Value.Code);
        var added = new List<Choice>();

        // The whole parent's own row tells whether it is a code or a group; its level is read when it was never shown.
        if (!state.Known.ContainsKey(whole.Value.Code) && await BranchAsync(link, state, from == 0 ? Top : ancestors[from - 1]) is null)
        {
            Announce(T.PickerLoadError, error: true);
            return false;
        }

        for (var i = from; i < ancestors.Count; i++)
        {
            if (!state.Known.TryGetValue(ancestors[i], out var onTheWay))
            {
                Announce(T.PickerLoadError, error: true);
                return false;
            }

            if (!onTheWay.IsGroup)
            {
                added.Add(CodeOnly(link, onTheWay, [.. ancestors.Take(i)]));
            }

            var next = i + 1 < ancestors.Count ? ancestors[i + 1] : hit.Value;
            var branch = await BranchAsync(link, state, ancestors[i]);
            if (branch is null)
            {
                Announce(T.PickerLoadError, error: true);
                return false;
            }

            if (branch.Codes.Count < branch.Total)
            {
                Announce(T.PickerLevelCut(ancestors[i], whole.Value.Code), error: true);
                return false;
            }

            IReadOnlyList<string> path = [.. ancestors.Take(i + 1)];
            added.AddRange(branch.Codes.Where(c => c.Value != next).Select(c => ChoiceOf(link, state, c, c.Ancestors.Count > 0 ? c.Ancestors : path)));
        }

        // Checked again after the reads: a save, or another press, may have come in meanwhile.
        var at = _choices.IndexOf(whole);
        if (_saving || at < 0)
        {
            return false;
        }

        var gone = ChoicesIn(link)
            .Where(c => c == whole || IsUnder(c, whole.Value.Code) || IsDirect(c, hit.Value))
            .ToHashSet();
        var kept = _choices.Where(c => !gone.Contains(c)).Select(c => c.Value).ToHashSet();
        added = [.. added.DistinctBy(c => c.Value).Where(c => !kept.Contains(c.Value))];
        if (_choices.Count - gone.Count + added.Count > MaxChoices)
        {
            Announce(T.PickerFull(MaxChoices), error: true);
            return false;
        }

        at -= _choices.Take(at).Count(gone.Contains);
        _choices.RemoveAll(gone.Contains);
        foreach (var choice in added)
        {
            if (Insert(at, choice))
            {
                at++;
            }
        }

        return true;
    }

    private async Task ToggleOpenAsync(KodeverkLink link, string section, KodeverkCodeHit hit)
    {
        var state = StateOf(link);
        var open = state.OpenIn(section);
        if (!hit.HasChildren)
        {
            return;
        }

        if (!open.Add(hit.Value))
        {
            open.Remove(hit.Value);
            return;
        }

        await BranchAsync(link, state, hit.Value);
    }

    private async Task RowPressAsync(MouseEventArgs press, KodeverkLink link, string section, string path, KodeverkCodeHit hit)
    {
        // The package's standing-gesture clause: a double-click's second click and a shift-click are not presses.
        if (RowPress.WasSelectionStandingStill(press))
        {
            return;
        }

        StateOf(link).Active[section] = path;
        await ToggleTickAsync(link, hit);
    }

    private async Task TogglePressAsync(MouseEventArgs press, KodeverkLink link, string section, string path, KodeverkCodeHit hit)
    {
        if (RowPress.WasSelectionStandingStill(press))
        {
            return;
        }

        StateOf(link).Active[section] = path;
        await ToggleOpenAsync(link, section, hit);
    }

    // The APG tree: ↑ ↓ move, → opens or goes to the first child, ← closes or goes to the parent, Home and End jump,
    // Space ticks. Focus moves with the tab stop (roving tabindex).
    private async Task TreeKeyAsync(KeyboardEventArgs e, KodeverkLink link, string section)
    {
        var state = StateOf(link);
        var rows = VisibleRows(state, section);
        if (rows.Count == 0 || e.AltKey || e.CtrlKey || e.MetaKey)
        {
            return;
        }

        var active = ActivePath(state, section);
        var at = Math.Max(0, rows.FindIndex(r => r.Path == active));
        var row = rows[at];
        int? next = null;
        switch (e.Key)
        {
            case "ArrowDown":
                next = Math.Min(at + 1, rows.Count - 1);
                break;
            case "ArrowUp":
                next = Math.Max(at - 1, 0);
                break;
            case "Home":
                next = 0;
                break;
            case "End":
                next = rows.Count - 1;
                break;
            case "ArrowRight" when row.Hit.HasChildren && !row.Open:
                await ToggleOpenAsync(link, section, row.Hit);
                break;
            case "ArrowRight" when row.Open && at + 1 < rows.Count && rows[at + 1].Parent == row.Path:
                next = at + 1;
                break;
            case "ArrowLeft" when row.Open:
                await ToggleOpenAsync(link, section, row.Hit);
                break;
            case "ArrowLeft" when row.Parent is { } parent:
                next = rows.FindIndex(r => r.Path == parent);
                break;
            case " " or "Spacebar":
                await ToggleTickAsync(link, row.Hit);
                break;
        }

        if (next is { } to && to >= 0)
        {
            state.Active[section] = rows[to].Path;
            _focusRow = RowKey(section, rows[to].Path);
        }
    }

    // The summary's way into the tree: its tab, every level above the code opened, focus on its row. A later reveal,
    // another tab or the picker closing while it reads takes over, and this one then does nothing more.
    private async Task RevealAsync(Choice choice)
    {
        var run = ++_revealRun;
        var index = Tabs.ToList().FindIndex(link => SameList(choice, link));
        if (index < 0)
        {
            return;
        }

        var link = Tabs[index];
        bool Stale() => run != _revealRun || _disposed || Current is not { } now || KodeverkKey.Of(now) != KodeverkKey.Of(link);
        if (index != Math.Clamp(_tab, 0, Tabs.Count - 1))
        {
            await SelectTabAsync(index, revealing: true);
        }

        var state = StateOf(link);
        await OpenAsync(link);
        if (Stale())
        {
            return;
        }

        // The top level that failed to read is an error, not a sign the code is gone.
        if (state.TopBranch is not { Loaded: true, Failed: false })
        {
            Announce(T.PickerLoadError, error: true);
            return;
        }

        var ancestors = choice.Ancestors.Count > 0 ? choice.Ancestors : AncestorsOf(state, choice.Value.Code);
        foreach (var ancestor in ancestors)
        {
            var branch = Stale() ? null : await BranchAsync(link, state, ancestor);
            if (Stale())
            {
                return;
            }

            if (branch is null)
            {
                Announce(T.PickerLoadError, error: true);
                return;
            }

            state.TreeOpen.Add(ancestor);
        }

        if (Stale())
        {
            return;
        }

        if (VisibleRows(state, TreeSection).FirstOrDefault(r => r.Code == choice.Value.Code) is not { Path: { } path })
        {
            _notInTree.Add(choice.Value);
            Announce(T.PickerNotInTree(choice.Value.Code), error: true);
            return;
        }

        state.Active[TreeSection] = path;
        _focusRow = RowKey(TreeSection, path);
    }

    // ---------------------------------------------------------------------------------------------- the choices

    /// <summary>A draft choice with what is known about it for display: its term(s), whether expired, its count, what is above it.</summary>
    internal sealed record Choice(
        DesiredCodeChoice Value, string? Name, string? ToName, bool Expired, int? Count, string? ListName = null, IReadOnlyList<string>? Above = null)
    {
        public IReadOnlyList<string> Ancestors => Above ?? [];

        public DesiredCodeView ToView() => new()
        {
            KodeverkType = Value.KodeverkType,
            KodeverkReference = Value.KodeverkReference,
            KodeverkName = ListName,
            Kind = Value.Kind,
            Code = Value.Code,
            To = Value.To,
            Name = Name,
            ToName = ToName,
            Expired = Expired,
            Count = Count,
            Ancestors = Ancestors,
        };
    }

    internal IReadOnlyList<Choice> Choices => _choices;

    // A range the tree has no block for: kept, and shown as an older choice.
    private static bool IsOlderRange(Choice choice) => choice.Value.Kind == DesiredCodeKinds.Range;

    // What the summary can remove: whatever no box in the tree stands for.
    private bool Removable(Choice choice) =>
        IsOlderRange(choice) || !CanReveal(choice) || _notInTree.Contains(choice.Value)
        || (_noCount.Contains(choice.Value) && !InLoadedTree(choice));

    // A search hit is not enough: only a level read from the tree shows the code where the reader can find it again.
    private bool InLoadedTree(Choice choice) =>
        Tabs.Any(link => SameList(choice, link) && _lists.TryGetValue(KodeverkKey.Of(link), out var state)
            && state.Branches.Values.Any(b => b.Loaded && b.Codes.Any(c => c.Value == choice.Value.Code)));

    // The list's name as the variable page gives it, so a saved choice keeps the label the summary shows.
    private static string? ListNameOf(KodeverkLink link) => string.IsNullOrWhiteSpace(link.DisplayName) ? null : link.DisplayName;

    // The removed line took its button with it, so focus goes to the count, which says what is left.
    private async Task RemoveAsync(Choice choice)
    {
        if (_saving)
        {
            return;
        }

        _choices.Remove(choice);
        _noCount.Remove(choice.Value);
        _notInTree.Remove(choice.Value);
        _focusCounter = true;
        await RecountAsync();
    }

    /// <summary>A choice as words: the code and its term, «med alt under», or a range.</summary>
    internal static string Describe(Texts t, DesiredCodeChoice value, string? name, string? toName) => value.Kind switch
    {
        DesiredCodeKinds.Node => t.PickerWithAllUnder(value.Code),
        DesiredCodeKinds.Range => $"{value.Code}–{value.To}",
        _ => string.IsNullOrWhiteSpace(name) ? value.Code : $"{value.Code} {name}",
    };

    // The union of the choices per code list, from the API: E11 inside a chosen E10-E14 counts once. Only the newest
    // answer lands; a list it cannot count leaves the total unknown rather than short.
    private async Task RecountAsync()
    {
        var run = ++_unionRun;
        if (_choices.Count == 0)
        {
            _union = 0;
            return;
        }

        int? total = 0;
        try
        {
            foreach (var list in _choices.GroupBy(c => (c.Value.KodeverkType, c.Value.KodeverkReference)))
            {
                if (await Client.CountKodeverkCodesUnionAsync(VariableId, [.. list.Select(c => c.Value)]) is not { } count)
                {
                    total = null;
                    break;
                }

                total += count;
            }
        }
        catch (Exception ex)
        {
            // The count is a help, not a condition: the choices stand without it.
            Log?.LogWarning(ex, "the chosen codes of variable {VariableId} could not be counted", VariableId);
            total = null;
        }

        if (run == _unionRun)
        {
            _union = total;
        }
    }

    // ------------------------------------------------------------------------------------------------- saving

    // Two regions, both present from the first render: a polite one for counts, an alert for what went wrong.
    private void Announce(string? text, bool error)
    {
        _status = error ? null : text;
        _alert = error ? text : null;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            if (_focusTab && _tabButtons.TryGetValue(Math.Clamp(_tab, 0, Math.Max(0, Tabs.Count - 1)), out var tab))
            {
                _focusTab = false;
                await tab.FocusAsync();
            }

            if (_focusRow is { } key && _rows.TryGetValue(key, out var row))
            {
                _focusRow = null;
                await row.FocusAsync();
            }

            if (_focusCounter)
            {
                _focusCounter = false;
                await _counter.FocusAsync();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Microsoft.JSInterop.JSException or TaskCanceledException)
        {
            Log?.LogDebug(ex, "focus could not move inside the code picker");
        }
    }

    private async Task SaveAsync()
    {
        // An untick still reading would land after the draft was sent, so the save sends what it leaves.
        await UntickDoneAsync();

        // aria-disabled rather than disabled while saving: disabling the focused button drops focus to <body>.
        if (_saving)
        {
            return;
        }

        _saving = true;
        Announce(null, error: false);
        try
        {
            var submitted = _choices.ToList();
            var result = await Save([.. submitted.Select(c => c.ToView())]);
            switch (result)
            {
                case { Outcome: DesiredDataOutcome.Saved }:
                    // The picker closes, so the caller, which stays, says it was saved.
                    await Closed.InvokeAsync();
                    break;
                case { Outcome: DesiredDataOutcome.Refused, MaxLength: { } maxLength }:
                    Announce(T.DesiredDataTooLong(maxLength), error: true);
                    break;
                case { Outcome: DesiredDataOutcome.Refused, InvalidIndex: { } index } when index >= 0 && index < submitted.Count:
                    var bad = submitted[index];
                    _notInTree.Add(bad.Value);
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
        catch (MuninExplorerUnauthorizedException ex)
        {
            // Caught like every other write here: uncaught, it would leave the handler and end the circuit.
            Log?.LogWarning(ex, "the code choices of variable {VariableId} were refused: not signed in", VariableId);
            Announce(T.SignInRequiredError, error: true);
        }
        catch (Exception ex)
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
