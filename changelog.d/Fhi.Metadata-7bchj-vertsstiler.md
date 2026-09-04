category: Notes for hosts

- **The sample stylesheets now push the facet count to the label's right edge**, with
  `margin-left: auto` on `munin-explorer-filters__count`. Without it a count sits wherever its own
  label's text ended — measured at 81 distinct horizontal positions across the variable explorer's
  107 facet values, and 7 across the kildeutforsker's 8 — so the `font-variant-numeric:
  tabular-nums` that rule already carried had no column to line its digits up in. `Fhi.Helsedata.Stiler`
  has shipped this since 0.1.37, so a host on that stylesheet already had it and the samples were
  the ones out of step; a host copying its rules from the samples should take it. Keep it together
  with the `flex: none` beside it — pushed right and still shrinkable, a long label squeezes the
  count against the edge and it breaks between the digits and the bracket again.
  (Fhi.Metadata-7bchj)
