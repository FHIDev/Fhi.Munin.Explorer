category: Added
- **The kilde, datasamling and variable views draw each block as a `<section>` with a stable id.**
  Every block heading below the page title — the catalogue metadata, the datasamlinger, the
  inclusion criteria, the version history, the statistics and each sidebar box — now sits inside
  `<section id="…" data-nav-section class="munin-explorer-page__section">`, which is what a
  contents nav or a scroll-spy needs to anchor on; neither ships yet. The ids are fixed English
  words (`metadata`, `criteria`, `source`, `statistics`, `datacollections`, `versions`,
  `dataperiod`, `datatype`, `variablegroups`) rather than a slug of the heading, so a link into a
  section is the same link for a Norwegian and an English reader and survives a label being
  reworded. Nothing moved on the page: the wrapper carries no spacing, and a block that drew
  nothing before still draws nothing — the section is emitted inside each emptiness check, never
  around it, so a source with no statistics gets no empty box. (Fhi.Metadata-35w0p.5)
