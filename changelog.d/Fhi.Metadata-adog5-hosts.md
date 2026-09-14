category: Notes for hosts
- **Two class names to style, and one disclosure that is no longer a `<summary>`.**
  `munin-explorer-filters__branch` is the `<li>` of a facet value that has values under it, and
  `munin-explorer-filters__disclosure` is the `<button aria-expanded>` inside it that opens the
  branch. Both are handles: the button carries an arrow as text and an `aria-label` of its own, so
  with no rule at all it is still visible, operable and announced, and the row simply draws as the
  blocks it is made of instead of as a row. What a rule buys is the row itself and a 24×24 target,
  which is WCAG 2.5.8 rather than decoration — and the focus ring, if your reset strips outlines:
  the disclosure is a tab stop on every branch of the tree. Both sample stylesheets carry the
  rules to copy. The change to watch for is the kildetype groups inside the Kilde facet: they were
  `<details>`/`<summary>` and inherited whatever you give `.munin-explorer-filters summary`, and
  they are now ordinary branch rows, so a rule written for that summary no longer reaches them.
  The `Fhi.Helsedata.Stiler` rules for the two new names, and whatever that summary rule was
  carrying for the group rows, are `Fhi.Metadata-cs3pt` and are in no published version yet — so
  every host draws the disclosures at browser defaults until the release that first carries them.
  Both names are drawn by every facet that nests values and not by the Kilde facet alone: a
  variabelgruppe nested under another is a branch row too, and it starts shut where it used to be
  drawn unasked, so a rule scoped to the Kilde facet reaches neither it nor its disclosure.
  (Fhi.Metadata-adog5)
