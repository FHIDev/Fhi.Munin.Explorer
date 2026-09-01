category: Added
- **The filter panel has a toolbar: Utvid alle, Skjul alle and Nivålinjer.** The first two fold and
  unfold every facet at once, which a native `<details>` cannot do for itself. Nivålinjer puts
  `data-level-lines="true"` on the panel for a host to draw a guide line per level from — no new
  class name, because a tree is the one shape Stiler has no name for, and both sample stylesheets
  show the rule. (Fhi.Metadata-wcbxi)
- **`LevelLines` / `LevelLinesChanged` is a new two-way parameter**, off by default. The package
  remembers nothing itself — `localStorage` from a Blazor circuit is a JS interop call this package
  makes none of — so a host that wants the choice to survive a visit stores what the press raises
  and passes it back.
