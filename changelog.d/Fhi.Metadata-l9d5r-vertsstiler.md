category: Notes for hosts
- **`munin-explorer-meta__language` is a new name to style.** It is the language's name beside a
  catalogue value that is held in more than one, and it is drawn only in that case. An undrawn one
  costs look and not information: the element is a `<p>`, so a host with no rule still gets each
  language on its own line above its value. Both sample stylesheets carry a rule — a quieter,
  uppercase label — and a host that wants the same should scope it to its own component root.
- **This is not in `Fhi.Helsedata.Stiler` yet.** Nothing in this repository can see Stiler, so a
  green build here is not evidence the marker is styled on helsedata.no. Until a rule lands there,
  a host on Stiler sees the language names at body size rather than as labels; the languages are
  still separated and still correct.
