using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Fhi.Munin.Explorer.Blazor;

// Avansert søk: a form of field-operator-value rows under the search field, applied as VariableFilter.Conditions so the
// list, the counts and an export all narrow by them, and shown as chips beside the facets (Fhi.Metadata-ipok2.2).
public partial class VariableSearch
{
    /// <summary>One row of the form while it is being edited; applied only by «Vis treff».</summary>
    private sealed class ConditionDraft(int key)
    {
        internal int Key { get; } = key;
        internal string Field { get; set; } = "";
        internal string Operator { get; set; } = "";
        internal string Text { get; set; } = "";
        internal string From { get; set; } = "";
        internal string To { get; set; } = "";
        internal HashSet<string> Codes { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal string? Error { get; set; }
    }

    private bool AdvancedSearchOffered => ServiceProvider.GetService<AdvancedSearchSwitch>()?.On == true;

    private bool _advancedOpen;

    // Until an opening has its rows, «Vis treff» would apply none and «Legg til» add one the opening then follows.
    private bool _advancedLoading;

    // Null until asked for; empty when the API has none to offer or the call failed.
    private IReadOnlyList<ConditionField>? _conditionFields;

    // A failure is not an answer: the next open asks again, and a link's conditions are not judged by it.
    private bool _conditionFieldsFailed;

    private Task<IReadOnlyList<ConditionField>>? _conditionFieldsLoad;

    // Bumped on every open, so a slow fields answer from an earlier open adds no rows to a later one.
    private int _advancedOpenings;

    private readonly List<ConditionDraft> _drafts = [];
    private int _nextDraftKey;

    private ElementReference _advancedToggle;
    private ElementReference _addConditionButton;
    private readonly Dictionary<int, ElementReference> _draftFieldSelects = [];

    // What to focus once the form has drawn: a row's field list, the add button, or the toggle.
    private int? _focusDraft;
    private bool _focusAddCondition;
    private bool _focusAdvancedToggle;

    // Set when the link's conditions could not all be kept, so the host is told the filter actually in force.
    private bool _conditionsDropped;

    private string AdvancedPanelId => $"munin-explorer-advanced-{_instance}";

    private string AdvancedLabel =>
        Pending.Conditions.Count > 0 ? T.AdvancedSearchCount(Pending.Conditions.Count) : T.AdvancedSearch;

    private string DraftId(ConditionDraft draft, string part) => $"munin-explorer-condition-{_instance}-{draft.Key}-{part}";

    /// <summary>Before the first fetch, drop the conditions a link carries that nobody could see (feature off) or that
    /// the API would refuse (one refusal is a 400 for the whole search).</summary>
    private async Task PrepareLinkedConditionsAsync()
    {
        if (_filter.Conditions.Count == 0)
        {
            return;
        }

        IReadOnlyList<SearchCondition> kept = [];
        if (AdvancedSearchOffered)
        {
            var fields = await ConditionFieldsAsync();
            kept = _conditionFieldsFailed
                ? [.. _filter.Conditions.Distinct(SameCondition.Instance)]
                : [.. _filter.Conditions.Distinct(SameCondition.Instance)
                    .Where(condition => FieldOf(fields, condition.Field)?.Accepts(condition) == true)];
        }

        if (kept.Count != _filter.Conditions.Count)
        {
            _filter = _filter with { Conditions = kept };
            _conditionsDropped = true;
        }

        if (kept.Count > 0)
        {
            await LoadVocabularyAsync();
        }
    }

    // Field and operator are matched ignoring case, as Accepts matches them, so «Navn» and «navn» are one condition.
    private sealed class SameCondition : IEqualityComparer<SearchCondition>
    {
        internal static readonly SameCondition Instance = new();

        public bool Equals(SearchCondition? x, SearchCondition? y) =>
            ReferenceEquals(x, y) || (x is not null && y is not null
                && string.Equals(x.Field, y.Field, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Operator, y.Operator, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Value, y.Value, StringComparison.Ordinal));

        public int GetHashCode(SearchCondition condition) => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(condition.Field),
            StringComparer.OrdinalIgnoreCase.GetHashCode(condition.Operator),
            condition.Value);
    }

    private async Task<IReadOnlyList<ConditionField>> ConditionFieldsAsync()
    {
        if (_conditionFields is { } known && !_conditionFieldsFailed)
        {
            return known;
        }

        // One call however many ask while it is out, so a late failure cannot overwrite an answer; a failure is asked again.
        var load = _conditionFieldsLoad ??= FetchConditionFieldsAsync();
        var fields = await load;
        if (_conditionFieldsFailed && ReferenceEquals(_conditionFieldsLoad, load))
        {
            _conditionFieldsLoad = null;
        }

        return fields;
    }

