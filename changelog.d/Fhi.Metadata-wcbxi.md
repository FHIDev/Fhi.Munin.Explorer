category: Added
- **The filter panel has a toolbar: Utvid alle, Skjul alle and Nivålinjer.** The first two fold and
  unfold every facet at once, which a native `<details>` cannot do for itself. Nivålinjer puts
  `data-level-lines="true"` on the panel for a host to draw a guide line per level from. **The
  package draws no lines**, and a host with no rule for that attribute sees nothing change when the
  toggle is on; both sample stylesheets show the rule. It is an attribute and not a class name so
  that no new name enters the set a host has to style. (Fhi.Metadata-wcbxi)
- **A host drawing those lines must clear 3:1.** They are a non-text control under WCAG 1.4.11, and
  the panel sits on the page ground rather than on a card: the samples' ordinary border grey gives
  1.16:1 there and is invisible above about 1000px, so the rule uses a darker token at 6.76:1. The
  same bar applies to a host's dark theme, which neither sample defines.
- **`LevelLines` / `LevelLinesChanged` is a new two-way parameter**, off by default. The package
  remembers nothing itself — `localStorage` from a Blazor circuit is a JS interop call this package
  makes none of — so a host that wants the choice to survive a visit stores what the press raises
  and passes it back.
