category: Fixed
- **A kildekodeverk with no name lists its codes one per line instead of as one run-on line.**
  Where a kildekodeverk link has codes but no name, `VariableSearch` shows up to eight of them in
  the name's place; they were drawn as a single line joined by " · ", which read as one bold
  sentence (Munin sak #6132). They are now a plain `<ul>` with one `<li>` per code, still marked
  `lang="no"`. A `<ul>` cannot sit inside a `<p>`, so in this case only the element carrying
  `munin-explorer-kodeverk__name` is a `<div>` rather than a `<p>`; the class is unchanged, no
  class name is added, and a named kodeverk renders exactly as before. "Vis alle (N)" still opens
  the full list when there are more than eight codes. (Fhi.Metadata-0ajsy)
