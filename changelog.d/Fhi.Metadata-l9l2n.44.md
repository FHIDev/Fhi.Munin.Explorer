category: Changed
- **A released version now has a curated changelog entry, and the feed shows it.**
  `PackageReleaseNotes` carries that version's own `CHANGELOG.md` section rather than a link to a
  GitHub release page of auto-generated commit titles, and the release for the tag carries the same
  text. `CHANGELOG.md` also gained the eight `0.1.0-alpha.*` sections it never had: a host bumping
  `0.1.0-alpha.7` to `0.1.0-alpha.8` can now read that `VariableExplorerWithUrlState` was removed
  and the bare component renamed to `VariableSearch` where it would look for it. (Fhi.Metadata-l9l2n.44)
