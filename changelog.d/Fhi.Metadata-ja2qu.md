category: Fixed
- **The pager's skip link is hidden until it is focused on a host with Stiler alone.** The anchor
  that jumps past the result list to the pager wore helsedata's `skiplink-pagination`, and Stiler
  had no rule that reached it — so on a host outside helsedata's estate a permanently visible
  "Hopp til paginering" sat over every multi-page result list. A skip link everyone can see is not
  a skip link. It is `munin-explorer-skiplink-pagination` now, and the rule that hides it until
  `:focus` ships unscoped in `Fhi.Helsedata.Stiler` 0.1.14. Inside helsedata nothing changes: their
  `variables.css` rule for the old name is still there, now unused. (Fhi.Metadata-ja2qu)
