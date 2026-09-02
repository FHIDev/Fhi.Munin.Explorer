category: Notes for hosts

- The variable explorer's facet values no longer wear `hd-button-square`, `button-square--secondary`
  or `button-square--ghost`, and no longer carry `aria-pressed`. They emit no class at all: a bare
  `<input type="checkbox">` inside its own `<label>`, inside the `<li>` that was already there. No
  `munin-explorer` name is added or removed, so a host that styles the panel by that handle needs no
  change — but a rule scoped to a *button* inside `.munin-explorer-filters` now reaches only the
  toolbar. Both sample hosts style `.munin-explorer-filters li > label` instead, which is the same
  rule Kelda's values already used. (Fhi.Metadata-j0a2h)
