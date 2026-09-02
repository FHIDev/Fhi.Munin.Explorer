category: Notes for hosts
- **One new class name, `munin-explorer-kilde__delkilde-description`**, the paragraph holding a
  delkilde's own words inside the delkilde tree. The rule has to land in `Fhi.Helsedata.Stiler`
  under `components/munin-explorer/`, which this repository's CI cannot see, so a green build here
  does not mean the paragraph is styled on helsedata.no. Undrawn it costs look rather than
  information — it is a `<p>` and a browser draws one readably — but it is prose sitting between a
  heading and a table, so without a measure and a margin it runs the full width of a wide window
  and crowds the table under it. Both sample stylesheets carry the same rule the kilde's own
  description wears one size down: `margin: 8px 0 0`, `max-width: 65ch`, `color: var(--grey60)`
  and `font-size: 0.9375rem`. That last one is measured against Stiler's own `headline-xxs`, which
  is `1rem` — the size the delkilde's name above it wears — so the prose sits just under its
  heading rather than over it. The samples' `headline-xxs` stand-in is smaller than Stiler's, so in
  a sample host alone the two read the other way round until that stand-in is corrected.
  (Fhi.Metadata-3osk6)