    private async Task<IReadOnlyList<ConditionField>> FetchConditionFieldsAsync()
    {
        try
        {
            _conditionFields = await Client.GetConditionFieldsAsync();
            _conditionFieldsFailed = false;
        }
        catch (Exception ex)
        {
            // Warning: the search still works without the form, and the form says it is unavailable.
            Log?.LogWarning(ex, "could not load the avansert søk fields");
            _conditionFields = [];
            _conditionFieldsFailed = true;
        }

        return _conditionFields;
    }

    private static ConditionField? FieldOf(IReadOnlyList<ConditionField> fields, string token) =>
        fields.FirstOrDefault(field => string.Equals(field.Field, token, StringComparison.OrdinalIgnoreCase));

    private Task ToggleAdvancedSearchAsync() => _advancedOpen ? CloseAdvancedSearchAsync() : OpenAdvancedSearchAsync();

    private Task CloseAdvancedSearchAsync()
    {
        _advancedOpen = false;
        return Task.CompletedTask;
    }

    /// <summary>Open the form on the conditions in force, or on one empty row when there are none.</summary>
    private async Task OpenAdvancedSearchAsync()
    {
        _advancedOpen = true;
        _focusAddCondition = false;
        _drafts.Clear();
        _draftFieldSelects.Clear();
        var opening = ++_advancedOpenings;
        _advancedLoading = true;

        try
        {
            await FillDraftsAsync(opening);
        }
        finally
        {
            if (opening == _advancedOpenings)
            {
                _advancedLoading = false;
            }
        }
    }

    private async Task FillDraftsAsync(int opening)
    {
        var fields = await ConditionFieldsAsync();
        if (fields.Count == 0 || opening != _advancedOpenings || !_advancedOpen)
        {
            return;
        }

        await LoadVocabularyAsync();
        if (opening != _advancedOpenings || !_advancedOpen)
        {
            return;
        }

        foreach (var condition in Pending.Conditions)
        {
            if (FieldOf(fields, condition.Field) is { } field)
            {
                _drafts.Add(DraftFrom(field, condition));
            }
        }

        if (_drafts.Count == 0)
        {
            _drafts.Add(NewDraft(fields[0]));
        }

        _focusDraft = _drafts[0].Key;
    }

    private ConditionDraft NewDraft(ConditionField field)
    {
        var draft = new ConditionDraft(_nextDraftKey++);
        SetField(draft, field);
        return draft;
    }

    private ConditionDraft DraftFrom(ConditionField field, SearchCondition condition)
    {
        var draft = NewDraft(field);
        draft.Operator = field.Operators.FirstOrDefault(op => string.Equals(op, condition.Operator, StringComparison.OrdinalIgnoreCase))
            ?? draft.Operator;

        if (field.IsCodeList)
        {
            draft.Codes.UnionWith(ConditionField.Codes(condition.Value));
        }
        else if (field.Type == ConditionField.DateType && ConditionField.TryReadRange(condition.Value, out var from, out var to))
        {
            (draft.From, draft.To) = (Iso(from), Iso(to));
        }
        else if (field.Type == ConditionField.DateType)
        {
            draft.From = condition.Value;
        }
        else
        {
            draft.Text = condition.Value;
        }

        return draft;
    }

    private static string Iso(DateOnly day) => day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static void SetField(ConditionDraft draft, ConditionField field)
    {
        draft.Field = field.Field;
        draft.Operator = field.Operators.FirstOrDefault() ?? "";
        draft.Text = draft.From = draft.To = "";
        draft.Codes.Clear();
        draft.Error = null;
    }

    private ConditionField? DraftField(ConditionDraft draft) =>
        _conditionFields is { } fields ? FieldOf(fields, draft.Field) : null;

    private void ChooseField(ConditionDraft draft, ChangeEventArgs change)
    {
        if (_conditionFields is { } fields && FieldOf(fields, change.Value as string ?? "") is { } field)
        {
            SetField(draft, field);
        }
    }

    private static void ChooseOperator(ConditionDraft draft, ChangeEventArgs change)
    {
        draft.Operator = change.Value as string ?? draft.Operator;
        draft.Error = null;
    }

    // Set from the box's own state, so a lost or repeated change event cannot leave the set inverted against it.
    private static void SetCode(ConditionDraft draft, string code, bool ticked)
    {
        if (ticked)
        {
            draft.Codes.Add(code);
        }
        else
        {
            draft.Codes.Remove(code);
        }
    }

    private void AddCondition()
    {
        if (_conditionFields is not { Count: > 0 } fields || _drafts.Count >= SearchCondition.MaxCount)
        {
            return;
        }

        var draft = NewDraft(fields[0]);
        _drafts.Add(draft);
        _focusDraft = draft.Key;
    }

