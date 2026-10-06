using Fhi.Munin.Explorer.Contracts;
using Fhi.Munin.Explorer.Display;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>
/// The kodeverk a variable's values are drawn from, a heading per kind of link and a line per link,
/// each with the control that fetches its codes.
/// </summary>
/// <remarks>
/// Groups come in the order the payload first mentions each kind: the API owns that list. The code
/// lists live in <see cref="Lists"/>, which the parent owns so that two views of one variable share
/// what was fetched.
/// </remarks>
internal sealed class KodeverkGroups : ComponentBase, IDisposable
{
    // Runa's INLINE_CODE_PREVIEW, so the two clients draw the same list to the same length.
    private const int InlineCodePreview = 8;

    private readonly string _instance = Guid.NewGuid().ToString("N")[..8];

    [Parameter, EditorRequired] public VariableDetail Detail { get; set; } = null!;

    [Parameter, EditorRequired] public KodeverkCodeLists Lists { get; set; } = null!;

    [Parameter] public int Level { get; set; } = 3;

    [Parameter] public string Language { get; set; } = "no";

    private Texts T => Texts.For(Language);

    private KodeverkCodeLists? _listening;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_listening, Lists))
        {
            return;
        }

        Dispose();
        _listening = Lists;
        _listening.Changed += Redraw;
    }

    public void Dispose()
    {
        if (_listening is not null)
        {
            _listening.Changed -= Redraw;
            _listening = null;
        }
    }

    private void Redraw() => _ = InvokeAsync(StateHasChanged);

    // By the link's place in the payload: a reference is catalogue text whose punctuation cannot be
    // made into an id without two references minting the same one.
    private string NameId(int index) => $"munin-explorer-kodeverk-{_instance}-{index}";

    private string CodesId(int index) => $"munin-explorer-codes-{_instance}-{index}";

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Detail.KodeverkLinks.Count == 0)
        {
            return;
        }

        builder.AddContent(1, DetailBlocks.LeadParagraph(T.KodeverkLead(Detail.KodeverkLinks.Select(link => link.KodeverkType))));

        // Numbered before grouping, so the ids stay unique across the whole panel.
        var links = Detail.KodeverkLinks.Select((link, index) => (Link: link, Index: index));

        var seq = 10;

        foreach (var group in links.GroupBy(entry => entry.Link.KodeverkType, StringComparer.OrdinalIgnoreCase))
        {
            builder.OpenElement(seq, $"h{Level}");
            builder.AddAttribute(seq + 1, "class", "headline headline-xxs margin--none munin-explorer-group");
            builder.AddContent(seq + 2, T.KodeverkTypeLabel(group.Key));
            builder.CloseElement();

            builder.OpenElement(seq + 3, "ul");
            builder.AddAttribute(seq + 4, "class", "munin-explorer-kodeverk");
            builder.AddContent(seq + 5, Items(group));
            builder.CloseElement();

            seq += 10;
        }
    }

    private Task ToggleFromControlAsync(KodeverkLink link, MouseEventArgs released) =>
        RowPress.WasSelectionStandingStill(released) ? Task.CompletedTask : Lists.ToggleAsync(link);

    private enum InlineCodes
    {
        Loading,
        Failed,
        None,
        Codes
    }

    // Nothing fetched yet is the window before the fetch starts, and it reads as loading, not empty.
    private InlineCodes InlineCodesState(KodeverkKey key) =>
        Lists.HasFailed(key) ? InlineCodes.Failed
        : Lists.CodesOf(key) is not { } codes ? InlineCodes.Loading
        : codes.Count == 0 ? InlineCodes.None
        : InlineCodes.Codes;

    // Code and name in a span each, so Stiler can set them in two columns (sak #6132); the space
    // between them keeps the line reading "1 Ja" where no stylesheet does.
    private static RenderFragment InlineCodesPreview(IReadOnlyList<KodeverkCode> codes) => builder =>
    {
        builder.OpenElement(0, "ul");
        builder.AddAttribute(1, "lang", "no");

        foreach (var code in codes.Take(InlineCodePreview))
        {
            builder.OpenElement(2, "li");
            builder.OpenElement(3, "span");
            builder.AddContent(4, code.Value);
            builder.CloseElement();

            if (DisplayText.Trimmed(code.Name) is { } name)
            {
                builder.AddContent(5, " ");
                builder.OpenElement(6, "span");
                builder.AddContent(7, name);
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        builder.CloseElement();
    };

    private RenderFragment Items(IEnumerable<(KodeverkLink Link, int Index)> links) => builder =>
    {
        var seq = 0;

        // How often each key has appeared, so a kodeverk the payload names twice gets a unique key.
        var occurrences = new Dictionary<KodeverkKey, int>();

        foreach (var (link, index) in links)
        {
            var key = KodeverkKey.Of(link);
            var inline = KodeverkCodeLists.IsUnnamedKildekodeverk(link) ? InlineCodesState(key) : (InlineCodes?)null;
            var showAll = inline is InlineCodes.Codes && Lists.CodesOf(key)!.Count > InlineCodePreview;

            var occurrence = occurrences.GetValueOrDefault(key);
            occurrences[key] = occurrence + 1;

            builder.OpenElement(seq, "li");
            // Keyed on the link, so two links reordered under one heading keep their own lists open.
            builder.SetKey((key, occurrence));
            builder.AddAttribute(seq + 1, "class", "munin-explorer-kodeverk__item");

            // A list cannot sit inside a <p>, so the preview's slot is a div wearing the same class.
            builder.OpenElement(seq + 2, inline is InlineCodes.Codes ? "div" : "p");
            builder.AddAttribute(seq + 3, "class", "munin-explorer-kodeverk__name");
            builder.AddAttribute(seq + 4, "id", NameId(index));

            if (inline is { } state)
            {
                builder.AddContent(seq + 5, state switch
                {
                    InlineCodes.Loading => (RenderFragment)(b => b.AddContent(0, T.CodesLoading)),
                    InlineCodes.Failed => b => b.AddContent(0, T.KodeverkUnnamed),
                    InlineCodes.None => b => b.AddContent(0, T.NoCodes),
                    _ => InlineCodesPreview(Lists.CodesOf(key)!)
                });
            }
            else if (DisplayText.Trimmed(link.DisplayName) is { } name)
            {
                builder.AddAttribute(seq + 6, "lang", "no");
                builder.AddContent(seq + 7, name);
            }
            else
            {
                builder.AddContent(seq + 8, T.KodeverkUnnamed);
            }

            builder.CloseElement();

            // The reference is left out where the codes are the identity, and back when they cannot be shown.
            if (inline is null or InlineCodes.Failed or InlineCodes.None)
            {
                builder.OpenElement(seq + 9, "p");
                builder.AddAttribute(seq + 10, "class", "caption munin-explorer-kodeverk__reference");
                builder.AddContent(seq + 11, $"{T.FieldKodeverkReference}: {link.KodeverkReference}");
                builder.CloseElement();
            }

            // No button where the API serves no codes, nor where they are all inline already.
            if (link.HasCodeValues && (inline is null or InlineCodes.Failed || showAll))
            {
                builder.AddContent(seq + 12, CodesToggle(link, key, index, showAll));
            }

            builder.CloseElement();

            seq += 20;
        }
    };

    // aria-controls only while the list is open: an id naming an absent element is worse than none.
    private RenderFragment CodesToggle(KodeverkLink link, KodeverkKey key, int index, bool showAll) => builder =>
    {
        var open = Lists.IsOpen(key);

        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "class", "hd-button-square button-square--ghost margin-bottom");
        builder.AddAttribute(2, "type", "button");
        builder.AddAttribute(3, "aria-expanded", open ? "true" : "false");
        builder.AddAttribute(4, "aria-controls", open ? CodesId(index) : null);
        builder.AddAttribute(5, "onclick",
            EventCallback.Factory.Create<MouseEventArgs>(this, e => ToggleFromControlAsync(link, e)));
        builder.AddContent(6, open ? T.HideCodes : showAll ? T.ShowAllCodes(Lists.CodesOf(key)!.Count) : T.ShowCodes);
        builder.CloseElement();

        if (!open)
        {
            return;
        }

        // Stiler makes this box scroll, and a scroll box a keyboard cannot reach fails WCAG 2.1.1.
        builder.OpenElement(7, "div");
        builder.AddAttribute(8, "id", CodesId(index));
        builder.AddAttribute(9, "class", "munin-explorer-codes");
        builder.AddAttribute(10, "role", "region");
        builder.AddAttribute(11, "tabindex", "0");
        builder.AddAttribute(12, "aria-labelledby", NameId(index));
        builder.AddContent(13, CodesBody(key, index));
        builder.CloseElement();
    };

    private RenderFragment CodesBody(KodeverkKey key, int index) => builder =>
    {
        if (Lists.IsLoading(key))
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "class", "caption");
            builder.AddContent(2, T.CodesLoading);
            builder.CloseElement();

            return;
        }

        if (Lists.FailureOf(key) is { } failure)
        {
            builder.OpenElement(3, "p");
            builder.AddAttribute(4, "class", "infobox infobox--bg-yellow");
            builder.AddContent(5, failure is CodesFailure.Throttled ? T.RateLimitError : T.CodesError);
            builder.CloseElement();

            return;
        }

        var codes = Lists.CodesOf(key) ?? [];

        if (codes.Count == 0)
        {
            builder.OpenElement(6, "p");
            builder.AddAttribute(7, "class", "caption");
            builder.AddContent(8, T.NoCodes);
            builder.CloseElement();

            return;
        }

        builder.AddContent(9, CodesTable(index, codes));
    };

    // A real <table>: four aligned columns have no other shape that survives being unstyled.
    private RenderFragment CodesTable(int index, IReadOnlyList<KodeverkCode> codes) => builder =>
    {
        builder.OpenElement(0, "table");
        builder.AddAttribute(1, "class", "munin-explorer-codes__table");
        builder.AddAttribute(2, "aria-labelledby", NameId(index));

        builder.OpenElement(3, "thead");
        builder.OpenElement(4, "tr");

        var head = 5;

        foreach (var heading in new[] { T.ColumnCodeValue, T.ColumnCodeName, T.ColumnValidFrom, T.ColumnValidTo })
        {
            builder.OpenElement(head, "th");
            builder.AddAttribute(head + 1, "scope", "col");
            builder.AddContent(head + 2, heading);
            builder.CloseElement();

            head += 3;
        }

        builder.CloseElement();
        builder.CloseElement();

        builder.OpenElement(20, "tbody");

        var seq = 30;

        foreach (var code in codes)
        {
            builder.OpenElement(seq, "tr");

            builder.OpenElement(seq + 1, "td");
            builder.AddContent(seq + 2, code.Value);
            builder.CloseElement();

            builder.OpenElement(seq + 3, "td");
            builder.AddAttribute(seq + 4, "lang", "no");
            builder.AddContent(seq + 5, DisplayText.Trimmed(code.Name) ?? T.NotSpecified);
            builder.CloseElement();

            builder.OpenElement(seq + 6, "td");
            builder.AddContent(seq + 7, ValidityDate(code.ValidFrom));
            builder.CloseElement();

            builder.OpenElement(seq + 8, "td");
            builder.AddContent(seq + 9, ValidityDate(code.ValidTo));
            builder.CloseElement();

            builder.CloseElement();

            seq += 20;
        }

        builder.CloseElement();
        builder.CloseElement();
    };

    // The day only: these arrive as midnight UTC or an import's instant, neither a fact about the code.
    private string ValidityDate(DateTimeOffset? date) =>
        date is { } value ? CatalogueDate.Day(value, Language, DateWidth.Narrow) : T.NotSpecified;
}
