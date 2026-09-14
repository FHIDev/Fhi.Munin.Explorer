category: Changed
- **The kilde, datasamling and variable views now draw one shared page chassis.** A new
  `DetailPage` component emits the root, the name block, the body grid, the contents column and the
  main column once, and the three views supply content into it rather than each writing the same
  three wrappers under a prefix of its own. The chassis wears `munin-explorer-page`,
  `munin-explorer-page__body`, `munin-explorer-page__main` and `munin-explorer-page__toc`. Each
  view keeps the prefixed names it already had on the root and the main column — `munin-explorer-kilde`
  and `munin-explorer-kilde__main` sit beside the chassis names there, and so do the `-datasamling`
  and `-whole` equivalents — because one of them, `munin-explorer-kilde__datasamlinger`, is styled
  by an expanded row in the kildeutforsker as well as by the detail page. The body is the one
  element that does not keep its old name: `munin-explorer-kilde__body`,
  `munin-explorer-datasamling__body` and `munin-explorer-whole__body` are no longer emitted, and
  the reason is in the notes for hosts. The markup below the chassis is untouched, down to the
  count of `dt`/`dd` pairs. (Fhi.Metadata-35w0p.9)
