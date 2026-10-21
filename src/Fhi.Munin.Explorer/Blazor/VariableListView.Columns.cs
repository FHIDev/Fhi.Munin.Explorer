using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

// The list's own columns: six fit 1280, so four start hidden behind the shared picker (Fhi.Metadata-b2w2z).
public sealed partial class VariableListView
{
    private enum ListColumn
    {
        Source,
        DataCollection,
        VariableGroup,
        DataType,
        DataPeriod,
        Kodeverk,
        Statistics
    }

    private static readonly ListColumn[] OfferedListColumns = Enum.GetValues<ListColumn>();

    private readonly HashSet<ListColumn> _hiddenListColumns =
        [ListColumn.DataType, ListColumn.DataPeriod, ListColumn.Kodeverk, ListColumn.Statistics];

    private bool Shown(ListColumn column) => !_hiddenListColumns.Contains(column);

    private void ToggleListColumn(ListColumn column)
    {
        if (!_hiddenListColumns.Remove(column))
        {
            _hiddenListColumns.Add(column);
        }
    }

    private string ListColumnLabel(ListColumn column) => column switch
    {
        ListColumn.Source => T.FieldSource,
        ListColumn.DataCollection => T.FieldDataCollection,
        ListColumn.VariableGroup => T.FieldVariableGroup,
        ListColumn.DataType => T.FieldDataType,
        ListColumn.DataPeriod => T.FieldDataPeriod,
        ListColumn.Kodeverk => T.FieldKodeverk,
        ListColumn.Statistics => T.FieldStatistics,
        _ => throw new ArgumentOutOfRangeException(nameof(column), column, "No label for this column.")
    };

    // The name, Ønskede data and «Valg» always, and whichever of the rest are shown. The opened row spans them.
    private int OwnListColumnCount => 3 + OfferedListColumns.Count(Shown);

    // One fold at a time, «Kolonner» among them: opening it shuts «Last ned» and «Flere valg» as theirs shut it.
    private ColumnsFold? _columns;

    private ColumnsFold Columns => _columns ??= new(
        _instance,
        () =>
        {
            _menuOpen = false;
            _downloadOpen = false;
            DisarmConfirmations();
        },
        moved: () => _readerMoves++);

    private RenderFragment ListColumnPicker() =>
        ColumnPicker.For(
            this,
            Columns,
            T.Columns,
            T.CloseMenu,
            [.. OfferedListColumns.Select(column => new ColumnPicker.Choice(
                ListColumnLabel(column),
                Shown(column),
                Locked: false,
                () =>
                {
                    ToggleListColumn(column);

                    return Task.CompletedTask;
                }))]);
}