    /// <summary>Remove a row, and hand focus to the row that takes its place, the one before, or the add button.</summary>
    private void RemoveDraft(ConditionDraft draft)
    {
        var index = _drafts.IndexOf(draft);
        if (index < 0)
        {
            return;
        }

        _drafts.RemoveAt(index);
        _draftFieldSelects.Remove(draft.Key);

        if (_drafts.Count == 0)
        {
            _focusAddCondition = true;
        }
        else
        {
            _focusDraft = _drafts[Math.Min(index, _drafts.Count - 1)].Key;
        }
    }

    private void ResetConditions()
    {
        _drafts.Clear();
        _draftFieldSelects.Clear();

        if (_conditionFields is { Count: > 0 } fields)
        {
            var draft = NewDraft(fields[0]);
            _drafts.Add(draft);
            _focusDraft = draft.Key;
        }
    }

    /// <summary>Apply the rows: an empty row is left out, and a row that cannot be read stops the lot.</summary>
    private async Task ShowConditionHitsAsync()
    {
        List<SearchCondition> conditions = [];
        ConditionDraft? firstRefused = null;
        var opening = _advancedOpenings;
        var rows = _drafts.ToList();

        foreach (var draft in rows)
        {
            draft.Error = null;

            if (DraftField(draft) is not { } field)
            {
                continue;
            }

            var (value, error) = await HoldsBadNativeDateAsync(draft, field)
                ? (null, T.ConditionDateInvalid)
                : DraftValue(draft, field);
            draft.Error = error;
            firstRefused ??= error is null ? null : draft;

            // Two rows saying the same thing would draw two chips with one name, and one press would empty both.
            if (value is not null && new SearchCondition(field.Field, draft.Operator, value) is var condition
                && !conditions.Contains(condition))
            {
                conditions.Add(condition);
            }
        }

        // The browser was asked between rows: a second press, a close, «Nullstill» or a removal may have landed meanwhile.
        if (opening != _advancedOpenings || !_advancedOpen || !rows.SequenceEqual(_drafts))
        {
            return;
        }

        if (firstRefused is not null)
        {
            _focusDraft = firstRefused.Key;
            return;
        }

        _advancedOpen = false;
        _focusAdvancedToggle = true;
        await ApplyFilterAsync(Pending with { Conditions = conditions });
    }

    // A native date reports "" for a half-typed or impossible entry as well as for a cleared one.
    private async Task<bool> HoldsBadNativeDateAsync(ConditionDraft draft, ConditionField field)
    {
        if (field.Type != ConditionField.DateType || _interop is not { } interop)
        {
            return false;
        }

        var between = string.Equals(draft.Operator, ConditionField.BetweenOperator, StringComparison.OrdinalIgnoreCase);
        return (draft.From.Trim().Length == 0 && (await interop.ReadDateInputAsync(DraftId(draft, "from")))?.BadInput == true)
            || (between && draft.To.Trim().Length == 0 && (await interop.ReadDateInputAsync(DraftId(draft, "to")))?.BadInput == true);
    }

    /// <summary>The value in the API's form, null for an empty row, or the reason the row cannot be read.</summary>
    private (string? Value, string? Error) DraftValue(ConditionDraft draft, ConditionField field)
    {
        if (field.IsCodeList)
        {
            // In the API's order, so a link is the same whichever box was ticked first.
            var codes = field.Options.Where(draft.Codes.Contains).ToList();
            return (codes.Count == 0 ? null : string.Join(',', codes), null);
        }

        if (field.Type != ConditionField.DateType)
        {
            // A pasted tab or line break would apply here and then be refused when a link is read back.
            var text = string.Concat(draft.Text.Select(c => char.IsControl(c) ? ' ' : c)).Trim();
            return (text.Length == 0 ? null : text[..Math.Min(text.Length, SearchCondition.MaxTextLength)], null);
        }

        var from = draft.From.Trim();
        var to = draft.To.Trim();

        if (!string.Equals(draft.Operator, ConditionField.BetweenOperator, StringComparison.OrdinalIgnoreCase))
        {
            return from.Length == 0 ? (null, null)
                : ConditionField.TryReadDay(from, out _) ? (from, null)
                : (null, T.ConditionDateInvalid);
        }

        if (from.Length == 0 && to.Length == 0)
        {
            return (null, null);
        }

        var range = $"{from}..{to}";
        return ConditionField.TryReadRange(range, out _, out _) ? (range, null) : (null, T.ConditionRangeInvalid);
    }

