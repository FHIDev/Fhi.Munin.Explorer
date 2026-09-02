category: Notes for hosts

- **This needs the Stiler release carrying `.munin-explorer-filters li > label`** (Stiler PR 39039).
  Before it, Stiler dressed a facet checkbox only under `.munin-explorer-filters__facets`, which is
  Kelda's container and one the variable explorer has never emitted — so on an older Stiler the
  variable explorer's values render as an unspaced inline checkbox with no wrapping control. Measured
  in a host loading `main.css` and nothing else.
- The variable explorer's facet values no longer wear `hd-button-square`, `button-square--secondary`
  or `button-square--ghost`, and no longer carry `aria-pressed`. They emit no class at all: a bare
  `<input type="checkbox">` inside its own `<label>`, inside the `<li>` that was already there. No
  `munin-explorer` name is added or removed, so a host that styles the panel by that handle needs no
  change — but a rule scoped to a *button* inside `.munin-explorer-filters` now reaches only the
  toolbar. Both sample hosts style `.munin-explorer-filters li > label` instead, which is the
  selector Stiler now uses too. (Fhi.Metadata-j0a2h)
