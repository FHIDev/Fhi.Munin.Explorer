using System.Reflection;
using System.Text.Json;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

// Held-out regressions for avansert søk (Fhi.Metadata-ipok2.2): a failed field load, a double chip press, a stale row remove.
public class AdvancedSearchHeldOutTest : ExplorerTestContext
{
    private static readonly IReadOnlyList<ConditionField> Fields =
        JsonSerializer.Deserialize<IReadOnlyList<ConditionField>>(TestData.Read("vilkar.json"), MuninExplorerClient.Json)!;

    private sealed class GatedClient : EmptyMuninExplorerClient
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _searchCalls;

        public int FailFieldCalls { get; init; }

        public bool GateLaterSearches { get; init; }

        public List<VariableFilter?> Searches { get; } = [];

        public int FieldCalls { get; private set; }

        public void Release() => _gate.TrySetResult();

        public override Task<IReadOnlyList<ConditionField>> GetConditionFieldsAsync(CancellationToken cancellationToken = default)
        {
            FieldCalls++;
            return FieldCalls <= FailFieldCalls
                ? Task.FromException<IReadOnlyList<ConditionField>>(new HttpRequestException("429"))
                : Task.FromResult(Fields);
        }

        public override async Task<Page<VariableSummary>> SearchVariablesAsync(
            string? search, VariableFilter? filter = null, int page = 1, int pageSize = 25,
            SortField sort = SortField.Default, SortDirection direction = SortDirection.Ascending,
            CancellationToken cancellationToken = default)
        {
            Searches.Add(filter);
            if (GateLaterSearches && Interlocked.Increment(ref _searchCalls) > 1)
            {
                await _gate.Task;
            }

            return new Page<VariableSummary>
            {
                Items = [new VariableSummary { Id = Guid.NewGuid(), Code = "V_A", PreferredTerm = "Blodtrykk" }],
                TotalCount = 1, PageNumber = 1, Size = 25, TotalPages = 1
            };
        }
    }

    private IRenderedComponent<VariableSearch> RenderWith(GatedClient client, string[] conditions, Action<VariableFilter>? changed = null)
    {
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddSingleton(new AdvancedSearchSwitch(true));
        var filter = new VariableFilter { Conditions = [.. conditions.Select(Condition)] };
        return Render<VariableSearch>(b =>
        {
            b.Add(c => c.Filter, filter);
            if (changed is not null)
            {
                b.Add(c => c.FilterChanged, changed);
            }
        });
    }

    private static SearchCondition Condition(string text) =>
        SearchCondition.TryParse(text, out var condition) ? condition : throw new ArgumentException(text);

    private static IElement? Toggle(IRenderedComponent<VariableSearch> cut) =>
        cut.FindAll("button[aria-expanded]").FirstOrDefault(b => b.TextContent.StartsWith("Avansert søk", StringComparison.Ordinal));

    [Fact]
    public void Render_WhenTheFieldLoadFailsAndTheLinkCarriesConditions_ThenTheHostIsNotToldTheyWereDropped()
    {
        var reported = new List<VariableFilter>();
        var client = new GatedClient { FailFieldCalls = 1 };
        var cut = RenderWith(client, ["navn:inneholder:blod"], reported.Add);

        cut.WaitForAssertion(() => Assert.NotEmpty(client.Searches));
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));

        Assert.DoesNotContain(reported, filter => filter.Conditions.Count == 0);
    }

    [Fact]
    public void Open_WhenTheFirstFieldLoadFailed_ThenTheFormAsksForTheFieldsAgain()
    {
        var client = new GatedClient { FailFieldCalls = 1 };
        var cut = RenderWith(client, ["navn:inneholder:blod"]);
        cut.WaitForAssertion(() => Assert.NotEmpty(client.Searches));
        cut.WaitForAssertion(() => Assert.NotNull(Toggle(cut)));
        var before = client.FieldCalls;

        Toggle(cut)!.Click();

        Assert.Equal(before + 1, client.FieldCalls);
    }

    [Fact]
    public async Task RemoveRow_WhenTheSameHandlerRunsTwice_ThenTheSecondRunIsANoOp()
    {
        var client = new GatedClient();
        var cut = RenderWith(client, ["navn:inneholder:a", "navn:inneholder:b"]);
        cut.WaitForAssertion(() => Assert.NotEmpty(client.Searches));
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Endre avansert søk").Click();
        cut.WaitForAssertion(() => Assert.Contains("Fjern betingelse 2", cut.Markup, StringComparison.Ordinal));
        var stale = HandlerOf( "Fjern betingelse 2");

        await cut.InvokeAsync(() => stale.InvokeAsync(new MouseEventArgs()));
        var second = await Record.ExceptionAsync(() => cut.InvokeAsync(() => stale.InvokeAsync(new MouseEventArgs())));

        Assert.Null(second);
    }

    // The delegate Blazor Server would still hold for a button the first press has already removed.
#pragma warning disable BL0006 // Only the render tree holds it.
    private EventCallback HandlerOf(string buttonText)
    {
        var method = typeof(Microsoft.AspNetCore.Components.RenderTree.Renderer)
            .GetMethod("GetCurrentRenderTreeFrames", BindingFlags.Instance | BindingFlags.NonPublic)!;
        for (var id = 0; id < 500; id++)
        {
            object range;
            try
            {
                range = method.Invoke(Renderer, [id])!;
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            var frames = (RenderTreeFrame[])range.GetType().GetField("Array")!.GetValue(range)!;
            var count = (int)range.GetType().GetField("Count")!.GetValue(range)!;
            var text = Enumerable.Range(0, count).Where(i =>
                frames[i].FrameType == RenderTreeFrameType.Text && frames[i].TextContent == buttonText).ToList();
            if (text.Count == 1)
            {
                var click = Enumerable.Range(0, text[0]).Reverse().First(i =>
                    frames[i].FrameType == RenderTreeFrameType.Attribute && frames[i].AttributeName == "onclick");
                return (EventCallback)frames[click].AttributeValue;
            }
        }

        throw new InvalidOperationException(buttonText);
#pragma warning restore BL0006
    }
}