    /// <summary>Open the form from the chip row, or, when it is open already, keep its rows and move focus into it.</summary>
    private Task EditAdvancedSearchAsync()
    {
        if (!_advancedOpen)
        {
            return OpenAdvancedSearchAsync();
        }

        // The opening still loading puts focus on its first row itself.
        if (_advancedLoading)
        {
            return Task.CompletedTask;
        }

        _focusDraft = _drafts.FirstOrDefault()?.Key;
        _focusAddCondition = _focusDraft is null;
        return Task.CompletedTask;
    }

    /// <summary>One chip per condition, after the facets', each removing only itself.</summary>
    private IEnumerable<ActiveFilters.Chip> ConditionChips()
    {
        var conditions = Pending.Conditions;

        foreach (var condition in conditions)
        {
            var text = ConditionReading(condition);
            yield return new ActiveFilters.Chip(text, T.RemoveFilter(text), null, null,
                () => RemoveFilterAsync(() => RemoveConditionAsync(condition)));
        }
    }

    // By value, not position: a second press of the same chip finds it gone and removes nothing more.
    private Task RemoveConditionAsync(SearchCondition condition)
    {
        var conditions = Pending.Conditions.ToList();
        return conditions.Remove(condition) ? ApplyFilterAsync(Pending with { Conditions = conditions }) : Task.CompletedTask;
    }

    // A token with no word here reads as itself, so a field the API adds later still shows.
    private string FieldName(string token) => T.ConditionFieldNames.GetValueOrDefault(token) ?? token;

    private string OperatorName(string token) => T.ConditionOperatorNames.GetValueOrDefault(token) ?? token;

    // Not a literal in the markup: beside type="date", @bind converts to DateTime, and a row keeps the browser's own text.
    private const string NativeDateType = "date";

    // With the fields unknown, only a date operator says a value is a day; a text can look like one.
    private static readonly string[] DateOperators = [ConditionField.BetweenOperator, "for", "etter"];

    /// <summary>A condition in words: «Beskrivelse inneholder «blodtrykk»», «Tilgangsnivå er en av Statistikkbank».</summary>
    private string ConditionReading(SearchCondition condition)
    {
        var field = FieldName(condition.Field);
        var op = OperatorName(condition.Operator);

        var known = _conditionFields is { } fields ? FieldOf(fields, condition.Field) : null;
        var dated = known is null
            ? DateOperators.Contains(condition.Operator, StringComparer.OrdinalIgnoreCase)
            : known.Type == ConditionField.DateType;

        string value;
        if (dated && ConditionField.TryReadRange(condition.Value, out var from, out var to))
        {
            value = $"{CatalogueDate.Day(from, Language)}–{CatalogueDate.Day(to, Language)}";
        }
        else if (dated && ConditionField.TryReadDay(condition.Value, out var day))
        {
            value = CatalogueDate.Day(day, Language);
        }
        else if (known is { IsCodeList: true })
        {
            value = string.Join(", ", ConditionField.Codes(condition.Value).Select(code => CodeName(condition.Field, code)));
        }
        else
        {
            value = T.QuotedConditionValue(condition.Value);
        }

        return T.ConditionText(field, op, value);
    }

    private string CodeName(string field, string code) => CodeWord(field, code).Text;

    /// <summary>A code's word, and its language where that is not the reader's: the catalogue's own for
    /// tilgangsnivå, this package's for personidentifikasjon, else the code.</summary>
    private (string Text, string? Language) CodeWord(string field, string code)
    {
        if (string.Equals(field, "tilgangsniva", StringComparison.OrdinalIgnoreCase)
            && _vocabulary.TryGetValue(AccessLevels.Key, out var entry)
            && CatalogueProperties.Option(entry, code, Reader) is { Curated: true } word)
        {
            return (word.Label, Foreign(word.Language));
        }

        return string.Equals(field, "personidentifikasjon", StringComparison.OrdinalIgnoreCase)
            && T.PersonIdentificationNames.TryGetValue(code, out var name)
            ? (name, null)
            : (code, null);
    }

    /// <summary>Called from <see cref="OnAfterRenderAsync"/>: focus what the last press asked for, once it is drawn.</summary>
    private async Task FocusAdvancedSearchAsync()
    {
        ElementReference? target = null;
        if (_focusDraft is { } key && _draftFieldSelects.TryGetValue(key, out var select) && select.Context is not null)
        {
            (_focusDraft, target) = (null, select);
        }
        else if (_focusAddCondition && _addConditionButton.Context is not null)
        {
            (_focusAddCondition, target) = (false, _addConditionButton);
        }
        else if (_focusAdvancedToggle && _advancedToggle.Context is not null)
        {
            (_focusAdvancedToggle, target) = (false, _advancedToggle);
        }

        try
        {
            if (target is { } element)
            {
                await element.FocusAsync();
            }
        }
        catch (JSException)
        {
            // An element a later render removed refuses focus; the reader is no worse off than before.
        }
    }
}
