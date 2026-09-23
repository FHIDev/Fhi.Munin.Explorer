using Bunit;
using Fhi.Munin.Explorer.Blazor;
using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using static Fhi.Munin.Explorer.Tests.KildeColumns;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// The kilder table's header handles, <c>munin-explorer-kilder-header__&lt;key&gt;</c> (Fhi.Metadata-35w0p.74).
/// </summary>
/// <remarks>
/// Stiler keys the sticky head on which wide columns are shown. The scroll box's count cannot say
/// that: Kode+Dataansvarlig+Databehandler and the default three are both <c>--cols-8</c> and 226px
/// apart. A missing or misspelt handle draws nothing wrong here and picks the wrong threshold there.
/// </remarks>
public class KildeHeaderHandlesTest : ExplorerTestContext
{
    private sealed class FakeClient(params KildeSummary[] kilder) : EmptyMuninExplorerClient
    {
        public override Task<IReadOnlyList<KildeSummary>> GetKilderAsync(
            string? search = null, string? kildeType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<KildeSummary>>(kilder);
    }

    private static readonly KildeSummary Als = new()
    {
        Id = Guid.NewGuid(),
        Code = "K_ALS",
        Name = "Als registeret",
        Kildetype = "sentraltHelseregister",
        IsActive = true,
        DatasamlingCount = 1,
        TotalVariables = 10,
    };

    private IRenderedComponent<KildeSearch> RenderWith(bool selectable)
    {
        Services.AddSingleton<IMuninExplorerClient>(new FakeClient(Als));

        return selectable
            ? Render<KildeSearch>(b => b.Add(c => c.ExploreVariablesRequested,
                EventCallback.Factory.Create<IReadOnlyList<Guid>>(this, _ => { })))
            : Render<KildeSearch>();
    }

    [Fact]
    public void Headers_WhenSelectableWithEveryColumnOn_ThenEachCellCarriesExactlyItsOwnHandle()
    {
        // Written out rather than read off ColumnKeys: these strings are what Stiler's selectors
        // spell, so a renamed key has to fail here before it silently unkeys a threshold there.
        var cut = RenderWith(selectable: true);

        TurnEveryColumnOn(cut);

        Assert.Equal(
            [
                ["expand"], ["select"], ["navn"], ["kode"], ["kildetype"], ["status"],
                ["dataansvarlig"], ["databehandler"], ["grad"], ["gyldighetsperiode"],
                ["delkilder"], ["datasamlinger"], ["variabler"], ["andelKodeverk"],
                ["andelStatistikk"], ["opprettet"], ["importert"], ["sistEndret"],
            ],
            HeaderKeys(cut));
    }

    [Fact]
    public void Headers_WhenTheDefaultColumnsAreOn_ThenOnlyTheirHandlesAppear()
    {
        var cut = RenderWith(selectable: false);

        Assert.Equal(
            [["expand"], ["navn"], ["kildetype"], ["status"], ["datasamlinger"], ["variabler"], ["opprettet"]],
            HeaderKeys(cut));
    }

    [Fact]
    public void Headers_WhenEveryColumnIsOn_ThenEveryColumnKeyHasAHandle()
    {
        // One source of truth: the optional handles are built from ColumnKey, so a column added to
        // the enum gets one without anyone writing it down — and this fails if it does not.
        var cut = RenderWith(selectable: false);

        TurnEveryColumnOn(cut);

        var drawn = HeaderKeys(cut).SelectMany(keys => keys).ToHashSet(StringComparer.Ordinal);

        Assert.All(KildeSearch.ColumnKeys, key => Assert.Contains(key, drawn));

        // And the orphan guard's exemption covers every one drawn, so it never reports a handle.
        Assert.Subset(HeaderHandles.ToHashSet(),
                      drawn.Select(KildeSearch.HeaderClass).ToHashSet());
    }

    [Fact]
    public void Headers_WhenEveryColumnIsOn_ThenTheCellsKeepTheClassesTheyAlreadyWore()
    {
        var cut = RenderWith(selectable: true);

        TurnEveryColumnOn(cut);

        var classes = cut.FindAll(".munin-explorer-kilder thead th")
            .ToDictionary(
                th => th.ClassList.Single(c => c.StartsWith(KilderHeaderStem, StringComparison.Ordinal))[KilderHeaderStem.Length..],
                th => th.ClassList.Where(c => !c.StartsWith(KilderHeaderStem, StringComparison.Ordinal)).ToArray());

        Assert.Equal(["munin-explorer-kilder__expand"], classes["expand"]);
        Assert.Equal(["munin-explorer-kilder__select"], classes["select"]);

        foreach (var key in new[] { "delkilder", "datasamlinger", "variabler", "andelKodeverk", "andelStatistikk" })
        {
            Assert.Equal(["munin-explorer-kilder__count"], classes[key]);
        }

        foreach (var key in new[] { "navn", "kode", "kildetype", "status", "dataansvarlig", "databehandler",
                                    "grad", "gyldighetsperiode", "opprettet", "importert", "sistEndret" })
        {
            Assert.Empty(classes[key]);
        }
    }
}
