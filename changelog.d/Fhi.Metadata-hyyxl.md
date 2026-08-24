category: Notes for hosts
- **The pager wears our own class names now, like the rest of the component.**
  `variables-pagination` and `variables-pagination-content` became `munin-explorer-pagination` and
  `munin-explorer-pagination-content`. They were the last part the component *drew* with names taken
  from helsedata's page-specific `variables.css`, and two of the three names of the 95 it emits that
  `Fhi.Helsedata.Stiler` 0.1.13 has no rule for — Stiler carries no pagination rule of any kind. A
  host with Stiler alone drew the pager at browser defaults while the rest of the component came
  out right, which is the failure the `munin-explorer` rename exists to end. The third name is the
  pager's skip link, which went the same way under `Fhi.Metadata-ja2qu` — see that entry.
  (Fhi.Metadata-hyyxl)
- **The rules for them ship in Stiler, under `components/munin-explorer/` with the rest of the
  prefix — in 0.1.14, not in 0.1.13, which predates this rename.** Until you are on 0.1.14 the
  pager renders at browser defaults, exactly as it did before the rename.
  Two of its rules are worth supplying yourself in the meantime whatever else you do about the
  look: an outline on `.munin-explorer-pagination:focus`, which is the only sign a sighted keyboard
  user gets that the skip link moved focus, and an unavailable state drawn from
  `.munin-explorer-pagination-content [aria-disabled="true"]` rather than from `:disabled`, because
  the buttons at the ends of the list are never `disabled`. Both sample hosts' `host.css` shows the
  shape. (Fhi.Metadata-hyyxl)
- **The third name was the skip link, and this rename did not close that gap.**
  `skiplink-pagination`, on the link that jumps past the result list to the pager, stayed
  helsedata's here: what it needs is not a look but a single visually-hidden-until-focused rule,
  and `variables.css` — served on every page of helsedata.no, despite the name — has it, while no
  released Stiler had a rule that hid the link. `Fhi.Metadata-ja2qu` is where it is closed.
  (Fhi.Metadata-hyyxl)
- **Inside helsedata.no nothing changes.** Their `variables-pagination` rules are still in
  `variables.css` on every page; the component simply no longer asks for them. (Fhi.Metadata-hyyxl)
