category: Notes for hosts
- **`munin-explorer-kodeverk__name` can now be a `<div>` holding a `<ul>`, and needs no new rule.**
  Where a kildekodeverk has no name and its codes are shown instead, the element wearing the class
  is a `<div>` with a plain `<ul lang="no">` inside, one `<li>` per code; everywhere else it is the
  `<p>` it was. The existing class rule (`margin: 0; font-weight: 600` in Stiler) applies to both.
  A host stylesheet that selects it as `p.munin-explorer-kodeverk__name` misses the code list, and
  the `<ul>` takes the host's own list defaults. (Fhi.Metadata-0ajsy)
