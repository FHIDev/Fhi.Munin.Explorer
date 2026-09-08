category: Added
- The kilde detail view now loads an expandable hierarchy of delkilder, datasamlinger and
  variabelgrupper, initially collapsed. Descriptions and validity periods remain available
  in a separate disclosure. The variable explorer's source view keeps its existing presentation.
- Hosts can render `KildeHierarchyView` with a `KildeId` and optional `Language`, or supply
  `KildeView.Hierarchy` to replace its always-open structure while retaining the metadata disclosure.
