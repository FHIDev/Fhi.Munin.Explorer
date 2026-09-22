using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.State;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The host's lede under the explorer title, on Kelda and Runa and on the wrapper each is mounted
/// through (Fhi.Metadata-35w0p.32).
/// </summary>
/// <remarks>
/// Stiler keys on <c>.munin-explorer &gt; h2 + .munin-explorer__lede</c> behind a <c>:has()</c>, so
/// class, order and absence are the contract. Mounted by string name, as a CMS host mounts.
/// </remarks>
public class ExplorerLedeTest : ExplorerTestContext
{
    private const string Parameter = "Lede";

    private const string Lede = "Et avsnitt verten har skrevet selv.";

    private sealed class OneKildeClient : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>(
                [new KildeSummary { Id = Guid.NewGuid(), Code = "K_ALS", Name = "Als registeret" }]);
    }

    private static readonly Type[] MountTypes =
        [typeof(KildeSearch), typeof(KildeExplorer), typeof(VariableSearch), typeof(VariableExplorer)];

    public static TheoryData<Type> Mounts() => [.. MountTypes];

    private IRenderedComponent<IComponent> Mount(Type component, bool passLede, string? lede = null)
    {
        Services.AddSingleton<IMuninExplorerClient>(new OneKildeClient());
        Services.AddScoped<VariableListState>();
        SetRendererInfo(new RendererInfo("Server", true));

        return Render(builder =>
        {
            builder.OpenComponent(0, component);
            builder.AddComponentParameter(1, "Language", "no");

            if (passLede)
            {
                builder.AddComponentParameter(2, Parameter, lede);
            }

            builder.CloseComponent();
        });
    }

    private static IElement Title(IRenderedComponent<IComponent> cut) =>
        Assert.Single(cut.FindAll("section.munin-explorer > h2"));

    private static bool Declares(Type mount) =>
        mount.GetProperty(Parameter, BindingFlags.Public | BindingFlags.Instance) is { } property
        && property.IsDefined(typeof(ParameterAttribute), inherit: false)
        && property.PropertyType == typeof(string);

    [Theory]
    [MemberData(nameof(Mounts))]
    public void Mount_WhenItIsRead_ThenItDeclaresTheLedeAsAStringParameter(Type mount)
    {
        Assert.True(Declares(mount), $"{mount.Name} declares no public string [Parameter] {Parameter}.");
    }

    [Theory]
    [MemberData(nameof(Mounts))]
    public void Lede_WhenTheHostPassesNothing_ThenNoElementFollowsTheTitle(Type mount)
    {
        // Declared first, so this cannot pass on a component that has never heard of the lede and
        // therefore trivially draws none.
        Assert.True(Declares(mount), $"{mount.Name} declares no public string [Parameter] {Parameter}.");

        var cut = Mount(mount, passLede: false);

        Assert.Empty(cut.FindAll(".munin-explorer__lede"));
        Assert.NotEqual("P", Title(cut).NextElementSibling?.TagName);
    }

    public static TheoryData<Type, string?> MountsAndBlanks()
    {
        var data = new TheoryData<Type, string?>();
        foreach (var mount in MountTypes)
        {
            foreach (var blank in new[] { null, "", "   \n\t" })
            {
                data.Add(mount, blank);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(MountsAndBlanks))]
    public void Lede_WhenTheHostPassesNullOrBlank_ThenNoElementFollowsTheTitle(Type mount, string? blank)
    {
        // An empty <p> would still open Stiler's :has()-gated grid row and push the results down.
        var cut = Mount(mount, passLede: true, lede: blank);

        Assert.Empty(cut.FindAll(".munin-explorer__lede"));
        Assert.NotEqual("P", Title(cut).NextElementSibling?.TagName);
    }

    [Theory]
    [MemberData(nameof(Mounts))]
    public void Lede_WhenTheHostPassesText_ThenItIsTheParagraphDirectlyAfterTheTitle(Type mount)
    {
        var cut = Mount(mount, passLede: true, lede: Lede);

        var lede = Title(cut).NextElementSibling;

        Assert.NotNull(lede);
        Assert.Equal("P", lede.TagName);
        Assert.Equal(["munin-explorer__lede"], lede.ClassList);
        Assert.Equal(Lede, lede.TextContent);
        Assert.True(lede.ParentElement?.ClassList.Contains("munin-explorer"),
            "The lede must be a direct child of .munin-explorer, where Stiler's grid places it.");
        Assert.Single(cut.FindAll(".munin-explorer__lede"));
    }

    [Theory]
    [MemberData(nameof(Mounts))]
    public void Lede_WhenTheHostPassesMarkup_ThenItIsDrawnAsText(Type mount)
    {
        var cut = Mount(mount, passLede: true, lede: "<b>fet</b>");

        var lede = cut.Find(".munin-explorer__lede");

        Assert.Equal("<b>fet</b>", lede.TextContent);
        Assert.Empty(lede.Children);
    }
}
