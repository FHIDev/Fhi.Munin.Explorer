category: Fixed
- **A long filter facet no longer draws every value.** Both explorers' filter panels, and the
  saved-list panel beside them, now draw the first ten values of a facet and put the rest behind a
  "Vis N til" button — which is a real button with `aria-expanded`, reachable by Tab and operable
  by Enter and Space. Before this, a facet like Databehandler drew all 24 of its values and the
  filter panel grew longer than the results it filters. A value the reader has ticked stays on
  screen whatever its place in the list, and typing in a facet's own search box shows every match
  with the button withdrawn, so the two controls cannot hide a value between them. Utvid alle
  reaches past the cap and Skjul alle puts it back, so the control that offers to open everything
  still means it. Ten is the
  threshold that already decided which facets get a search box, so the package has one notion of a
  long facet rather than two. (Fhi.Metadata-35w0p.31)
