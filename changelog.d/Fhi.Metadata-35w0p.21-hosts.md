category: Notes for hosts
- **Three new class names, and none of them is in Fhi.Helsedata.Stiler yet.**
  `munin-explorer-complete-record` on the disclosure, `munin-explorer-complete-record__lead` on the
  paragraph above it and `munin-explorer-complete-record__fields` on the list inside it. Both sample
  stylesheets carry a rule for each; a host without one gets a native `<details>`, a paragraph and a
  single-column definition list, which is legible but not the dense two-column grid the mockup
  draws. Fhi.Helsedata.Stiler now carries a rule for all
  three: they landed on 2026-09-16 as 9ae64786 (_detail.scss, _hierarchy.scss) and Stiler publishes
  on merge to main, so a host taking Stiler from the feed gets them with its next version. This
  package's samples still pin the version before that, which is a separate change.
  (Fhi.Metadata-35w0p.21)
