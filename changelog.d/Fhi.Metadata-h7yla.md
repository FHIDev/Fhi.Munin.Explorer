category: Notes for hosts
- The XML doc comments still told hosts that `variables.css` is a page-specific stylesheet only
  helsedata's variable page carries, and that a host mounting the component elsewhere has to supply
  three pager names. Both halves were wrong. `variables.css` is served on every page of
  helsedata.no — `/no/`, `/no/variabler/` and `/no/datakilder/` load an identical seven bundles —
  so a host inside their estate has the result vocabulary wherever the component is mounted, not
  only on the variable page; and a host outside has to supply the whole of that vocabulary, the
  rows and the opened panel and the column picker as well as the pager. (Fhi.Metadata-h7yla)
