category: Notes for hosts

- **The sample stylesheets now push the facet count to the label's right edge**, with
  `margin-left: auto` on `munin-explorer-filters__count`. Without it a count sits wherever its own
  label's text ended — measured in the samples at 81 distinct right edges across the variable
  explorer's 107 facet values, and 7 across the kildeutforsker's 8 — so the `font-variant-numeric:
  tabular-nums` that rule already carried had no column to line its digits up in.
  `Fhi.Helsedata.Stiler` carries the same declaration (ADO PR 39101), so a host on that stylesheet
  already had the column and the samples were the ones out of step; a host copying its rules from
  the samples should take it. It is a physical `margin-left` rather than `margin-inline-start`,
  matching the declaration it stands in for — a host serving `dir="rtl"` wants the logical form.
  (Fhi.Metadata-7bchj)
