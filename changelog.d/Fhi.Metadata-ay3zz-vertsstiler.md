category: Notes for hosts

- **The column picker's names now appear on the kilde table too**, unchanged:
  `munin-explorer-header`, `munin-explorer-header__actions`,
  `munin-explorer-header__actions-button` and `munin-explorer__dropdown`, plus Stiler's own
  `dropdown-choicepicker`. No name is new, so a host that styles the variable explorer's picker
  has nothing to add — **unless its rules are scoped to that explorer's own container**, in which
  case the same control renders undressed above the kilde table. Worth reading the selector rather
  than the name: a rule reaching this picker has to match a `<details>` inside
  `munin-explorer-header`, wherever that header sits.
- **A toggle in that picker should not have its tick read out.** The sample stylesheets draw the
  on/off state as `☑`/`☐` in `::before`, and a browser folds generated content into the accessible
  name — so the control announced as "☑ Kildetype" and said in words what `aria-pressed` already
  says. The samples now write `content: "\2611" / ""`, whose empty alternative text keeps the glyph
  out of the name; a host drawing its own tick owes the same, or it owes no glyph at all. That
  syntax has a floor — Chrome 77, Safari 17.4, Firefox 133 — and below it the whole declaration is
  invalid, so the tick disappears rather than degrading. A host supporting older browsers should
  mark the glyph up as `aria-hidden` content instead. (Fhi.Metadata-ay3zz)
