category: Notes for hosts
- **The package now depends on Markdig** (BSD-2-Clause), which every consuming host restores
  transitively. It is the parser behind the catalogue-text rendering above; nothing about
  registration or configuration changes. No new class names come with this change — the anchors
  and breaks render inside elements the views already emit. (Fhi.Metadata-5bcr7)
