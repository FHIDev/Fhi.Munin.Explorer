using Fhi.Munin.Explorer.Contracts;
using Microsoft.AspNetCore.Components;

namespace Fhi.Munin.Explorer.Blazor;

/// <summary>Which columns the kilde table carries, and the control that turns them on and off.</summary>
public sealed partial class KildeExplorer
{
    /// <summary>
    /// A column the reader can turn off.
    /// </summary>
    /// <remarks>
    /// Kelda's ten, in the order its own picker lists them (<c>kelda.tsx</c>,
    /// <c>OPTIONAL_COLUMNS</c>). Navn, Status and Opprettet are not among them, in Kelda either.
    /// Which field each of the two dates reads is on <see cref="Imported"/> and
    /// <see cref="SourceUpdated"/> below, where a reader meets it.
    /// </remarks>
    private enum KildeColumn
    {
        Kildetype,
        Datasamlinger,
        Variabler,
        Delkilder,
        DataController,
        DataProcessor,
        PersonIdentification,
        Validity,
        Imported,
        SourceUpdated,
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
    /// Kildetype, Datasamlinger and Variabler are on; the other seven are off. Held as what is
    /// hidden rather than as what is shown, so the table's default view is the one this component
    /// already shipped and a column added to the enum appears rather than disappears.
    /// <para>
    /// Not persisted and not in the host's URL, which is what Kelda does today: the choice lasts as
    /// long as the page does. Whether it should be remembered is a decision of its own — this
    /// component owns no storage and no URL (Fhi.Metadata-ay3zz).
    /// </para>
    /// </remarks>
    private readonly HashSet<KildeColumn> _hiddenColumns =
    [
        KildeColumn.Delkilder,
        KildeColumn.DataController,
        KildeColumn.DataProcessor,
        KildeColumn.PersonIdentification,
        KildeColumn.Validity,
        KildeColumn.Imported,
        KildeColumn.SourceUpdated,
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
    private void ToggleColumn(KildeColumn column)
    {
        if (!_hiddenColumns.Remove(column))
        {
            _hiddenColumns.Add(column);
        }
    }

    /// <summary>A column's name, in the words the header above it uses.</summary>
    /// <remarks>
    /// The same strings the header cells carry, so the picker and the column it turns off are never
    /// two names for one thing. An unknown member throws for the reason <see cref="Texts.FieldLabel"/>
    /// does: a column added to <see cref="KildeColumn"/> without a word here would sit unlabelled.
    /// </remarks>
    private string ColumnLabel(KildeColumn column) => column switch
    {
        KildeColumn.Kildetype => T.ColumnKildetype,
        KildeColumn.Datasamlinger => T.HeadingDataCollections,
        KildeColumn.Variabler => T.ColumnVariableCount,
        KildeColumn.Delkilder => T.ColumnDelkildeCount,
        KildeColumn.DataController => T.FieldDataController,
        KildeColumn.DataProcessor => T.FieldDataProcessor,
        KildeColumn.PersonIdentification => T.FieldPersonIdentification,
        KildeColumn.Validity => T.FieldValidity,
        KildeColumn.Imported => T.ColumnImported,
        KildeColumn.SourceUpdated => T.ColumnSourceUpdated,
        _ => throw new ArgumentOutOfRangeException(nameof(column), column, "No label for this column.")
    };

    /// <summary>The picker, drawn by the shared <see cref="Fhi.Munin.Explorer.Blazor.ColumnPicker"/>.</summary>
    /// <remarks>
    /// No hint, because no column here can lock — see <see cref="ToggleColumn"/>. The markup and
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
                () => ToggleColumn(column)))]);

    /// <summary>The validity period, with an open end read as ongoing.</summary>
    /// <remarks>
    /// <see cref="CatalogueDate.Period"/> rather than a copy: it already decides what an open end
    /// and a missing start mean, and the kilde view draws the same field through it. Kelda writes a
    /// missing start as "?"; the shared helper lets an end stand alone instead, on the ground that a
    /// start the catalogue never gave would be an invention (Fhi.Metadata-n39ea).
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
