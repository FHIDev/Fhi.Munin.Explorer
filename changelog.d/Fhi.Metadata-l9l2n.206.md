category: Fixed
- **With `datasamlingId` declined, a `?variabelId=` link names the primary row, as a `?variabel=` link does.** A host
  that declines `datasamlingId` and navigates to `?variabelId=<id>` once the explorer knows the variable's code now
  opens the primary row, even where the list puts another of its rows first. As with a `?variabel=` link, the list
  narrows to that one row with «Vis alle treff», and where the primary row is not on the arrived page, no row opens.
  A page load on such a link, a host that also declines `variabel`, a Munin without the code route, or a navigation
  before the code has arrived is unchanged: a load opens the variable's first row in the list, and a navigation
  keeps the open row, since there the link is the explorer's own address for that row. An address the explorer wrote
  for another row before the code arrived names the primary row once the code is known. With the primary row
  already open, the link is the same view and starts no new search.
  (Fhi.Metadata-l9l2n.206)
