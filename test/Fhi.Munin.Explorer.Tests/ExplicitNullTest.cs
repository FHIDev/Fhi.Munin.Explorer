using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Fhi.Munin.Explorer.Client;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Every property this package deserialises has a decided answer to <c>"key": null</c>.
/// </summary>
/// <remarks>
/// Three mechanisms decide it, and the sweep below is what stops a fourth kind of property from
/// arriving with no answer at all: <c>NullAsEmptyCollections</c> for collections,
/// <c>NullAsEmptyStrings</c> for the strings the contract declares non-nullable, and
/// <c>System.Text.Json</c>'s own refusal for the non-nullable value types — deliberately kept, since
/// <c>0</c>, <c>false</c>, <c>Guid.Empty</c> and the zeroth enum member are each a claim the payload
/// did not make. AGENTS.md, "What an explicit null does", carries the argument. (Fhi.Metadata-o355u)
/// <para>
/// A property outside all three — a nested contract record declared non-nullable, say — takes the
/// null over its initialiser in silence and throws at the first read while rendering, which is past
/// the try/catch around the fetch and takes a Blazor Server circuit with it. That is the failure
/// this repository has now shipped twice, and it is invisible at the declaration both times.
/// </para>
/// </remarks>
public class ExplicitNullTest
{
    [Fact]
    public void Contracts_WhenSwept_ThenEveryDeserialisedPropertyHasADecidedAnswerToAnExplicitNull()
    {
        var nullability = new NullabilityInfoContext();
        var collections = new NullAsEmptyCollections();

        var undecided = DeserialisedProperties()
            .Where(property => Classify(property, nullability, collections) is null)
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name} is {property.PropertyType.Name}")
            .ToList();

