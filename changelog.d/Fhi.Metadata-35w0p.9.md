category: Changed
- **The kilde, datasamling and variable views now draw one shared page chassis.** A new
  `DetailPage` component emits the root, the name block, the body grid, the contents column and the
  main column once, and the three views supply content into it rather than each writing the same
  three wrappers under a prefix of its own. The chassis wears `munin-explorer-page`,
  `munin-explorer-page__body`, `munin-explorer-page__main` and `munin-explorer-page__toc`, and
  every view keeps the prefixed names it already had on the same elements — `munin-explorer-kilde`
  and `munin-explorer-kilde__body` are now both on the body, and so on for `-datasamling` and
  `-whole`. Nothing was renamed and nothing was dropped: one of the old names,
  `munin-explorer-kilde__datasamlinger`, is styled by an expanded row in the kildeutforsker as well
  as by the detail page, and a host or a test that finds a detail view by any of the old names
  still finds it. The markup below the chassis is untouched, down to the count of `dt`/`dd` pairs.
  (Fhi.Metadata-35w0p.9)
