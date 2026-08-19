category: Changed

- **Row cells no longer repeat the column name** - every cell said "Datakilde: Als registeret" because
  there was no header row to name the field. There is one now, and repeating the name in all
  twenty-five rows is exactly what a header exists to stop. The label survives as each cell's
  `aria-label`, so a screen reader moving across a row still hears which field it is on.
- **The Status column is drawn only when historical variables can be in the list** - the API computes
  `VersjonStatus` from `GyldigTil` and filters expired versions out unless `IncludeHistorical` is
  asked for, so in the default view every row reads "Active". Verified against the live API: 100 rows
  sampled across five pages of the catalogue, all Active. A column that says the same word on every
  row is furniture, so it appears with the historical filter and not before. (Fhi.Metadata-zs56s)
