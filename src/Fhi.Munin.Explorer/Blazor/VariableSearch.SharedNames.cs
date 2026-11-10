using Fhi.Munin.Explorer.Contracts;

namespace Fhi.Munin.Explorer.Blazor;

// Rows that share a name on one page, such as a registry's numbered procedure slots, are told apart by their code,
// drawn under the name the way an own list draws it (Fhi.Metadata-l9l2n.161).
public partial class VariableSearch
{
    private Page<VariableSummary>? _sharedNamesOf;
    private string? _sharedNamesReader;
    private HashSet<string> _sharedNames = [];

    /// <summary>The code, when this row's name is on another row of the page and the Kode column is off.</summary>
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
                : [.. _result.Items.GroupBy(ShownName, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key)];
        }

        return _sharedNames;
    }
}
