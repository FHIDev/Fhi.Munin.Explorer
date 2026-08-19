category: Changed

- **Rows line up with their column headers** - three things were pulling them apart. The name was
  wrapped in a heading, which made the heading the flex item rather than the button, so
  `.variable-dataitem-main__name` sized nothing and the column collapsed to its content. Each row
  also carried a description paragraph, which neither reference has — helsedata's rows are
  explicitly one line (`height: 3.5rem; overflow: hidden`) and Runa's are table rows. And the
  sample's own generic column rule sat after the per-column widths, silently overriding them.
  Header and row cells now land on the same pixel across every column.
- **The first column header says Navn, not "Standard (stigende)"** - it was rendering the sort
  field's label instead of the column's name. A header names its column; the ordering is shown by
  an arrow beside it and announced through `aria-sort` on the active column. Runa calls this column
  Navn, so it does too. (Fhi.Metadata-zs56s)
