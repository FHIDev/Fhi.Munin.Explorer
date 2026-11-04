using System.Globalization;
using System.Text.Json;
using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Client;

/// <summary>Reads a stored share snapshot, whichever frontend wrote it.</summary>
/// <remarks>
/// By hand rather than by <c>JsonSerializer</c>: the items are whatever Runa or this package posted,
/// stored verbatim, and one unreadable date must cost that field rather than the whole list.
/// Mirrors Runa's cloneListFromSnapshot, legacy <c>variableIds</c> form included. An item is a
/// (variable, datasamling) pair; one written before items named a datasamling reads as not chosen.
/// </remarks>
internal static class SharedSnapshot
{
    public static SharedList Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"A shared list snapshot is an object, not {root.ValueKind}.");
        }

        var name = Text(root, "name")?.Trim() ?? "";
        var seen = new HashSet<VariableDatasamlingKey>();
        var items = new List<VariableListItem>();

        if (root.TryGetProperty("items", out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in array.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.Object
                    && Id(element, "variabelId") is { } id
                    && seen.Add(new VariableDatasamlingKey(id, Id(element, "datasamlingId"))))
                {
                    items.Add(Item(element, id));
                }
            }
        }
        else if (root.TryGetProperty("variableIds", out var ids) && ids.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in ids.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String
                    && Guid.TryParse(element.GetString(), out var id)
                    && seen.Add(new VariableDatasamlingKey(id, null)))
                {
                    items.Add(new VariableListItem { VariableId = id });
                }
            }
        }

        return new SharedList(name, items);
    }

    private static VariableListItem Item(JsonElement element, Guid id) => new()
    {
        VariableId = id,
        VariableCode = Text(element, "variabelCode"),
        VariableName = Text(element, "variabelName"),
        KildeId = Id(element, "kildeId"),
        KildeName = Text(element, "kildeName"),
        KildeShortName = Text(element, "kildeKortNavn"),
        DatasamlingName = Text(element, "datasamlingName"),
        VariabelgruppeName = Text(element, "variabelgruppeName"),
        DataType = Text(element, "dataType"),
        DataFrom = Date(element, "dataFrom"),
        DataTo = Date(element, "dataTo"),
        VersionStatus = Text(element, "versjonStatus"),
        DatasamlingId = Id(element, "datasamlingId"),
        DesiredDataCodes = Codes(element),
    };

    // A client-built share keeps whatever codes the client posted, unchecked (Fhi.Metadata-emsc0.3.7): read as text,
    // capped at the API's 200 choices, and a choice without a code is dropped rather than shown blank.
    private static IReadOnlyList<DesiredCodeView> Codes(JsonElement element)
    {
        if (!element.TryGetProperty("desiredDataCodes", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var codes = new List<DesiredCodeView>();
        foreach (var code in array.EnumerateArray())
        {
            if (codes.Count == MaxCodes)
            {
                break;
            }

            if (code.ValueKind == JsonValueKind.Object && Text(code, "kode") is { Length: > 0 } value)
            {
                codes.Add(new DesiredCodeView
                {
                    KodeverkType = Text(code, "kodeverkType") ?? "",
                    KodeverkReference = Text(code, "kodeverkReference") ?? "",
                    Kind = Text(code, "kind") ?? "",
                    Code = value,
                    To = Text(code, "til"),
                    Name = Text(code, "navn"),
                    ToName = Text(code, "tilNavn"),
                    Expired = code.TryGetProperty("utgatt", out var expired) && expired.ValueKind == JsonValueKind.True,
                    Count = code.TryGetProperty("antall", out var count) && count.ValueKind == JsonValueKind.Number
                        && count.TryGetInt32(out var n) && n >= 0 ? n : null,
                });
            }
        }

        return codes;
    }

    private const int MaxCodes = 200;

    // A number is kept as written: Runa's dataType is a string today, and a code sent bare is
    // still the same code.
    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null,
            }
            : null;

    private static Guid? Id(JsonElement element, string name) =>
        Guid.TryParse(Text(element, name), out var id) ? id : null;

    private static DateTimeOffset? Date(JsonElement element, string name) =>
        DateTimeOffset.TryParse(
            Text(element, name),
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var date)
            ? date
            : null;
}
