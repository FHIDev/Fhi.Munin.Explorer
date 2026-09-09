category: Changed

- **A folded facet in the kildeutforsker says how many of its values are ticked in words, beside
  the heading rather than inside it.** The number used to be appended to the facet's own heading
  text, which put it inside the thing a screen-reader user navigates the panel by and left it to
  say only "(2)" — a figure with no noun. The summary now reads "Kildetype 2 valgt", with the count
  as its own element next to the heading, so that whole sentence is what the disclosure is
  announced as. It is drawn only while at least one of the facet's values is ticked: an untouched
  facet says nothing rather than "0 valgt", and clearing the last tick takes the count away again.
  English hosts get "2 selected". The heading is still a heading at the same level, and the
  disclosure is still the native `open` state with no `aria-expanded` beside it.
  (Fhi.Metadata-l9l2n.53)
