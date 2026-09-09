category: Added

- **A long facet in the kildeutforsker gets a search box over its own values.** Databehandler has
  39 values on the live catalogue, so finding one meant reading all of them. A facet with **more
  than ten** values now draws a small search field inside its disclosure; the threshold is one
  number applied to every facet, never a decision taken per facet, so kildetype's five values stay
  a plain list. Typing narrows that facet's values and nothing else — not the result table, not the
  counts, not the other facets, and not the ticks: a value that is ticked and then typed out of
  sight is still ticked and still narrowing the list, and clearing the box brings it back with its
  tick on. It is counted over the values the facet has rather than the ones its own search leaves,
  so the box does not disappear as it starts working. The values are not merged or normalised:
  four spellings of Folkehelseinstituttet are still four choices with four counts, because deciding
  that two strings name one organisation is a claim about the catalogue and not about the view.
  The box carries no class name of its own — it is a native text input inside the filter panel, on
  the same terms as the dataperiode facet's date fields, so there is nothing new for a host to
  style beyond the form fields it already styles. (Fhi.Metadata-6we8a)
