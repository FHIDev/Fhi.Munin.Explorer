using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Tests;

public class VariableCodeLookupStatusLangHeldOutTest : ExplorerTestContext
{
    private sealed class HeldClient : EmptyMuninExplorerClient, IMuninExplorerClient
    {
        public TaskCompletionSource<VariableDetail?> Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<VariableDetail?> GetVariableByCodeAsync(string code, CancellationToken cancellationToken = default) => Gate.Task;
    }

    [Fact]
    public void Arrive_WhenAnEnglishReaderWaitsForTheCodeLookup_ThenTheStatusIsMarkedEnglish()
    {
        // WCAG 3.1.2: the English status must not inherit the host page's Norwegian lang.
        var client = new HeldClient();
        Services.AddSingleton<IMuninExplorerClient>(client);
        Services.AddSingleton<IJSRuntime>(new HistoryRecorder(JSInterop.JSRuntime));
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/variabler?variabel=V_HKR.KJONN");

        var cut = Render<VariableExplorer>(b => b.Add(c => c.Language, "en"));

        var status = cut.WaitForElement("[role=status]");
        var marked = status.Closest("[lang]");
        Assert.Equal("en", marked?.GetAttribute("lang"));
    }
}
