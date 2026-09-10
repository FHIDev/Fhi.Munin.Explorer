category: Fixed

- **Ticking one value in the variabelutforsker's filter panel draws one active-filter chip.** A
  delkilde, variabelgruppe or saved filter the facet payload listed twice - once under a parent
  that is in the payload, once as an orphan - was drawn as two checkboxes, so one press ticked both
  copies, the facet's own count said two and the row over the results showed two chips for one
  filter. The tree builder now places an id once however many times the payload names it, keeping
  the copy that hangs off a parent that is present, and the kilde facet's chips - the one facet
  reading its ticks off the payload rather than off the drawn tree - are de-duplicated on the same
  terms. The kildeutforsker's chip row is unchanged and was never affected: it walks four fixed
  facet definitions over a set of ticked values apiece, so it cannot name one value twice.
  (Fhi.Metadata-l9l2n.82)
