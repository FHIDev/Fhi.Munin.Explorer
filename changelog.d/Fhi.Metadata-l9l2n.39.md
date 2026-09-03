category: Changed
- **BREAKING for hosts on `0.1.0-alpha.7`: `VariableExplorer` is the whole variabelutforsker.**
  Search, the reader's own variable lists behind Runa's two tabs, and the view in the address bar,
  from one mount. The search half on its own is now `VariableSearch`, and the separate
  `VariableExplorerWithUrlState` is gone — `VariableExplorer` does what it did.
- **The tabs sit below the search box and the filters, not around them**, which is where Runa puts
  them: the heading, the search field and the facets stay on screen whichever tab is open, and only
  the results and their pager belong to the first. A signed-out reader gets no tablist at all.
- **Removed: the "Koble konto" account-link control.** It was Munin's own and no host wants it. The
  client keeps `RedeemIdentityLinkAsync`, so a host that wants the feature can still build one.
