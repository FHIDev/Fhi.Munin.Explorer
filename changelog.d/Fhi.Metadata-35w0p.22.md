category: Changed
- **`KildeView` draws its sections in the order the API places them, property sections and built-in
  ones in one pass.** The page used to emit every curated property group under one "Metadata"
  heading and then a fixed run of Datasamlinger, Kildeinformasjon and Statistikk, which could not
  put a built-in section between two property ones — the order the kilde mockup asks for. It now
  reads the new `sections` collection on `GET /api/explorer/kilder/{id}` and renders what it names,
  so reordering the page, renaming a property section or moving a property between sections is an
  edit a Munin curator makes rather than a release of this package. A built-in section keeps the
  word this package has for it — `groupTranslations` on a placement row is read by no view here, so
  renaming Datasamlinger is still a release. Each placed property group becomes a section of its
  own, anchored at its catalogue group key under a `section-` prefix and listed in the contents
  nav.
  Kildeinformasjon and Statistikk are named by no mockup and reserved by no seed yet: they keep
  their sections and every field in them, drawn after the placed sections, and each carries a key
  so the first placement that names it moves it with no release here. Against an API that sends no
  `sections` — every environment Munin has not migrated — the page draws exactly as it did before.
  (Fhi.Metadata-35w0p.22)
