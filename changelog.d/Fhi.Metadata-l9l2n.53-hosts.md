category: Notes for hosts

- **One new class name for the kildeutforsker's facet summaries, `munin-explorer-filters__chosen`.**
  It is the "2 valgt" beside a facet heading, and its rule is in `Fhi.Helsedata.Stiler` from the
  release that follows PR 39209. A handle: the words are markup, so a host that defines nothing
  still gets the count on screen and in the summary's accessible name, and what the rule buys is
  the dimming, the tabular figures and `flex: none` so the number is not broken across lines. Not
  to be confused with `munin-explorer-filters__count`, the hit count beside a single value, whose
  rule pushes it to the column edge with `margin-left: auto` — on the summary line that edge
  belongs to the disclosure marker, which is why the two names exist. Both sample stylesheets show
  the rule. (Fhi.Metadata-l9l2n.53)
