category: Fixed

- **The hierarchy trail no longer offers a second way to undo a filter.** Ticking a kilde, a
  delkilde or a variabelgruppe drew it twice over the results — once as a removable chip, once as a
  trail step with an × of its own — so one ticked value had two remove controls a screen reader
  announced one after the other. The chip row is now the only place a value is removed, and it
  still holds one chip per chosen value across every facet. A trail step still narrows to its own
  level and clears every level below it, which is the thing a chip cannot do, and the trail is now
  a navigation landmark named "Valgt hierarki" rather than a filter list. (Fhi.Metadata-oj286)
- **A hierarchy value the facets do not name now has a chip of its own.** The facets are
  cross-filtered and can come back without a value the reader chose — and there are none at all
  before the first answer, or after one that failed, while a host may have mounted with a filter
  already set. Such a value was drawn in the trail and nowhere else, so with the trail's × gone the
  only control left would have been "Fjern alle filtre", which drops the datatype, the kodeverk and
  the dates with it. It is listed under its level's own word, exactly as the trail's step reads it.
  (Fhi.Metadata-oj286)
