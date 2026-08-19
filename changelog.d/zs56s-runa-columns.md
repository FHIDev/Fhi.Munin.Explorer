category: Changed

- **The result columns are Runa's, not the page being replaced** - Navn, Kode, Kilde, Datasamling,
  Variabelgruppe, Datatype and Status, which is Runa's column set. Runa is what helsedata's variable
  page is being replaced *with*, so it decides what a row says; helsedata decides what a row looks
  like. Taking the column set from the page being retired would have been copying the thing we are
  replacing. Four of the seven have a width modifier in helsedata's stylesheet; Kode, Datatype and
  Status do not, so they wear the bare `variable-dataitem-main__column` and size by content under
  their flex layout — using a class of theirs without a modifier, rather than inventing
  `__code`/`__dataType`/`__status`, which would be names with no rule behind them. Those three
  modifiers are worth asking for in the SCSS file helsedata offered. Periode is not a Runa column
  and is no longer a row column; it remains in the panel. (Fhi.Metadata-zs56s)