        Assert.True(
            undecided.Count == 0,
            "An explicit null lands over the initialiser on these and is found later, while "
            + "rendering, where the declaration said it could not happen. Either annotate them "
            + "nullable or widen a mechanism to cover them:"
            + Environment.NewLine + string.Join(Environment.NewLine, undecided.Select(line => "  " + line)));
    }

    [Fact]
    public void DeserialisedProperties_WhenGathered_ThenThereAreSomeToCheck() =>
        // The sweep passes just as happily on nothing at all, which is what a namespace rename or an
        // attribute convention that stopped being followed would leave it looking at.
        Assert.NotEmpty(DeserialisedProperties());

    /// <summary>The contract types the sweep is allowed not to see, and why.</summary>
    /// <remarks>
    /// None of them is read from JSON. <see cref="ExplorerUrlState"/> and <see cref="VariableFilter"/>
    /// are built from a query string, <see cref="ExportedList"/> from a response the client reads as
    /// bytes and headers, and <see cref="DesiredDataResult"/> is assembled from status codes — the
    /// body behind it is the client's internal <c>DesiredDataRefusal</c>, which is swept.
    /// <para>
    /// Named rather than inferred because the sweep identifies a contract by its
    /// <c>[JsonPropertyName]</c> attributes, so a type carrying none drops out of it in silence —
    /// and "no attributes" is what a type looks like both when it is not deserialised and when its
    /// author forgot the convention. This list is where that difference has to be stated out loud.
    /// </para>
    /// </remarks>
    private static readonly string[] NotDeserialised =
    [
        nameof(ExplorerUrlState), nameof(VariableFilter), nameof(ExportedList), nameof(DesiredDataResult)
    ];

    [Fact]
    public void Contracts_WhenATypeCarriesNoJsonPropertyName_ThenItIsOneTheSweepIsMeantToMiss()
    {
        var missed = typeof(IMuninExplorerClient).Assembly
            .GetExportedTypes()
            .Where(type => type.Namespace == typeof(IMuninExplorerClient).Namespace
                           && !type.IsInterface && !type.IsEnum
                           && !typeof(Exception).IsAssignableFrom(type)
                           && !IsDeserialised(type))
            .Select(type => type.Name)
            .Except(NotDeserialised)
            .ToList();

        Assert.True(
            missed.Count == 0,
            "These contract types carry no [JsonPropertyName] anywhere, so the null sweep does not "
            + "see them at all. Either they are not deserialised — say so by listing them in "
            + "NotDeserialised — or they are, and the missing attributes are the bug:"
            + Environment.NewLine + string.Join(Environment.NewLine, missed.Select(line => "  " + line)));
    }

    // ------------------------------------------------------- the three mechanisms, demonstrated once

    [Fact]
    public async Task GetKilderAsync_WhenOneRowHasANullName_ThenThatRowIsBlankAndTheListSurvives()
    {
        // Reads through the real client and its real options, so the whole chain is exercised: this
        // is the failure that used to pass deserialisation and throw at render instead.
        var kilder = await WithJson("""
            [{"id":"8ec4c2c4-662d-47a5-a946-f1086a014070","code":"K_ALS","navn":null,"kortNavn":null},
             {"id":"1d1c0f21-0f2e-4d1a-9a0f-000000000002","code":"K_MFR","navn":"Fødselsregisteret"}]
            """).GetKilderAsync();

        Assert.Equal(2, kilder.Count);
        Assert.Equal("", kilder[0].Name);
        Assert.Equal("K_ALS", kilder[0].Code);
        Assert.Equal("Fødselsregisteret", kilder[1].Name);
    }

    [Fact]
    public async Task GetKilderAsync_WhenARowHasANullShortName_ThenItStaysNullRatherThanBecomingBlank()
    {
        // The half a converter on the options could not have kept: kortNavn is declared string?, and
        // the components fall back on the full name when it is null rather than drawing nothing.
        var kilder = await WithJson("""
            [{"id":"8ec4c2c4-662d-47a5-a946-f1086a014070","navn":"Als registeret","kortNavn":null}]
            """).GetKilderAsync();

        Assert.Null(kilder[0].ShortName);
    }

    [Fact]
    public async Task GetKilderAsync_WhenARowHasANullCount_ThenTheReadFailsRatherThanReporting0()
    {
        // The decision, pinned. It costs the reader the whole table — KildeExplorer catches this and
        // draws "Kunne ikke laste kilder nå" — and that is the intended trade: a count of 0 for a
        // kilde with fourteen datasamlinger is a number a reader would believe.
        await Assert.ThrowsAsync<JsonException>(() => WithJson("""
            [{"id":"8ec4c2c4-662d-47a5-a946-f1086a014070","navn":"Als registeret","datasamlingCount":null}]
            """).GetKilderAsync());
    }

    [Fact]
    public void Json_WhenAContractIsResolved_ThenTheConverterIsOnTheNonNullableStringsOnly()
    {
        // The wiring, once: the sweep above asks NullAsEmptyStrings.Covers directly, which would go
        // on agreeing if the modifier were never registered on the options.
        var typeInfo = MuninExplorerClient.Json.GetTypeInfo(typeof(KildeSummary));

        Assert.NotNull(Property(typeInfo, "navn").CustomConverter);
        Assert.Null(Property(typeInfo, "kortNavn").CustomConverter);
    }

    [Fact]
    public void Json_WhenANonNullableStringIsWrittenBack_ThenItIsStillAString() =>
        // The converter has to write as well as read, and a Write that forgot to would be found by
        // nothing else: the client only writes request bodies, whose strings it fills in itself.
        Assert.Contains(
            "\"navn\":\"Als registeret\"",
            JsonSerializer.Serialize(new KildeSummary { Name = "Als registeret" }, MuninExplorerClient.Json));

    /// <summary>A contract spelled as a positional record, which several of the request bodies are.</summary>
    private sealed record Positional(
        [property: JsonPropertyName("navn")] string Name,
        [property: JsonPropertyName("kortNavn")] string? ShortName);

    [Fact]
    public void Deserialize_WhenTheContractIsAPositionalRecord_ThenTheStringsBehaveTheSameWay()
    {
        // The sweep reads PropertyInfo and so counts a positional record's properties as covered.
        // Whether they are is a question about constructor binding, which nothing else here asks:
        // the contracts are all init-only today, so a positional one would inherit the claim untested.
        var read = JsonSerializer.Deserialize<Positional>(
            """{"navn":null,"kortNavn":null}""", MuninExplorerClient.Json)!;

        Assert.Equal("", read.Name);
        Assert.Null(read.ShortName);
    }

    /// <summary>A count of the shape every count on every contract carries.</summary>
    private sealed record NonNullableCount
    {
        [JsonPropertyName("datasamlingCount")] public int DatasamlingCount { get; init; }
    }

    [Fact]
    public void Deserialize_WhenAnExplicitNullMeetsANonNullableValueType_ThenItThrowsRatherThanDefaulting() =>
        // The mechanism the "refused" class rests on, demonstrated once: the refusal is an exception
        // out of the whole read rather than a default in one field. What it does NOT do is pin the
        // contracts — that is the sweep. Mirrors the date probe in MuninExplorerClientTest.
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NonNullableCount>(
            """{"datasamlingCount":null}""", MuninExplorerClient.Json));

    // ------------------------------------------------------------------------------------ the sweep

    /// <summary>The class that decides this property's null, or null when nothing does.</summary>
    private static string? Classify(
        PropertyInfo property,
        NullabilityInfoContext nullability,
        NullAsEmptyCollections collections)
    {
        var type = property.PropertyType;

        if (Nullable.GetUnderlyingType(type) is not null)
        {
            return "nullable value type";
        }

        if (type.IsValueType)
        {
            // Refused by System.Text.Json, on purpose: no value of these types means "nothing", so
            // tolerating the null would mean inventing a fact. Fhi.Metadata-6r6rf paid for the
            // equivalent sentinel on dates.
            return "refused";
        }

        if (collections.CanConvert(type))
        {
            return "empty collection";
        }

        if (NullAsEmptyStrings.Covers(property, nullability))
        {
            return "empty string";
        }

        // ReadState, as NullAsEmptyStrings.Covers uses: the promise being broken is the one made to
        // whoever reads the property, and [AllowNull] is the case where the two states disagree.
        return nullability.Create(property).ReadState == NullabilityState.Nullable
            ? "nullable reference"
            : null;
    }

    /// <summary>Every settable property on every type this package reads from JSON.</summary>
    /// <remarks>
    /// By type rather than by property, which is the difference that matters: a contract spells
    /// <c>[JsonPropertyName]</c> on every property (AGENTS.md says so), so one attribute anywhere on
    /// the type is enough to identify it, and then a property that forgot its attribute — and binds
    /// by the camelCase policy of <c>JsonSerializerDefaults.Web</c> anyway — is swept with it.
    /// <para>
    /// It also keeps out the types that carry no attribute at all and are never deserialised:
    /// <see cref="ExplorerUrlState"/> and <see cref="VariableFilter"/> are built from a query string,
    /// and <see cref="ExportedList"/> from a response the client reads as bytes and headers.
    /// </para>
    /// </remarks>
    private static IReadOnlyList<PropertyInfo> DeserialisedProperties() =>
        [.. typeof(IMuninExplorerClient).Assembly
            .GetTypes()
            .Where(type => !typeof(Exception).IsAssignableFrom(type) && IsDeserialised(type))
            .SelectMany(type => type.GetProperties(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            .Where(property => property.SetMethod is not null
                               && property.GetCustomAttribute<JsonIgnoreAttribute>() is null)];

    private static bool IsDeserialised(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Any(property => property.GetCustomAttribute<JsonPropertyNameAttribute>() is not null);

    private static JsonPropertyInfo Property(JsonTypeInfo typeInfo, string name) =>
        typeInfo.Properties.Single(property => property.Name == name);

    private static MuninExplorerClient WithJson(string json) =>
        new(new HttpClient(StubHttpHandler.Ok(json))
        {
            BaseAddress = new Uri("https://runa.munin.skytest.fhi.no/")
        });
}
