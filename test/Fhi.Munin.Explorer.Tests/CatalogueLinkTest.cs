using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>Links to another site open in a new window and say so; the page's own do neither (Fhi.Metadata-c8lgk).</summary>
public class CatalogueLinkTest : ExplorerTestContext
{
    private IRenderedComponent<CatalogueLink> Link(string href, string? reader = null)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("http://localhost/kildeutforsker/?kilde=1");

        return reader is null
            ? Render<CatalogueLink>(p => p.Add(c => c.Href, href).AddChildContent("lenke"))
            : Render<CascadingValue<string>>(p => p
                .Add(c => c.Name, CatalogueLink.ReaderName)
                .Add(c => c.Value, reader)
                .AddChildContent<CatalogueLink>(l => l.Add(c => c.Href, href).AddChildContent("lenke")))
              .FindComponent<CatalogueLink>();
    }

    [Theory]
    [InlineData("https://lovdata.no/lov/2014-06-20-43")]
    [InlineData("http://data.europa.eu/eli/reg/2025/327/oj")]
    public void Render_WhenTheLinkIsToAnotherHost_ThenItOpensInANewWindowAndSaysSo(string href)
    {
        var a = Link(href).Find("a");

        Assert.Equal("_blank", a.GetAttribute("target"));
        Assert.Equal("noopener noreferrer", a.GetAttribute("rel"));
        Assert.Equal("lenke (åpnes i nytt vindu)", a.TextContent);
        Assert.Equal(" (åpnes i nytt vindu)", a.QuerySelector("span.screenreader-only")?.TextContent);
    }

    [Theory]
    [InlineData("http://localhost/variabelutforsker/?kildeIds=1")]
    [InlineData("/variabelutforsker/")]
    [InlineData("#munin-explorer-section-om-kilden")]
    [InlineData("mailto:post@fhi.no")]
    public void Render_WhenTheLinkStaysOnThisSiteOrIsNotTheWeb_ThenItIsAPlainLink(string href)
    {
        var a = Link(href).Find("a");

        Assert.Null(a.GetAttribute("target"));
        Assert.Empty(a.QuerySelectorAll(".screenreader-only"));
        Assert.Equal("lenke", a.TextContent);
    }

    [Fact]
    public void Render_WhenTheReaderIsEnglish_ThenTheWindowIsAnnouncedInEnglish()
    {
        var a = Link("https://lovdata.no/", reader: "en").Find("a");

        Assert.Equal("lenke (opens in a new window)", a.TextContent);
        Assert.Equal("en", a.QuerySelector(".screenreader-only")?.GetAttribute("lang"));
    }
}
