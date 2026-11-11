using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    // Null until asked for; empty when the API has none to offer or the call failed.
    private IReadOnlyList<ConditionField>? _conditionFields;

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
        _filter.Conditions.Count > 0 ? T.AdvancedSearchCount(_filter.Conditions.Count) : T.AdvancedSearch;

    private string DraftId(ConditionDraft draft, string part) => $"munin-explorer-condition-{_instance}-{draft.Key}-{part}";

    /// <summary>Keep only the conditions this explorer can show and the API will take, before the first fetch.</summary>
    /// <remarks>
    /// Off, every condition goes: a link is public input, and a narrowing nobody can see or remove is worse than none.
    /// On, a condition the API would refuse goes too, since one refusal is a 400 for the whole search.
    /// </remarks>
    private async Task PrepareLinkedConditionsAsync()
    {
        if (_filter.Conditions.Count == 0)
        {
            return;
        }

        var kept = AdvancedSearchOffered ? await AcceptedAsync(_filter.Conditions) : [];

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

    private async Task<IReadOnlyList<SearchCondition>> AcceptedAsync(IReadOnlyList<SearchCondition> conditions)
    {
        var fields = await ConditionFieldsAsync();
        return [.. conditions.Where(condition => FieldOf(fields, condition.Field)?.Accepts(condition) == true)];
    }

    private async Task<IReadOnlyList<ConditionField>> ConditionFieldsAsync()
    {
        if (_conditionFields is { } known)
        {
            return known;
        }

        try
        {
            _conditionFields = await Client.GetConditionFieldsAsync();
        }
        catch (Exception ex)
        {
            // Warning: the search still works without the form, and the form says it is unavailable.
            Log?.LogWarning(ex, "could not load the avansert søk fields");
            _conditionFields = [];
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
        _drafts.Clear();
        _draftFieldSelects.Clear();

        var fields = await ConditionFieldsAsync();
        if (fields.Count == 0)
        {
            return;
        }

        await LoadVocabularyAsync();

        foreach (var condition in _filter.Conditions)
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

    private static void ToggleCode(ConditionDraft draft, string code)
    {
        if (!draft.Codes.Remove(code))
        {
            draft.Codes.Add(code);
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

        foreach (var draft in _drafts)
        {
            draft.Error = null;

            if (DraftField(draft) is not { } field)
            {
                continue;
            }

            var (value, error) = DraftValue(draft, field);
            draft.Error = error;
            firstRefused ??= error is null ? null : draft;

            if (value is not null)
            {
                conditions.Add(new SearchCondition(field.Field, draft.Operator, value));
            }
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
            var text = draft.Text.Trim();
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

    /// <summary>Open the form from the chip row; the chip row stays, so focus moves into the form.</summary>
    private Task EditAdvancedSearchAsync() => OpenAdvancedSearchAsync();

    /// <summary>One chip per condition, after the facets', each removing only itself.</summary>
    private IEnumerable<ActiveFilters.Chip> ConditionChips()
    {
        var conditions = Pending.Conditions;

        for (var index = 0; index < conditions.Count; index++)
        {
            var position = index;
            var text = ConditionReading(conditions[index]);

            yield return new ActiveFilters.Chip(text, T.RemoveFilter(text), null, null,
                () => RemoveFilterAsync(() => ApplyFilterAsync(Pending with
                {
                    Conditions = [.. Pending.Conditions.Where((_, i) => i != position)]
                })));
        }
    }

    // A token with no word here reads as itself, so a field the API adds later still shows.
    private string FieldName(string token) => T.ConditionFieldNames.GetValueOrDefault(token) ?? token;

    private string OperatorName(string token) => T.ConditionOperatorNames.GetValueOrDefault(token) ?? token;

    /// <summary>A condition in words: «Beskrivelse inneholder «blodtrykk»», «Tilgangsnivå er en av Statistikkbank».</summary>
    private string ConditionReading(SearchCondition condition)
    {
        var field = FieldName(condition.Field);
        var op = OperatorName(condition.Operator);

        string value;
        if (ConditionField.TryReadRange(condition.Value, out var from, out var to))
        {
            value = $"{CatalogueDate.Day(from, Language)}–{CatalogueDate.Day(to, Language)}";
        }
        else if (ConditionField.TryReadDay(condition.Value, out var day))
        {
            value = CatalogueDate.Day(day, Language);
        }
        else if (_conditionFields is { } fields && FieldOf(fields, condition.Field) is { IsCodeList: true })
        {
            value = string.Join(", ", ConditionField.Codes(condition.Value).Select(code => CodeName(condition.Field, code)));
        }
        else
        {
            value = T.QuotedConditionValue(condition.Value);
        }

        return T.ConditionText(field, op, value);
    }

    /// <summary>A code's word: the catalogue's own for tilgangsnivå, this package's for personidentifikasjon, else the code.</summary>
    private string CodeName(string field, string code)
    {
        if (string.Equals(field, "tilgangsniva", StringComparison.OrdinalIgnoreCase)
            && _vocabulary.TryGetValue(AccessLevels.Key, out var entry)
            && CatalogueProperties.Option(entry, code, Language) is { } word)
        {
            return word.Label;
        }

        return string.Equals(field, "personidentifikasjon", StringComparison.OrdinalIgnoreCase)
            && T.PersonIdentificationNames.TryGetValue(code, out var name)
            ? name
            : code;
    }

    /// <summary>Called from <see cref="OnAfterRenderAsync"/>: focus what the last press asked for, once it is drawn.</summary>
    private async Task FocusAdvancedSearchAsync()
    {
        if (_focusDraft is { } key && _draftFieldSelects.TryGetValue(key, out var select) && select.Context is not null)
        {
            _focusDraft = null;
            await select.FocusAsync();
        }
        else if (_focusAddCondition && _addConditionButton.Context is not null)
        {
            _focusAddCondition = false;
            await _addConditionButton.FocusAsync();
        }
        else if (_focusAdvancedToggle && _advancedToggle.Context is not null)
        {
            _focusAdvancedToggle = false;
            await _advancedToggle.FocusAsync();
        }
    }
}
