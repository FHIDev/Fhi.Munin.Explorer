using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Fhi.Munin.Explorer.Blazor;

// The dataperiode facet's two native date fields, and what a typed date has to be before it filters.
public partial class VariableSearch
{
    // The API matches overlapping periods, so catalogue dates must not constrain the search interval.
    private RenderFragment DateFields() => builder =>
    {
        DateField(builder, 0, DateFromId, T.FacetDateFrom, Pending.DataFrom,
                  null, Pending.DataTo,
                  value => ApplyFilterAsync(Pending with { DataFrom = value }));

        DateField(builder, 100, DateToId, T.FacetDateTo, Pending.DataTo,
                  Pending.DataFrom, null,
                  value => ApplyFilterAsync(Pending with { DataTo = value }));
    };

    // Native controls display the browser's local format but exchange ISO dates with Blazor.
    private void DateField(
        RenderTreeBuilder builder, int seq, string id, string label, DateOnly? value,
        DateOnly? min, DateOnly? max, Func<DateOnly?, Task> set)
    {
        var errorId = $"{id}-error";
        var refused = _refusedDates.TryGetValue(id, out var typedText);
        var shown = _editedDates.TryGetValue(id, out var edited) ? edited
            : refused ? typedText! : value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";

        builder.OpenElement(seq, "label");
        builder.AddAttribute(seq + 1, "class", "form-element__label");
        builder.AddAttribute(seq + 2, "for", id);
        builder.AddContent(seq + 3, label);
        builder.CloseElement();

        builder.OpenElement(seq + 10, "input");
        builder.AddAttribute(seq + 11, "id", id);
        builder.AddAttribute(seq + 12, "type", "date");
        builder.AddAttribute(seq + 13, "autocomplete", "off");
        builder.AddAttribute(seq + 14, "min", min?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        builder.AddAttribute(seq + 15, "max", max?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        builder.AddAttribute(seq + 16, "value", shown);
        builder.AddAttribute(seq + 17, "aria-invalid", refused ? "true" : null);
        builder.AddAttribute(seq + 18, "aria-describedby", refused ? errorId : null);

        // Native date controls fire change for each year digit; searching then can interrupt typing.
        builder.AddAttribute(seq + 19, "onchange",
            EventCallback.Factory.CreateBinder<string?>(this, raw =>
            {
                _editedDates[id] = raw ?? "";
                _refusedDates.Remove(id);
            }, shown));
        builder.SetUpdatesAttributeName("value");
        builder.AddAttribute(seq + 20, "onblur",
            EventCallback.Factory.Create<FocusEventArgs>(this, _ => CommitDateAsync(id, min, max, set)));
        builder.CloseElement();

        // Always rendered and empty until needed: validation runs as focus leaves, so the reader is
        // elsewhere by then, and a role="alert" inserted and filled in one update is announced
        // unreliably. The infobox is inside, so an unrefused field draws no empty box.
        builder.OpenElement(seq + 30, "div");
        builder.AddAttribute(seq + 31, "id", errorId);
        builder.AddAttribute(seq + 32, "role", "alert");
        builder.AddAttribute(seq + 33, "aria-live", "assertive");
        builder.AddAttribute(seq + 34, "aria-atomic", "true");

        if (refused)
        {
            builder.OpenElement(seq + 35, "p");
            builder.AddAttribute(seq + 36, "class", "infobox infobox--bg-yellow");
            builder.AddContent(seq + 37, T.FacetDateInvalid(
                min is { } lo ? DateInput.Format(lo, Language) : null,
                max is { } hi ? DateInput.Format(hi, Language) : null));
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private async Task CommitDateAsync(string id, DateOnly? min, DateOnly? max, Func<DateOnly?, Task> set)
    {
        var browser = _interop is { } interop ? await interop.ReadDateInputAsync(id) : null;
        if (browser?.BadInput == true)
        {
            _editedDates.Remove(id);
            _refusedDates[id] = "";
            return;
        }

        if (!_editedDates.Remove(id, out var raw))
        {
            // Clearing an incomplete native entry can leave value empty without another change event.
            if (browser is null || !_refusedDates.ContainsKey(id))
            {
                return;
            }

            raw = browser.Value;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            if (browser is null || browser.Value.Length != 0)
            {
                _refusedDates[id] = raw;
                return;
            }

            _refusedDates.Remove(id);
            await set(null);
            return;
        }

        if (DateInput.TryParse(raw, Language, out var typed) && Within(typed, min, max))
        {
            _refusedDates.Remove(id);
            await set(typed);
            return;
        }

        _refusedDates[id] = raw;
    }

    private static bool Within(DateOnly? value, DateOnly? min, DateOnly? max) =>
        value is not { } date || ((min is not { } lo || date >= lo) && (max is not { } hi || date <= hi));

    private string DateFromId => $"munin-explorer-date-from-{_instance}";

    private string DateToId => $"munin-explorer-date-to-{_instance}";

    /// <summary>What the reader typed into a date field that was refused, by the field's id.</summary>
    private readonly Dictionary<string, string> _refusedDates = [];

    private readonly Dictionary<string, string> _editedDates = [];
}
