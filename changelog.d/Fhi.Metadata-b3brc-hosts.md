category: Notes for hosts

- **`munin-explorer-kilder-scroll` is new and needs `overflow-x: auto`.** It wraps the kilder table
  alone. Undrawn, the table's overflow goes to the document and the host's whole page scrolls
  sideways; the markup already carries `role`, `tabindex` and the name. Both sample stylesheets
  carry it, and it ships in `Fhi.Helsedata.Stiler` from 0.1.41 onward. (Fhi.Metadata-b3brc)

- **The same box is focusable, so it also needs a visible focus indicator.** `tabindex="0"` is
  unconditional, so a keyboard lands on the box whether or not it has anything to scroll, and a
  focus stop the reader cannot see is WCAG 2.4.7 — one failure traded for another. A host whose CSS
  reset strips outlines must put one back. Both sample stylesheets and `Fhi.Helsedata.Stiler` use
  `outline: 2px solid <focus colour>; outline-offset: 4px` on `:focus-visible`. (Fhi.Metadata-b3brc)
