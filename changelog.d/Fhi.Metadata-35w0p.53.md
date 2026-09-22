category: Added
- **Every kildeutforsker facet now says how many values it has.** A facet's summary reads
  "Databehandler 24 verdier" — heading, then the facet's size, then the ticked count when there is
  one ("Kildetype 2 verdier 2 valgt"), so a folded facet tells the reader whether opening it shows
  four values or forty. The size is the whole facet's, and ticking, the facet's own search and
  "Vis N til" do not change it. It is a separate `<span>` from the ticked count, each with its own
  unit word ("1 verdi"/"N verdier", "1 value"/"N values") and a space between them, so the two read
  as two facts to a screen reader and to the eye. It wears the existing
  `munin-explorer-filters__groupcount`, a direct child of the `<summary>` beside `__chosen`, whose
  `Fhi.Helsedata.Stiler` rule it shares — no new class name and no Stiler release needed. The
  text is a new `Texts.FacetSize` entry. The variabelutforsker's summaries are unchanged.
  (Fhi.Metadata-35w0p.53)
