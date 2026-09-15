category: Notes for hosts
- **`munin-explorer-page__facts` is a new class name, and `Fhi.Helsedata.Stiler` 0.1.75 already
  carries a rule for it.** It is the hero row the three detail views now open with, and a host on
  0.1.75 or later needs to do nothing: the rule is in the same
  `components/munin-explorer/_page.scss` the rest of the chassis landed in, laying the list out as
  six equal tracks at desktop, three below 1080px and two below 600px, with the label small and
  uppercase, the value tabular and the note muted under it. A handle otherwise — undefined, the
  `<dl>` is a definition list at browser defaults, with every label and value still on the page and
  in the right order, so what is lost is the row rather than a word. Both sample stylesheets stand
  in at exactly what the pinned 0.1.75 declares. (Fhi.Metadata-35w0p.26)
- **The markup inside it is a `<div>` per fact, `<dt>` then `<dd>`, with an optional `<small>`.**
  If you write your own rule, key the cells off `.munin-explorer-page__facts > div` rather than off
  the `<dt>`/`<dd>` pair directly: a `<dl>` laid out as a grid puts each element in a track of its
  own, and the wrapper is what keeps a label with its value. The `<dt>` deliberately wears no
  `headline` class, unlike the fact lists further down the page, so nothing of Stiler's type scale
  competes with the rule for it. (Fhi.Metadata-35w0p.26)
