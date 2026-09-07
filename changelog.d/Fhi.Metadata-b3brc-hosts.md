category: Notes for hosts

- **`munin-explorer-kilder-scroll` is new and needs `overflow-x: auto`.** It wraps the kilder table
  alone. Undrawn, the table's overflow goes to the document and the host's whole page scrolls
  sideways; the markup already carries `role`, `tabindex` and the name, so the rule is the only
  thing a host owes it. Both sample stylesheets carry it, and it ships in `Fhi.Helsedata.Stiler`
  from the release that follows PR 39148, which also moves the explorer's grid breakpoint from
  1024px to 1281px. (Fhi.Metadata-b3brc)
