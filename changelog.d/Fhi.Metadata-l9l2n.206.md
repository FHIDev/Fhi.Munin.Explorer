category: Fixed
- **With `datasamlingId` declined, a `?variabelId=` link names the primary row, as a `?variabel=` link does.** A host
  that declines `datasamlingId` and navigates to `?variabelId=<id>` while the reader has another of that variable's
  datasamlinger open now opens the primary row, which is what a fresh load of the same address opens. Before, the
  open row stayed. With the primary row already open, the link is the same view and starts no new search.
  (Fhi.Metadata-l9l2n.206)
