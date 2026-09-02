category: Notes for hosts
- One new class name, `munin-explorer-pagination-pages`, the row of numbered page buttons between
  `Forrige` and `Neste`. The buttons inside it are Stiler's own `hd-button-square` with
  `button-square--secondary` for the page in force and `button-square--ghost` for the rest, and
  they carry Stiler's `margin-right`, so a host that defines nothing for the new name still gets
  separated buttons in the flow — what the rule buys is the wrapping. Both sample hosts draw it as
  a wrapping flex row.
- **The three names that carry the pager's layout still owe a rule.** `munin-explorer-pagination`,
  `munin-explorer-pagination-content` and `munin-explorer-pagination-size` were measured on the
  helsedata mount on 2026-08-31 computing to `display: block` with `gap: normal`, filling 2025px:
  the whole pager stacked as blocks instead of laid out as a row. Only
  `munin-explorer-skiplink-pagination` had a rule. `-content` is the one that matters most — it is
  where `display: flex` with a 16px `gap` belongs — and `-pages` and `-size` want the same
  treatment inside it. Until they land, a host renders this pager as a column of controls whatever
  else it has. Neither guard in this repository can see that: both ask whether a name has a rule in
  the capture of helsedata's live page or in the sample stylesheet, and neither reads Stiler.
- The page-size `<select>` deliberately wears **no class**. An element degrades to its own browser
  default where an unknown class name degrades to nothing, and no select name could be verified
  against Stiler from this repository. A host styling it should reach it as
  `.munin-explorer-pagination-size select` — Stiler's `components/_select.scss` is the look it is
  meant to have. Both sample hosts style it that way.
- `munin-explorer-pagination-size` no longer holds three buttons, so a host with a rule written
  against `.munin-explorer-pagination-size button` is styling something that is no longer there.
  The label inside it is a `<label>` where it was a `<span>`; both still wear `caption`.
