using System.Text.Json;
using Fhi.Munin.Explorer.Client;

namespace Fhi.Munin.Explorer.Tests;

/// <summary>
/// Compares a response the API actually sent against what the contracts make of it.
/// </summary>
/// <remarks>
/// The comparison is a round trip: deserialise the live body into the DTO, serialise the DTO
/// back, and diff the two documents by shape — which keys exist at which path — rather than by
/// value. Values change every night; keys are the contract.
/// <para>
/// It reads both directions, because the two failures are different and both are silent:
/// </para>
/// <list type="bullet">
/// <item>A key in the live body with no key opposite it means the API sends something the
/// contracts drop on the floor. Nobody notices, because deserialisation ignores what it does
/// not recognise — this is also what a renamed field looks like from our side.</item>
/// <item>A key on our side with nothing opposite it means the contracts declare a field the API
/// has stopped sending, so the DTO's own default is being rendered as though it were data. An
/// empty string in a heading is indistinguishable from a variable with no name.</item>
/// </list>
/// <para>
/// The second direction is only reported when the round trip wrote something there. Null, an empty
/// array and an empty object are the three ways a contract says <em>nothing here</em>, and where it
/// said that there is nothing to tell apart: an omitted field and a field sent as null — or as
/// <c>[]</c> — produce exactly the same DTO, and a component renders both the same way. Reporting
/// them would fire on every field this package models ahead of the API it is pointed at, and on
/// the day somebody turns on "omit nulls" server side it would fire on all of them at once. A
/// nightly job nobody believes is a nightly job nobody reads.
/// </para>
/// <para>
/// The same three are interchangeable read the other way as well, which is what a live null
/// opposite an empty collection is: the client reads an explicit null where a collection is due as
/// the empty collection — see <c>NullAsEmptyCollections</c> — so the round trip writes <c>{}</c> or
/// <c>[]</c> where the live body had <c>null</c>. The kinds differ and nothing has drifted, and
/// since the Explorer API demonstrably sends <c>additionalProperties</c> that way, reporting it
/// would be a standing false positive on the very payloads the package was taught to survive.
/// </para>
/// <para>
/// What that gives up is narrow and worth stating: a collection the API stops sending entirely
/// reads exactly like a collection it sends empty, so a withdrawn <c>delkilder</c> would pass here.
/// It would not pass unnoticed for long — the same change usually renames or moves something else
/// as well, and that is the direction above. The alternative was a job that is red for a reason
/// nobody can act on, which fails sooner and more quietly than this does.
/// </para>
/// </remarks>
internal static class ShapeDrift
{
    /// <summary>Round-trips <paramref name="value"/> and reports how its shape differs from <paramref name="liveJson"/>.</summary>
    /// <remarks>
    /// Serialised with the client's own options, not a fresh set: the round trip is only evidence
    /// about the real client if it is the real client's serialiser on both legs.
    /// </remarks>
    public static IReadOnlyList<string> Against<T>(string liveJson, T value) =>
        Between(liveJson, JsonSerializer.Serialize(value, MuninExplorerClient.Json));

    /// <summary>Diffs two JSON documents by shape, returning one line per finding.</summary>
    public static IReadOnlyList<string> Between(string liveJson, string roundTrippedJson)
    {
        using var live = JsonDocument.Parse(liveJson);
        using var ours = JsonDocument.Parse(roundTrippedJson);

        // Array elements share one path — "$.items[]", not "$.items[3]" — so a field missing from
        // all 60 kilder is one finding rather than 60. Kept apart from the message itself because
        // the message quotes the offending value, which differs from one element to the next; two
        // findings about the same field at the same path are one finding however they read.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var findings = new List<string>();

        Compare(live.RootElement, ours.RootElement, "$", seen, findings);

        return findings;
    }

