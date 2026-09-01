category: Fixed

- **A half-typed date no longer empties the result list.** A native date input reports a complete
  value as soon as all three segments hold digits, so typing `01.01.2017` into *Til og med* arrived
  as `0002-01-01` on the way — which was applied, emptied the list and reached the host's URL. A
  date outside the bounds the field itself advertises is now ignored. Since the *to* field's lower
  bound is the *from* date, this is also the check that the end of the period comes after its
  start. (Fhi.Metadata-yxhv1)
- **The dataperiode facet stays on screen while it is filtering.** A date filter matching nothing is
  exactly when the API stops reporting a range, and the facet was dropped on that — taking away the
  only control that could undo the filter that emptied the list, and leaving the address bar as the
  way out. A facet carrying an active filter is now drawn whether or not the API reports a range
  for it.
