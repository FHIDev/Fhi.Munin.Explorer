using System.Text.Json;
using System.Text.Json.Serialization;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Every field the API sends has somewhere to land.
/// </summary>
/// <remarks>
/// Deserialising normally ignores a property the contract does not know about, so a field added in
/// Munin — or one missed when these records were written — disappears silently. Reading the same
/// captured responses with unmapped members disallowed turns that into a failing test. The
/// exception message names the offending property, which is the fix.
/// <para>
/// Re-capture the files under <c>Testdata/</c> from the live API when Munin's explorer changes;
/// this test is what tells you the contracts have to change too.
/// </para>
/// </remarks>
public class KontraktdekningTest
{
    private static readonly JsonSerializerOptions Strengt = new(MuninExplorerClient.Json)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    private static void Dekker<T>(string fixtur) =>
        Assert.NotNull(JsonSerializer.Deserialize<T>(Testdata.Les(fixtur), Strengt));

    [Fact]
    public void Variabelsøk_MotEkteRespons_ThenErAlleFeltDekket() =>
        Dekker<Side<VariabelSammendrag>>("variables.json");

    [Fact]
    public void Filtre_MotEkteRespons_ThenErAlleFeltDekket() =>
        Dekker<Filtervalg>("filters.json");

    [Fact]
    public void Kildeliste_MotEkteRespons_ThenErAlleFeltDekket() =>
        Dekker<IReadOnlyList<KildeSammendrag>>("kilder.json");

    [Fact]
    public void Kildedetalj_MotEkteRespons_ThenErAlleFeltDekket() =>
        Dekker<KildeDetalj>("kilde.json");

    [Fact]
    public void KildedetaljMedDelkilder_MotEkteRespons_ThenErAlleFeltDekket() =>
        // Most kilder have no delkilder at all, so the nested branch of the contract would
        // otherwise never be exercised. This one is a study series with one delkilde per wave.
        Dekker<KildeDetalj>("kilde-med-delkilder.json");

    [Fact]
    public void Kildehierarki_MotEkteRespons_ThenErAlleFeltDekket() =>
        Dekker<KildeHierarki>("hierarchy.json");

    [Fact]
    public void Datasamlingdetalj_MotEkteRespons_ThenErAlleFeltDekket() =>
        Dekker<DatasamlingDetalj>("datasamling.json");

    [Fact]
    public void Variabeldetalj_MotEkteRespons_ThenErAlleFeltDekket() =>
        Dekker<VariabelDetalj>("variable.json");

    [Fact]
    public void Tidslinje_MotEkteRespons_ThenErAlleFeltDekket() =>
        Dekker<IReadOnlyList<Variabelversjon>>("timeline.json");
}
