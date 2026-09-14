category: Notes for hosts
- **Two class names to style, and one disclosure that is no longer a `<summary>`.**
  `munin-explorer-filters__branch` is the `<li>` of a facet value that has values under it, and
  `munin-explorer-filters__disclosure` is the `<button aria-expanded>` inside it that opens the
  branch. Both are handles: the button carries an arrow as text and a name from `aria-labelledby`, so
  with no rule at all it is still visible, operable and announced, and the row simply draws as the
  blocks it is made of instead of as a row. What a rule buys is the row itself and a 24×24 target,
  which is WCAG 2.5.8 rather than decoration — and a focus state that survives an outline reset:
  the disclosure is a tab stop on every branch of the tree. Both sample stylesheets carry the
  rules to copy. The change to watch for is the kildetype groups inside the Kilde facet: they were
  `<details>`/`<summary>` and inherited whatever you give `.munin-explorer-filters summary`, and
  they are now ordinary branch rows, so a rule written for that summary no longer reaches them.
  Use `Fhi.Helsedata.Stiler` **0.1.75 or later**, which supplies both rules (`Fhi.Metadata-cs3pt`).
  Earlier versions leave the disclosures at browser defaults unless the host adds those rules.
  The disclosure's accessible name keeps the UI action and catalogue name separately language-marked.
  Both names are drawn by every facet that nests values and not by the Kilde facet alone: a
  variabelgruppe nested under another is a branch row too, and it starts shut where it used to be
  drawn unasked, so a rule scoped to the Kilde facet reaches neither it nor its disclosure.
  (Fhi.Metadata-adog5)
