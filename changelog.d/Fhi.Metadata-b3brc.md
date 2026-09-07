category: Fixed

- **The kilder table scrolls in a box of its own instead of scrolling the host's page.** Its eight
  columns want 779px at their narrowest, which is more than helsedata's content box below about
  827px, and the overflow used to land on the document — a WCAG 1.4.10 failure on the whole site
  rather than a table that looks wrong. The table now sits in a `region` with `overflow-x`,
  `tabindex="0"` and the table's own name, so a keyboard can reach it; the column picker stays
  outside the box and on screen. (Fhi.Metadata-b3brc)
