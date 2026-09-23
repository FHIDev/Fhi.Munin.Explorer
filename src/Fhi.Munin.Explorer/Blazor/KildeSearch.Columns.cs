using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Which columns the kilde table carries, and the control that turns them on and off.</summary>
public sealed partial class KildeSearch
{
    /// <summary>
    /// A column the reader can turn off.
    /// </summary>
    /// <remarks>
    /// Kelda's eleven, in the order its own picker lists them (<c>kelda.tsx</c>,
    /// <c>OPTIONAL_COLUMNS</c>), then the two coverage shares Kelda does not have
    /// (Fhi.Metadata-l9l2n.98). Navn, Status and Opprettet are not among them, in Kelda either.
    /// Which field each of the two dates reads is on <see cref="Imported"/> and
    /// <see cref="SourceUpdated"/> below, where a reader meets it.
    /// </remarks>
    private enum KildeColumn
    {
        Code,
        Kildetype,
        Datasamlinger,
        Variables,
        Delkilder,
        DataController,
        DataProcessor,
        PersonIdentification,
        Validity,
        Imported,
        SourceUpdated,
        KodeverkShare,
        StatisticsShare,
    }

    /// <summary>The columns the picker offers, in the order it lists them.</summary>
    /// <remarks>
    /// The enum itself rather than a list restating it, for the reason the variable explorer's is:
    /// a column added above without a line here would be one the reader could see and not turn off.
    /// </remarks>
    private static readonly KildeColumn[] OptionalColumns = Enum.GetValues<KildeColumn>();

    /// <summary>
    /// The columns that start turned off, which is Kelda's own default set.
    /// </summary>
    /// <remarks>
    /// Kildetype, Datasamlinger and Variables are on; the other ten are off. Held as what is
    /// hidden rather than as what is shown, so the table's default view is the one this component
    /// already shipped and a column added to the enum appears rather than disappears.
    /// <para>
    /// Not persisted by this component, which owns no storage and no URL (Fhi.Metadata-ay3zz): it
    /// leaves through <see cref="VisibleColumnsChanged"/> for a host that keeps it in its address.
    /// </para>
    /// </remarks>
    private readonly HashSet<KildeColumn> _hiddenColumns = [.. DefaultHidden];

    private static readonly KildeColumn[] DefaultHidden =
    [
        KildeColumn.Code,
        KildeColumn.Delkilder,
        KildeColumn.DataController,
        KildeColumn.DataProcessor,
        KildeColumn.PersonIdentification,
        KildeColumn.Validity,
        KildeColumn.Imported,
        KildeColumn.SourceUpdated,
        KildeColumn.KodeverkShare,
        KildeColumn.StatisticsShare,
    ];

    /// <summary>Whether a column is on screen.</summary>
    private bool ColumnVisible(KildeColumn column) => !_hiddenColumns.Contains(column);

    /// <summary>Turns a column on or off.</summary>
    /// <remarks>
    /// No last-column lock, unlike the variable explorer's picker. There the seven are every column
    /// a row has, so hiding all of them leaves rows of nothing but names; here Navn, Status and
    /// Opprettet are drawn whatever the picker says, so the emptiest table this control can reach
    /// still says what each kilde is and whether it is active. Kelda has no lock either.
    /// </remarks>
    private Task ToggleColumnAsync(KildeColumn column)
    {
        if (!_hiddenColumns.Remove(column))
        {
            _hiddenColumns.Add(column);
        }

        return RaiseAsync(VisibleColumnsChanged, VisibleColumnKeys(), Log);
    }

    /// <summary>
    /// The optional columns on screen when the list opens, by <see cref="ColumnKeys"/> — null for
    /// the default set. Set by the host, typically from its own URL; the component owns the choice
    /// afterwards.
    /// </summary>
    /// <remarks>
    /// Read once, on initialisation, as <see cref="Search"/> is. An empty list hides every optional
    /// column; a key outside <see cref="ColumnKeys"/> is dropped. Navn, Status and Opprettet are
    /// drawn whatever this says.
    /// </remarks>
    [Parameter] public IReadOnlyList<string>? VisibleColumns { get; set; }

    /// <summary>
    /// Raised on every press in the column picker, with the optional columns now on screen in
    /// <see cref="ColumnKeys"/> order — and null when that is the default set again. Gives a host
    /// <c>@bind-VisibleColumns</c>.
    /// </summary>
    [Parameter] public EventCallback<IReadOnlyList<string>?> VisibleColumnsChanged { get; set; }

    /// <summary>
    /// Every optional column's key, in the order the picker lists them. The names are Munin's own
    /// Kelda's where it has the same column, <c>kode</c> for the code, and the API's own field
    /// names, <c>andelKodeverk</c> and <c>andelStatistikk</c>, for the two coverage shares.
    /// </summary>
    public static IReadOnlyList<string> ColumnKeys { get; } = [.. OptionalColumns.Select(ColumnKey)];

    /// <summary>The keys of the columns on screen before the reader chooses, in picker order.</summary>
    internal static IReadOnlyList<string> DefaultColumnKeys { get; } =
        [.. OptionalColumns.Where(column => !DefaultHidden.Contains(column)).Select(ColumnKey)];

    private static string ColumnKey(KildeColumn column) => column switch
    {
        KildeColumn.Code => "kode",
        KildeColumn.Kildetype => "kildetype",
        KildeColumn.Datasamlinger => "datasamlinger",
        KildeColumn.Variables => "variabler",
        KildeColumn.Delkilder => "delkilder",
        KildeColumn.DataController => "dataansvarlig",
        KildeColumn.DataProcessor => "databehandler",
        KildeColumn.PersonIdentification => "grad",
        KildeColumn.Validity => "gyldighetsperiode",
        KildeColumn.Imported => "importert",
        KildeColumn.SourceUpdated => "sistEndret",
        KildeColumn.KodeverkShare => "andelKodeverk",
        KildeColumn.StatisticsShare => "andelStatistikk",
        _ => throw new ArgumentOutOfRangeException(nameof(column), column, "No key for this column.")
    };