    private static void Compare(
        JsonElement live,
        JsonElement ours,
        string path,
        HashSet<string> seen,
        List<string> findings)
    {
        if (live.ValueKind != ours.ValueKind)
        {
            // A live null opposite an empty collection is the same "nothing here" the withdrawn
            // direction below already lets through, read from the other side: the client reads an
            // explicit null where a collection is due as the empty collection, so a live
            // "additionalProperties": null comes back as {} and a live "delkilder": null as [].
            // Nothing has drifted there — the contract holds exactly what was sent, and renders it
            // the same way — but the kinds differ, and reporting that would make the nightly job
            // file an issue on every payload carrying a null the package handles by design. The
            // Explorer API demonstrably sends additionalProperties that way.
            if (live.ValueKind == JsonValueKind.Null && IsNothing(ours))
            {
                return;
            }

            // Most type changes throw during deserialisation and never reach here. The ones that
            // survive it are the quiet ones — a scalar that became an object, an object that
            // became an array — so they are worth a line of their own.
            Report(
                $"{path}:kind",
                $"{path} — the API sends {Describe(live.ValueKind)} here and the contract makes {Describe(ours.ValueKind)} of it.",
                seen,
                findings);
            return;
        }

        switch (live.ValueKind)
        {
            case JsonValueKind.Object:
                CompareObjects(live, ours, path, seen, findings);
                break;

            case JsonValueKind.Array:
                CompareArrays(live, ours, path, seen, findings);
                break;

            default:
                // A leaf of the same kind. Values are today's data, not the contract.
                break;
        }
    }

    private static void CompareObjects(
        JsonElement live,
        JsonElement ours,
        string path,
        HashSet<string> seen,
        List<string> findings)
    {
        var liveNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in live.EnumerateObject())
        {
            liveNames.Add(property.Name);

            if (ours.TryGetProperty(property.Name, out var mine))
            {
                Compare(property.Value, mine, $"{path}.{property.Name}", seen, findings);
            }
            else
            {
                Report(
                    $"{path}.{property.Name}:unmapped",
                    $"{path}.{property.Name} — the API sends this and the contract has nowhere to put it. " +
                    $"Its value here is {Summarise(property.Value)}.",
                    seen,
                    findings);
            }
        }

        foreach (var property in ours.EnumerateObject())
        {
            if (liveNames.Contains(property.Name) || IsNothing(property.Value))
            {
                continue;
            }

            Report(
                $"{path}.{property.Name}:withdrawn",
                $"{path}.{property.Name} — the contract expects this and the API did not send it, " +
                $"so {Summarise(property.Value)} is the DTO's own default being shown as data.",
                seen,
                findings);
        }
    }

    private static void CompareArrays(
        JsonElement live,
        JsonElement ours,
        string path,
        HashSet<string> seen,
        List<string> findings)
    {
        // Serialising a deserialised array cannot change its length, so this is a guard against
        // the comparison itself being wrong rather than against the API.
        var liveLength = live.GetArrayLength();
        var ourLength = ours.GetArrayLength();

        if (liveLength != ourLength)
        {
            Report(
                $"{path}:length",
                $"{path} — the API sends {liveLength} items and the round trip kept {ourLength}.",
                seen,
                findings);
            return;
        }

        // Every element, not just the first: which keys an element carries can differ between them,
        // and the element that differs is exactly the one worth knowing about.
        var liveItems = live.EnumerateArray();
        var ourItems = ours.EnumerateArray();

        while (liveItems.MoveNext() && ourItems.MoveNext())
        {
            Compare(liveItems.Current, ourItems.Current, $"{path}[]", seen, findings);
        }
    }

    /// <summary>Records a finding, unless the same field at the same path has already produced one.</summary>
    private static void Report(string key, string finding, HashSet<string> seen, List<string> findings)
    {
        if (seen.Add(key))
        {
            findings.Add(finding);
        }
    }

    /// <summary>Whether a round-tripped value is one of the ways a contract says "nothing here".</summary>
    private static bool IsNothing(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => true,
        JsonValueKind.Array => value.GetArrayLength() == 0,
        JsonValueKind.Object => !value.EnumerateObject().Any(),
        _ => false
    };

    /// <summary>A value short enough to sit in a failure message, so the finding can be read without the payload.</summary>
    private static string Summarise(JsonElement value)
    {
        const int limit = 60;
        var text = value.GetRawText();

        return text.Length <= limit ? text : string.Concat(text.AsSpan(0, limit), "…");
    }

    private static string Describe(JsonValueKind kind) => kind switch
    {
        JsonValueKind.Object => "an object",
        JsonValueKind.Array => "an array",
        JsonValueKind.String => "a string",
        JsonValueKind.Number => "a number",
        JsonValueKind.True or JsonValueKind.False => "a boolean",
        JsonValueKind.Null => "null",
        _ => "nothing"
    };
}
