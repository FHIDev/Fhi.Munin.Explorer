category: Notes for hosts
- **`KildeDatasamling` has a new property, `Code`** (`string?`, JSON `code`), null from a Munin that does not send it. A host that links to a datasamling from its own pages can use `?kilde=<kilde code>&datasamling=<datasamling code>`, which is the same in test and prod, instead of the ids, which differ per environment. (Fhi.Metadata-l9l2n.171)
