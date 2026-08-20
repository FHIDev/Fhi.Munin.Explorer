category: Changed

- **Split the explorer component into files by responsibility** - the facet sidebar, selection and
  detail loading, querying, the detail panel, the drill-in view and the translations each moved to
  their own file, leaving the component itself at a third of its former size. Pure move; the test
  suite is the contract and passed unchanged.
- **Lifted the translations out of the component** so the kildeutforsker shipping from this same
  package can share them rather than keeping a second copy that would drift. (Fhi.Metadata-7hu8p)
- **`dotnet format` now gives the same answer on Windows as on CI**, so a local check is worth
  running. `.gitattributes` forces LF in the working tree to match `.editorconfig`.
