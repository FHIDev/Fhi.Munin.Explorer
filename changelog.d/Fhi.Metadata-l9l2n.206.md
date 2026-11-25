category: Fixed
- **With `datasamlingId` declined, a `?variabelId=` link names the primary row, as a `?variabel=` link does.** A host
  that declines `datasamlingId` and navigates to `?variabelId=<id>` while the reader has another of that variable's
  datasamlinger open now opens the primary row, which is what a fresh load of the same address opens. This holds once
  the explorer knows the variable's code. A host that also declines `variabel`, a Munin without the code route, or a
  navigation before the code has arrived keeps the open row as before: there the link is the explorer's own address
  for that row. With the primary row already open, the link is the same view and starts no new search.
  (Fhi.Metadata-l9l2n.206)
