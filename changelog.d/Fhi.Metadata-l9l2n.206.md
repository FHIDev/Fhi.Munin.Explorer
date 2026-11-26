category: Fixed
- **With `datasamlingId` declined, a `?variabelId=` link names the primary row, as a `?variabel=` link does.** A host
  that declines `datasamlingId` and navigates to `?variabelId=<id>` while the reader has another of that variable's
  datasamlinger open now opens the primary row, even where the list puts another of its rows first. This holds once
  the explorer knows the variable's code. A host that also declines `variabel`, a Munin without the code route, or a
  navigation before the code has arrived keeps the open row as before: there the link is the explorer's own address
  for that row. An address the explorer wrote for another row before the code arrived names the primary row once
  the code is known. With the primary row already open, the link is the same view and starts no new search.
  (Fhi.Metadata-l9l2n.206)
