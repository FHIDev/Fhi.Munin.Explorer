category: Notes for hosts
- **Three new class names, and none of them is in Fhi.Helsedata.Stiler yet.**
  `munin-explorer-complete-record` on the disclosure, `munin-explorer-complete-record__lead` on the
  paragraph above it and `munin-explorer-complete-record__fields` on the list inside it. Both sample
  stylesheets carry a rule for each; a host without one gets a native `<details>`, a paragraph and a
  single-column definition list, which is legible but not the dense two-column grid the mockup
  draws. The Stiler rules for the three landed on 2026-09-16 (9ae64786, _detail.scss and _hierarchy.scss); the release is merged but not version-bumped, so a host resolving Stiler from the feed rather than from source will not see them yet.
  (Fhi.Metadata-35w0p.21)
