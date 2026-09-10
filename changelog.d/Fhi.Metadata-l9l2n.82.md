category: Fixed

- **Ticking one value in the variabelutforsker's filter panel draws one active-filter chip.** A
  variabelgruppe or saved filter the facet payload listed twice - once under a parent that is in
  the payload, once as an orphan - was drawn as two checkboxes, so one press ticked both copies,
  the facet's own count said two and the row over the results showed two chips for one filter. The
  tree builder now places an id once however many times the payload names it, and both explorers'
  chip rows draw one chip per chosen value. (Fhi.Metadata-l9l2n.82)
