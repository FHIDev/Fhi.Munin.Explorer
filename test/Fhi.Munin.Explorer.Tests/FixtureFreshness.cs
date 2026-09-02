using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Reports what the live API sends that a captured fixture under <c>Testdata/</c> has never heard of.
/// </summary>
/// <remarks>
/// <see cref="ContractDriftTest"/> compares the live API against the contracts and
/// <see cref="ContractCoverageTest"/> compares the fixtures against the contracts. Neither compares
/// the fixtures against the API, and a field the API sends and the DTO models but the fixture lacks
/// passes both: strict deserialisation rejects an unmapped member, never an absent one.
/// <para>
/// One direction only — a key the live body carries and the fixture does not. The other direction
/// cannot be told apart from data: the fixture is a capture of one entity and the live call fetches
/// another, so a key missing live may simply be empty on today's row.
/// </para>
/// <para>
/// Descent stops wherever either side says <em>nothing here</em> — null, <c>[]</c> or <c>{}</c> —
/// for the same reason. A fixture whose <c>delkilder</c> is empty is a kilde without delkilder, not
/// a stale capture, so nothing inside it is compared.
/// </para>
/// </remarks>
internal static class FixtureFreshness
{
    /// <summary>
    /// The one member whose interior is catalogue data rather than shape.
    /// </summary>
    /// <remarks>
    /// <c>additionalProperties</c> is curated per-entity metadata: one kilde carries
    /// <c>NavnEngelsk</c> and the next does not, so comparing its keys across two entities reports
    /// the catalogue, not the API.
    /// <para>
    /// Deliberately this member and not every dictionary the contracts declare. The translation
    /// maps on <see cref="PropertyMetadataEntry"/> are keyed by language code, a closed vocabulary
    /// whose drift — an <c>en</c> the API now sends and a capture lacks — is exactly what this is
    /// for. Read off the contracts so it covers all eight types that declare the bag.
    /// </para>
    /// </remarks>
    private static readonly HashSet<string> OpenBags = OpenBagNames();

    /// <summary>Compares one live response body against the fixture captured from the same endpoint.</summary>
    public static IReadOnlyList<string> Against(string liveJson, string fixtureJson)
    {
        using var live = JsonDocument.Parse(liveJson);
        using var fixture = JsonDocument.Parse(fixtureJson);

        var theirs = Describe(live.RootElement);
        var ours = Describe(fixture.RootElement);

        var findings = new List<string>();

        // An empty document on either side carries no evidence. An empty live response is a quiet
        // day in the catalogue; an empty fixture is a different fault, and the callers check for it.
        if (theirs.HasContent && ours.HasContent)
        {
            Compare(theirs, ours, "$", findings);
        }

        return findings;
    }

    /// <summary>Whether a document holds anything at all, so a caller can refuse to compare against nothing.</summary>
    public static bool CarriesAnything(string json)
    {
        using var document = JsonDocument.Parse(json);

        return HasContent(document.RootElement);
    }

    private static void Compare(Shape live, Shape fixture, string path, List<string> findings)
    {
        // Array elements share one path — "$.items[].navn" — so a key missing from every element is
        // one finding rather than sixty.
        var separator = live.IsArray ? "[]." : ".";

        foreach (var (name, member) in live.Members)
        {
            if (!member.HasContent)
            {
                continue;
            }

            var childPath = path + separator + name;

            if (!fixture.Members.TryGetValue(name, out var ours))
            {
                findings.Add(
                    $"{childPath} — the API sends this and the fixture has no such key, so the capture " +
                    $"is older than the endpoint. Its value live is {member.Sample}.");
                continue;
            }

            if (ours.HasContent && !OpenBags.Contains(name))
            {
                Compare(member, ours, childPath, findings);
            }
        }
    }

    /// <summary>Folds a document into the keys it carries, unioning an array's elements into one shape.</summary>
    private static Shape Describe(JsonElement element)
    {
        var shape = new Shape(element.ValueKind == JsonValueKind.Array, HasContent(element), Summarise(element));

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Absorb(shape, property.Name, Describe(property.Value));
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var (name, member) in Describe(item).Members)
                    {
                        Absorb(shape, name, member);
                    }
                }

                break;
        }

        return shape;
    }

    /// <summary>Merges a member into a shape, letting the first element that carries a value describe it.</summary>
    private static void Absorb(Shape shape, string name, Shape member)
    {
        if (!shape.Members.TryGetValue(name, out var existing) || (!existing.HasContent && member.HasContent))
        {
            shape.Members[name] = member;
        }
    }

    /// <summary>The JSON name the contracts give the free-form bag, on every type that declares one.</summary>
    private static HashSet<string> OpenBagNames() =>
        typeof(KildeDetail).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(KildeDetail).Namespace)
            .SelectMany(type => type.GetProperties())
            .Where(property => property.Name == nameof(KildeDetail.AdditionalProperties)
                && property.PropertyType.IsGenericType
                && property.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>))
            .Select(JsonNameOf)
            .ToHashSet(StringComparer.Ordinal);

    private static string JsonNameOf(PropertyInfo property) =>
        property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
        ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);

    /// <summary>Whether a value is one of the three ways JSON says "nothing here".</summary>
    private static bool HasContent(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Undefined or JsonValueKind.Null => false,
        JsonValueKind.Array => value.GetArrayLength() > 0,
        JsonValueKind.Object => value.EnumerateObject().Any(),
        _ => true
    };

    /// <summary>A value short enough to sit in a failure message, so the finding reads without the payload.</summary>
    private static string Summarise(JsonElement value)
    {
        const int limit = 60;

        if (value.ValueKind == JsonValueKind.Undefined)
        {
            return "nothing";
        }

        var text = value.GetRawText();

        return text.Length <= limit ? text : string.Concat(text.AsSpan(0, limit), "…");
    }

    /// <summary>One node of a document folded down to its keys.</summary>
    private sealed record Shape(bool IsArray, bool HasContent, string Sample)
    {
        public Dictionary<string, Shape> Members { get; } = new(StringComparer.Ordinal);
    }
}
