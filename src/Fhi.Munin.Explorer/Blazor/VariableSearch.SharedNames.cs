using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Blazor;

// Rows that share a name on one page, such as a registry's numbered procedure slots, are told apart by their code,
// drawn under the name the way an own list draws it (Fhi.Metadata-l9l2n.161).
public partial class VariableSearch
{
    private Page<VariableSummary>? _sharedNamesOf;
    private string? _sharedNamesReader;
    private HashSet<string> _sharedNames = [];

    /// <summary>The code, when another row of the page has this row's name and another code, and the Kode column is off.</summary>
    private string? CodeUnderName(VariableSummary v) =>
        !ColumnVisible(ResultColumn.Code)
        && !string.IsNullOrWhiteSpace(v.Code)
        && SharedNames().Contains(ShownName(v))
            ? v.Code
            : null;

    private string RowCodeId(VariableSummary v) => $"munin-explorer-code-{_instance}-{RowSuffix(v)}";

    // The row's name span, then its code span where one is drawn, for the save buttons' aria-labelledby.
    private string RowNameIds(VariableSummary v) =>
        CodeUnderName(v) is null ? RowHeadingId(v) : $"{RowHeadingId(v)} {RowCodeId(v)}";

    private string ShownName(VariableSummary v) => ReaderLanguage.Pick(v.PreferredTerm, v.PreferredTermEn, Reader).Text ?? "";

    private HashSet<string> SharedNames()
    {
        if (!ReferenceEquals(_sharedNamesOf, _result) || _sharedNamesReader != Reader)
        {
            _sharedNamesOf = _result;
            _sharedNamesReader = Reader;
            _sharedNames = _result is null
                ? []
                : [.. _result.Items.GroupBy(ShownName, StringComparer.Ordinal).Where(HasTwoCodes).Select(g => g.Key)];
        }

        return _sharedNames;
    }

    // One variable has a row per datasamling, so a name repeated under a single code is one variable twice,
    // and its Datasamling column already tells those rows apart.
    private static bool HasTwoCodes(IEnumerable<VariableSummary> rows) =>
        rows.Select(v => v.Code).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal).Skip(1).Any();
}