    /// <summary>
    /// A header cell's handle: the stem finished with the column's key, or with <c>expand</c>,
    /// <c>select</c>, <c>navn</c>, <c>status</c> or <c>opprettet</c> for the cells the picker cannot reach.
    /// </summary>
    /// <remarks>
    /// Stiler keys the sticky head on which wide columns are shown, which the scroll box's count
    /// cannot tell it (Fhi.Metadata-35w0p.74). The optional keys are <see cref="ColumnKey"/>'s own.
    /// </remarks>
    internal static string HeaderClass(string key) => $"munin-explorer-kilder-header__{key}";

    private static string HeaderClass(KildeColumn column) => HeaderClass(ColumnKey(column));

    private void SeedColumns()
    {
        if (VisibleColumns is not { } shown)
        {
            return;
        }

        _hiddenColumns.Clear();
        _hiddenColumns.UnionWith(OptionalColumns.Where(column =>
            !shown.Contains(ColumnKey(column), StringComparer.OrdinalIgnoreCase)));
    }

    private IReadOnlyList<string>? VisibleColumnKeys() =>
        _hiddenColumns.SetEquals(DefaultHidden)
            ? null
            : [.. OptionalColumns.Where(ColumnVisible).Select(ColumnKey)];

    /// <summary>A column's name, in the words the header above it uses.</summary>
    /// <remarks>
    /// The same strings the header cells carry, so the picker and the column it turns off are never
    /// two names for one thing. An unknown member throws for the reason <see cref="Texts.FieldLabel"/>
    /// does: a column added to <see cref="KildeColumn"/> without a word here would sit unlabelled.
    /// </remarks>
    private string ColumnLabel(KildeColumn column) => column switch
    {
        KildeColumn.Code => T.FieldCode,
        KildeColumn.Kildetype => T.ColumnKildetype,
        KildeColumn.Datasamlinger => T.HeadingDataCollections,
        KildeColumn.Variables => T.ColumnVariableCount,
        KildeColumn.Delkilder => T.ColumnDelkildeCount,
        KildeColumn.DataController => T.FieldDataController,
        KildeColumn.DataProcessor => T.FieldDataProcessor,
        KildeColumn.PersonIdentification => T.FieldPersonIdentification,
        KildeColumn.Validity => T.FieldValidity,
        KildeColumn.Imported => T.ColumnImported,
        KildeColumn.SourceUpdated => T.ColumnSourceUpdated,
        KildeColumn.KodeverkShare => T.ColumnKodeverkShare,
        KildeColumn.StatisticsShare => T.ColumnStatisticsShare,
        _ => throw new ArgumentOutOfRangeException(nameof(column), column, "No label for this column.")
    };

    /// <summary>The picker, drawn by the shared <see cref="Fhi.Munin.Explorer.Blazor.ColumnPicker"/>.</summary>
    /// <remarks>
    /// No hint, because no column here can lock — see <see cref="ToggleColumnAsync"/>. The markup and
    /// the borrowed Stiler names are the variable explorer's, shared rather than copied.
    /// </remarks>
    private RenderFragment ColumnPicker() =>
        Blazor.ColumnPicker.For(
            this,
            T.Columns,
            [.. OptionalColumns.Select(column => new Blazor.ColumnPicker.Choice(
                ColumnLabel(column),
                ColumnVisible(column),
                Locked: false,
                () => ToggleColumnAsync(column)))]);

    /// <summary>The validity period, with an open end read as ongoing.</summary>
    /// <remarks>
    /// <see cref="CatalogueDate.Period"/> rather than a copy: one helper decides what an open end
    /// and an unknown start mean, so this column and the kilde view beside it cannot word the same
    /// kilde's validity two ways (Fhi.Metadata-msax9).
    /// </remarks>
    private string ValidityPeriod(KildeSummary kilde) =>
        Value(CatalogueDate.Period(kilde.ValidFrom, kilde.ValidTo, Language, T, DateWidth.Narrow));

    /// <summary>When Munin imported the kilde — its own row, not the catalogue's founding year.</summary>
    /// <remarks>
    /// <see cref="KildeSummary.Created"/>, which the Opprettet column beside it does not read: that
    /// one is <c>additionalProperties.Opprettet</c>, a year the catalogue wrote as text. Through
    /// <see cref="CatalogueDate.DayOrNothing"/>, since a missing timestamp is not this column's
    /// problem alone — the kilde and datasamling views draw the same field (Fhi.Metadata-6r6rf).
    /// </remarks>
    private string Imported(KildeSummary kilde) =>
        Value(CatalogueDate.DayOrNothing(kilde.Created, Language, DateWidth.Narrow));

    /// <summary>When the source system last changed the kilde, as the catalogue writes it down.</summary>
    /// <remarks>
    /// <c>additionalProperties.SistOppdatert</c>, a compact <c>yyyyMMdd</c> string, and never
    /// <see cref="KildeSummary.LastUpdated"/> — that one is when Munin's row changed, which is a
    /// different question and already has a home in the kilde view.
    /// </remarks>
    private string SourceUpdated(KildeSummary kilde) =>
        Value(CatalogueDate.SourceSystemDay(Property(kilde, "SistOppdatert"), Language, DateWidth.Narrow));
}
