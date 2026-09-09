category: Notes for hosts

- **One new class name for the kildeutforsker's facet summaries, `munin-explorer-filters__chosen`,
  and one rule a host owes the summary line itself.** The name is the "2 valgt" beside a facet
  heading, and it is a handle: the words are markup, so a host that defines nothing still gets the
  count on screen and in the summary's accessible name, and what a rule buys is the dimming, the
  tabular figures and `flex: none` so the number is not broken across lines. The rule that has to
  be there is the other one — a `<summary>` laid out as a row, because the heading inside it is a
  block box and takes the whole first line otherwise, putting the count under the heading and the
  disclosure marker under that. Both sample stylesheets show the pair, scoped to the kildeutforsker's
  facets; the rule shipping in `Fhi.Helsedata.Stiler` is unscoped and shared with the
  variabelutforsker's panel, and it redraws the marker on the trailing edge, since a summary laid
  out as a row is no longer a list-item and the browser stops drawing one (Fhi.Metadata-l9l2n.58).
  Not to be confused with `munin-explorer-filters__count`, the hit count beside a single value,
  whose rule pushes it to the column edge with `margin-left: auto` — on the summary row that edge
  is the marker's, which is why the two names exist. (Fhi.Metadata-l9l2n.53)
