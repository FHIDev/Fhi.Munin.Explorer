category: Notes for hosts
- **The pager wears our own class names now, like the rest of the component.**
  `variables-pagination` and `variables-pagination-content` became `munin-explorer-pagination` and
  `munin-explorer-pagination-content`. They were the last two names the component took from
  helsedata's page-specific `variables.css`, and the only two of the 95 it emits that
  `Fhi.Helsedata.Stiler` 0.1.13 has no rule for — Stiler carries no pagination rule of any kind. A
  host with Stiler alone therefore drew every part of the component correctly except the pager,
  which is the promise the `munin-explorer` rename was meant to keep. (Fhi.Metadata-hyyxl)
- **The rules for them ship in Stiler, under `components/munin-explorer/` with the rest of the
  prefix — and not in 0.1.13, which predates this rename.** Until you are on the Stiler release
  that carries them, the pager, and only the pager, renders at browser defaults. Two of its rules
  are worth supplying yourself in the meantime whatever else you do about the look: an outline on
  `.munin-explorer-pagination:focus`, which is the only sign a sighted keyboard user gets that the
  skip link moved focus, and an unavailable state drawn from
  `.munin-explorer-pagination-content [aria-disabled="true"]` rather than from `:disabled`, because
  the buttons at the ends of the list are never `disabled`. Both sample hosts' `host.css` shows the
  shape. (Fhi.Metadata-hyyxl)
- **Inside helsedata.no nothing changes.** Their `variables-pagination` rules are still in
  `variables.css` on every page; the component simply no longer asks for them. (Fhi.Metadata-hyyxl)
- **`skiplink-pagination` is unchanged and is still helsedata's.** It is the one borrowed name the
  component emits that Stiler does not define, and the rule keeping the skip link out of sight
  until it is focused is still a host's to supply. (Fhi.Metadata-hyyxl)
