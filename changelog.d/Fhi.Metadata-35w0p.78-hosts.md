category: Notes for hosts
- **Runa's row markup changed, and the row ring needs Fhi.Helsedata.Stiler 0.1.114 or later** -
  `button.munin-explorer-dataitem-main__name` now carries `aria-expanded`, `aria-controls` (only
  while the panel is open) and an `aria-label`. Its first child is the
  `span.munin-explorer-dataitem-main__expand-icon` chevron. Stiler 0.1.114 draws the row's focus
  ring on `button.munin-explorer-dataitem-main__name:focus-visible::after` and swaps the chevron
  picture on the button's `aria-expanded`. On an older Stiler the chevron shows no picture and a
  focused row has no visible ring. The component no longer writes
  `munin-explorer-dataitem__expand-cell`, `munin-explorer-dataitem__expand-toggle`,
  `munin-explorer-dataitem-main__save` or `munin-explorer-dataitem-header__save`, so rules a host
  wrote for those match nothing now. `munin-explorer-data-list__save-status` still holds the save
  alert, which is now inside the open panel (`.munin-explorer-detail`) and not a cell of the row.
  A host that sent readers to the whole variable by clicking the name must point them at the
  panel's "Vis hele variabelen" button instead. (Fhi.Metadata-35w0p.78)
