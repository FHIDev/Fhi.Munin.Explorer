category: Fixed

- **Ticking one value in the variabelutforsker's filter panel draws one active-filter chip.** A
  delkilde, variabelgruppe or saved filter the facet payload listed twice - once under a parent
  that is in the payload, once as an orphan - was drawn as two checkboxes, so one press ticked both
  copies, the facet's own count said two and the row over the results showed two chips for one
  filter. A repeated id is now placed once however many times the payload names it, keeping the
  copy that hangs off a parent that is present rather than the copy listed first, so where a value
  sits and the words it carries are the payload's meaning rather than its order. The kilde facet -
  the one facet reading its ticks off the payload rather than off the drawn tree - collapses its
  delkilder on those same terms, so a chip and the checkbox it stands for can never keep copies
  naming one filter two ways. A repeated kilde is collapsed the same way, by id alone, since a
  kilde carries no parent; that one was also a crash, because two checkboxes drawn under one key
  took the whole panel down at the first render after a press. The kildeutforsker's chip row is
  unchanged and was never affected: it walks four fixed facet definitions over a set of ticked
  values apiece, so it cannot name one value twice. (Fhi.Metadata-l9l2n.82)
