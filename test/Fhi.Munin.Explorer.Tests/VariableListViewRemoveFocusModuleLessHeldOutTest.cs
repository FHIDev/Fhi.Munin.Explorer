using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

// A host serving no module must still get the focus move after «Fjern», by Blazor's own element focus (Fhi.Metadata-emsc0.3.24).
public class VariableListViewRemoveFocusModuleLessHeldOutTest : ExplorerTestContext
{
    private const string ElementFocus = "Blazor._internal.domWrapper.focus";

    private static readonly Guid ListId = new("11111111-1111-1111-1111-111111111111");

    private sealed class ThreeRowClient : EmptyMuninExplorerClient
    {
        private readonly List<VariableListItem> _items = [.. new[] { "Alder ved diagnose", "Kjønn", "Bosted" }.Select((n, i) => Item(n, $"V_{i}"))];

        public override Task<IReadOnlyList<VariableList>> GetMyListsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VariableList>>(
                [new VariableList { Id = ListId, Name = "Mine hjertevariabler", VariableCount = _items.Count }]);

        public override Task<bool> RemoveVariablesFromMyListAsync(
            Guid id, IReadOnlyCollection<Guid> variableIds, CancellationToken cancellationToken = default)
        {
            _items.RemoveAll(i => variableIds.Contains(i.VariableId));
            return Task.FromResult(true);
        }

        public override Task<Page<VariableListItem>?> GetMyListVariablesAsync(
            Guid id, int page = 1, int pageSize = 100, IReadOnlyCollection<Guid>? kildeIds = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Page<VariableListItem>?>(new Page<VariableListItem>
            {
                Items = [.. _items],
                TotalCount = _items.Count,
                PageNumber = 1,
                Size = pageSize,
                TotalPages = 1
            });
    }

    /// <summary>A host serving no module: the import is refused, and only Blazor's own element focus answers.</summary>
    private sealed class ModuleLessRuntime : IJSRuntime
    {
        private int _focuses;

        public int Focuses => Volatile.Read(ref _focuses);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier != ElementFocus)
            {
                throw new JSException("404");
            }

            Interlocked.Increment(ref _focuses);
            return default;
        }
    }

    private static VariableListItem Item(string name, string code) =>
        new()
        {
            VariableId = Guid.NewGuid(),
            AddedAt = DateTimeOffset.UtcNow,
            VariableName = name,
            VariableCode = code,
            KildeName = "Als registeret",
            KildeShortName = "ALS",
            DatasamlingName = "Inklusjon",
            VariabelgruppeName = "Ikke oppgitt",
            DataType = "2"
        };

    private static List<AngleSharp.Dom.IElement> RemoveButtons(IRenderedComponent<VariableListView> cut) =>
        [.. cut.FindAll("tbody tr td:last-child button")];

    [Fact]
    public async Task Remove_WhenTheModuleCannotBeImported_ThenFocusIsMovedByDotNetOntoARemainingFjern()
    {
        var runtime = new ModuleLessRuntime();
        Services.AddSingleton<IJSRuntime>(runtime);
        Services.AddSingleton(new CodePickerSwitch(true));
        Services.AddSingleton<IMuninExplorerClient>(new ThreeRowClient());
        Services.AddScoped<VariableListState>();
        var cut = Render<VariableListView>(p => p.Add(c => c.IsAuthenticated, true));
        cut.WaitForAssertion(() => Assert.Equal(3, RemoveButtons(cut).Count));
        var before = runtime.Focuses;

        await cut.InvokeAsync(() => RemoveButtons(cut)[1].Click());

        cut.WaitForAssertion(() => Assert.Equal(2, RemoveButtons(cut).Count));
        cut.WaitForAssertion(() => Assert.Equal(before + 1, runtime.Focuses));
    }
}
