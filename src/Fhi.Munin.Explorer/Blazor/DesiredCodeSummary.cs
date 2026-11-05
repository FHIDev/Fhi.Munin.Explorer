using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// Chosen codes as a short summary, one line per code list: «ICD-10: E11 med alt under (10 koder), E10–E14 (50 koder)».
/// </summary>
/// <remarks>
/// Text only: in a shared list these are whatever a client posted, unchecked, so nothing here is markup. The code
/// list is named by the API's name when it sends one, else by its kind, and a health code system by its reference.
/// </remarks>
internal static class DesiredCodeSummary
{
    public static string ListLabel(Texts t, DesiredCodeView code) =>
        !string.IsNullOrWhiteSpace(code.KodeverkName) ? code.KodeverkName!
        : string.Equals(code.KodeverkType, "HelsefagligKodeverk", StringComparison.OrdinalIgnoreCase) && code.KodeverkReference.Length > 0
            ? code.KodeverkReference
            : t.KodeverkTypeLabel(code.KodeverkType);

    /// <summary>
    /// The summary, a line per code list, or nothing when there are no codes. Plain blocks, not a list: a host styles
    /// no bare list here, and its bullets would crowd a table cell. Catalogue names carry <paramref name="lang"/>.
    /// </summary>
    public static RenderFragment For(Texts t, IReadOnlyList<DesiredCodeView> codes, string? lang) => builder =>
    {
        if (codes.Count == 0)
        {
            return;
        }

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "caption margin--none");

        foreach (var group in codes.GroupBy(c => (c.KodeverkType, c.KodeverkReference)))
        {
            builder.OpenElement(3, "div");
            builder.SetKey(group.Key);
            builder.OpenElement(4, "span");
            builder.AddAttribute(5, "lang", string.IsNullOrWhiteSpace(group.First().KodeverkName) ? null : lang);
            builder.AddContent(6, ListLabel(t, group.First()));
            builder.CloseElement();
            builder.AddContent(7, ": ");
            var first = true;
            foreach (var code in group)
            {
                if (!first)
                {
                    builder.AddContent(8, ", ");
                }

                first = false;
                Part(builder, t, code, lang);
            }

            builder.CloseElement();
        }

        builder.CloseElement();
    };

    private static void Part(RenderTreeBuilder builder, Texts t, DesiredCodeView code, string? lang)
    {
        builder.OpenRegion(20);
        // A code, or a kind this package does not know, shown as its code and its term marked Norwegian.
        if (code.Kind is not (DesiredCodeKinds.Node or DesiredCodeKinds.Range) && !string.IsNullOrWhiteSpace(code.Name))
        {
            builder.AddContent(0, $"{code.Code} ");
            builder.OpenElement(1, "span");
            builder.AddAttribute(2, "lang", lang);
            builder.AddContent(3, code.Name);
            builder.CloseElement();
        }
        else
        {
            builder.AddContent(4, DesiredCodePicker.Describe(t, code.ToChoice(), code.Name, code.ToName));
        }

        var count = code.Kind != DesiredCodeKinds.Code && code.Count is { } n ? $" ({t.PickerCodeCount(n)})" : "";
        builder.AddContent(5, $"{(code.Expired ? $" ({t.PickerExpired(null)})" : "")}{count}");
        builder.CloseRegion();
    }
}
