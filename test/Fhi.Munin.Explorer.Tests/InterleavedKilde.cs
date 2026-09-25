using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// One kilde whose ranks interleave the two kinds at the root and one level down, as each of the
/// three payloads that carry it — so every surface can be put to the same structure.
/// </summary>
/// <remarks>
/// Payload order is deliberately not the ranked order at either level: datasamlinger are listed
/// last-ranked first, so kind-first, payload-order and name-order drawings all fail. Ranked:
/// Registrering, Fødsel (Kontroll, Svangerskap (Ultralyd), Utskriving), Oppfølging.
/// </remarks>
internal static class InterleavedKilde
{
    internal static readonly Guid Kilde = new("5a000000-0000-0000-0000-000000000000");
    internal static readonly Guid Fodsel = new("5ad00000-0000-0000-0000-000000000001");
    internal static readonly Guid Svangerskap = new("5ad00000-0000-0000-0000-000000000002");
    internal static readonly Guid Registrering = new("5ada0000-0000-0000-0000-000000000001");
    internal static readonly Guid Oppfolging = new("5ada0000-0000-0000-0000-000000000002");
    internal static readonly Guid Kontroll = new("5ada0000-0000-0000-0000-000000000003");
    internal static readonly Guid Utskriving = new("5ada0000-0000-0000-0000-000000000004");
    internal static readonly Guid Ultralyd = new("5ada0000-0000-0000-0000-000000000005");

    internal const string KildeName = "Fødselsregisteret";

    /// <summary>Every delkilde and datasamling, parents before their children, in the ranked order.</summary>
    internal static readonly IReadOnlyList<string> RankedPreorder =
        ["Registrering", "Fødsel", "Kontroll", "Svangerskap", "Ultralyd", "Utskriving", "Oppfølging"];

    /// <summary>The same with no ranks: every level keeps payload order, delkilder first.</summary>
    internal static readonly IReadOnlyList<string> UnrankedPreorder =
        ["Fødsel", "Svangerskap", "Ultralyd", "Utskriving", "Kontroll", "Oppfølging", "Registrering"];

    private static int? Rank(bool ranked, int rank) => ranked ? rank : null;

    internal static KildeHierarchy Hierarchy(bool ranked) => new()
    {
        KildeId = Kilde,
        KildeName = KildeName,
        Delkilder =
        [
            new()
            {
                Id = Fodsel, Name = "Fødsel", VariableCount = 3, DisplayOrder = Rank(ranked, 2),
                Children =
                [
                    new()
                    {
                        Id = Svangerskap, Name = "Svangerskap", VariableCount = 1, DisplayOrder = Rank(ranked, 2),
                        Datasamlinger = [new() { Id = Ultralyd, Name = "Ultralyd", VariableCount = 1 }]
                    }
                ],
                Datasamlinger =
                [
                    new() { Id = Utskriving, Name = "Utskriving", VariableCount = 1, DisplayOrder = Rank(ranked, 3) },
                    new() { Id = Kontroll, Name = "Kontroll", VariableCount = 1, DisplayOrder = Rank(ranked, 1) }
                ]
            }
        ],
        DirectDatasamlinger =
        [
            new() { Id = Oppfolging, Name = "Oppfølging", VariableCount = 1, DisplayOrder = Rank(ranked, 3) },
            new() { Id = Registrering, Name = "Registrering", VariableCount = 1, DisplayOrder = Rank(ranked, 1) }
        ]
    };

    internal static KildeDetail Detail(bool ranked) => new()
    {
        Id = Kilde,
        Code = "K_MFR",
        PreferredTerm = KildeName,
        Delkilder =
        [
            new()
            {
                Id = Fodsel, Code = "K_MFR.F", Name = "Fødsel", DisplayOrder = Rank(ranked, 2),
                Children =
                [
                    new()
                    {
                        Id = Svangerskap, Code = "K_MFR.F.S", Name = "Svangerskap", DisplayOrder = Rank(ranked, 2),
                        Datasamlinger = [Collection(Ultralyd, "Ultralyd", null)]
                    }
                ],
                Datasamlinger =
                [
                    Collection(Utskriving, "Utskriving", Rank(ranked, 3)),
                    Collection(Kontroll, "Kontroll", Rank(ranked, 1))
                ]
            }
        ],
        Datasamlinger =
        [
            Collection(Oppfolging, "Oppfølging", Rank(ranked, 3)),
            Collection(Registrering, "Registrering", Rank(ranked, 1))
        ]
    };

    internal static FilterOptions Filters(bool ranked) => new()
    {
        Kilder = [new() { Id = Kilde, Name = KildeName, ShortName = "", Count = 5 }],
        Delkilder =
        [
            Delkilde(Fodsel, "Fødsel", null, Rank(ranked, 2)),
            Delkilde(Svangerskap, "Svangerskap", Fodsel, Rank(ranked, 2))
        ],
        Datasamlinger =
        [
            Datasamling(Oppfolging, "Oppfølging", null, Rank(ranked, 3)),
            Datasamling(Registrering, "Registrering", null, Rank(ranked, 1)),
            Datasamling(Ultralyd, "Ultralyd", Svangerskap, null),
            Datasamling(Utskriving, "Utskriving", Fodsel, Rank(ranked, 3)),
            Datasamling(Kontroll, "Kontroll", Fodsel, Rank(ranked, 1))
        ]
    };

    /// <summary>K_KK as the detail payload carries it, each wave holding one questionnaire when asked.</summary>
    /// <remarks>A wave with no datasamling of its own draws no group in the kilde explorer's panel, so
    /// the panel's order can only be read off waves that hold one.</remarks>
    internal static KildeDetail KildeKk(SiblingScope scope, Guid id, bool questionnaires = false) => new()
    {
        Id = id,
        Code = "K_KK",
        PreferredTerm = "Kvinner og kreft",
        Delkilder =
        [
            .. scope.Delkilder.Select(wave => new KildeDelkilde
            {
                Id = wave.Id,
                Name = wave.Name,
                DisplayOrder = wave.DisplayOrder,
                Datasamlinger = [.. Questionnaire(wave.Name, questionnaires)]
            })
        ],
        Datasamlinger = [.. scope.Datasamlinger.Select(d => Collection(d.Id, d.Name, d.DisplayOrder))]
    };

    private static IEnumerable<KildeDatasamling> Questionnaire(string wave, bool wanted)
    {
        if (wanted)
        {
            yield return Collection(Guid.NewGuid(), $"{wave} questionnaire", 1);
        }
    }

    private static KildeDatasamling Collection(Guid id, string name, int? rank) =>
        new() { Id = id, Name = name, DisplayOrder = rank, VariableCount = 1 };

    private static DelkildeFacet Delkilde(Guid id, string name, Guid? parent, int? rank) =>
        new() { Id = id, Name = name, KildeId = Kilde, ParentDelkildeId = parent, Count = 1, DisplayOrder = rank };

    private static DatasamlingFacet Datasamling(Guid id, string name, Guid? delkilde, int? rank) =>
        new() { Id = id, Name = name, KildeId = Kilde, DelkildeId = delkilde, Count = 1, DisplayOrder = rank };
}
