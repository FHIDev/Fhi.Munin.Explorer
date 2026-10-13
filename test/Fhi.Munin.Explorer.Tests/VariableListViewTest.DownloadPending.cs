using Bunit;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components.Web;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>The saved list's «Last ned» says that it is working (Fhi.Metadata-ht2gj).</summary>
public partial class VariableListViewTest
{
    private static AngleSharp.Dom.IElement DownloadGroup(IRenderedComponent<Fhi.Munin.Explorer.Blazor.VariableListView> cut) =>
        cut.Find($"{ActionRow} [role=group]");

    [Theory]
    [InlineData("Last ned som Excel", "Laster ned Excel …", "Last ned som CSV")]
    [InlineData("Last ned som CSV", "Laster ned CSV …", "Last ned som Excel")]
    public async Task Download_WhileTheListIsBeingExported_ThenThePageSaysSoAndASecondPressDoesNothing(
        string pressed, string working, string other)
    {
        var held = new TaskCompletionSource<ExportedList?>();
        var client = new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { HoldExport = held };
        var cut = RenderView(client);
        await cut.InvokeAsync(() => cut.Find(DownloadToggle).Click(new MouseEventArgs { Detail = 0 }));
        Assert.Equal("", DownloadGroup(cut).QuerySelector("[role=status]")!.TextContent.Trim());

        await cut.InvokeAsync(() => KeyboardPress(cut, pressed));

        cut.WaitForAssertion(() => Assert.Equal("Laster ned …", cut.Find(DownloadToggle).TextContent.Trim()));
        Assert.Equal("true", cut.Find(DownloadToggle).GetAttribute("aria-busy"));

        var buttons = DownloadGroup(cut).QuerySelectorAll("button");
        var busy = buttons.Single(b => b.TextContent.Trim() == working);
        Assert.Equal("true", busy.GetAttribute("aria-busy"));
        Assert.Equal("true", busy.GetAttribute("aria-disabled"));
        var idle = buttons.Single(b => b.TextContent.Trim() == other);
        Assert.Null(idle.GetAttribute("aria-busy"));
        Assert.Equal("true", idle.GetAttribute("aria-disabled"));
        Assert.True(DownloadGroup(cut).QuerySelector("input[type=checkbox]")!.HasAttribute("disabled"));
        Assert.Equal(working, DownloadGroup(cut).QuerySelector("[role=status]")!.TextContent.Trim());

        await cut.InvokeAsync(() => idle.Click(new MouseEventArgs { Detail = 0 }));
        Assert.Equal(1, client.ExportCalls);

        held.SetResult(new ExportedList([1, 2, 3], "text/csv", "variabelliste.csv"));

        cut.WaitForAssertion(() => Assert.Equal("Last ned", cut.Find(DownloadToggle).TextContent.Trim()));
        Assert.Null(cut.Find(DownloadToggle).GetAttribute("aria-busy"));
        Assert.Equal("Nedlastingen er klar.", DownloadGroup(cut).QuerySelector("[role=status]")!.TextContent.Trim());
        Assert.False(DownloadGroup(cut).QuerySelector("input[type=checkbox]")!.HasAttribute("disabled"));
        Assert.DoesNotContain("Kunne ikke laste ned", cut.Markup);
    }

    [Fact]
    public async Task Download_WhenTheListCannotBeExported_ThenTheWaitingEndsAndTheFailureIsSaid()
    {
        var held = new TaskCompletionSource<ExportedList?>();
        var cut = RenderView(new ListClient(Item("Alder ved diagnose", "V_BDR.ALDER")) { HoldExport = held });
        await cut.InvokeAsync(() => cut.Find(DownloadToggle).Click(new MouseEventArgs { Detail = 0 }));
        await cut.InvokeAsync(() => KeyboardPress(cut, "Last ned som Excel"));
        cut.WaitForAssertion(() => Assert.Equal("Laster ned …", cut.Find(DownloadToggle).TextContent.Trim()));

        held.SetException(new InvalidOperationException("the API answered 500"));

        cut.WaitForAssertion(() => Assert.Contains("Kunne ikke laste ned", cut.Markup));
        Assert.Equal("Last ned", cut.Find(DownloadToggle).TextContent.Trim());
        Assert.Null(cut.Find(DownloadToggle).GetAttribute("aria-busy"));
        Assert.Equal("", DownloadGroup(cut).QuerySelector("[role=status]")!.TextContent.Trim());
    }
}
