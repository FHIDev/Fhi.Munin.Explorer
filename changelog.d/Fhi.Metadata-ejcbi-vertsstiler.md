category: Notes for hosts
- One new class name, `munin-explorer-pagination-pages`, the row of numbered page buttons between
  `Forrige` and `Neste`. **A host has to draw this one.** The buttons inside it wear helsedata.no's
  own `hd-button-reset`, which strips the button chrome, and the page in force is marked with their
  `current` — so without a rule the run is a line of bare digits and nothing says which page you
  are on. `Forrige` and `Neste` are `hd-button-square button-square--ghost`, also helsedata's own
  pair. All four were read off their live pager on 2026-09-03 rather than guessed at. Both sample
  hosts draw the run as a wrapping flex row and bold the `.current` digit.
- **The three names that carry the pager's layout still owe a rule.** `munin-explorer-pagination`,
  `munin-explorer-pagination-content` and `munin-explorer-pagination-size` were measured on the
  helsedata mount on 2026-08-31 computing to `display: block` with `gap: normal`, filling 2025px:
  the whole pager stacked as blocks instead of laid out as a row. Only
  `munin-explorer-skiplink-pagination` had a rule, and re-checking the `Fhi.Helsedata.Stiler`
  working copy on 2026-09-03 found `origin/main` still carrying exactly that one and no other.
  (This repository's README used to say the pager's rules shipped in 0.1.14. They did not, and it
  no longer says so.) `-content` is the one that matters most — it is
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
