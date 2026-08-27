category: Fixed

- **The variable result list is a table to a screen reader** - it drew seven columns under a header
  row and told assistive technology nothing about any of it, so a reader got a flat run of text with
  no way to hear which column a value was in or to move by column. The rows, the header cells and
  the columns now carry `table`, `rowgroup`, `row`, `columnheader`, `rowheader` and `cell` roles,
  and the sorted column's `aria-sort` finally sits on a role that may carry it — it was on a
  roleless `<div>`, which is invalid ARIA, so the sort state was announced to nobody. Visual layout
  is unchanged: the roles go on the elements that were already there, and the two boxes that only
  lay the columns out step out of the accessibility tree instead. WCAG 2.1 AA, 1.3.1 and 4.1.2.
  (Fhi.Metadata-3b1l4)
- **The saved-list view got the same treatment** - it shares the result list's markup and had the
  same missing structure, which no automated check reports because absent structure is not a rule
  violation. (Fhi.Metadata-3b1l4)
- **"Hopp til paginering" moved above the result table** - it used to sit between the header row and
  the rows, which is inside the table now, and a table may own nothing but rows. It is still beside
  the list it skips and still invisible until focused. (Fhi.Metadata-3b1l4)
