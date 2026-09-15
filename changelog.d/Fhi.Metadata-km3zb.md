category: Changed
- **A variabelgruppe in the Kilde tree is a checkbox now, and it is the same selection as the
  standalone Variabelgruppe facet's.** Both surfaces write and read the one
  `VariableFilter.VariabelgruppeIds`, so ticking a group on either shows it ticked on the other,
  and one tick produces exactly one chip over the results however many places the tree draws that
  group at — a group hangs under every datasamling its variables are in, and every placement of it
  carries the same state. Each row now carries its cross-filtered count beside the name, as the
  levels above it already did. A group the API opts out of the standalone facet gets no checkbox
  there whether or not it is selected — where that facet names the group at all it is to nest the
  offered ones under it, so the row stays a container. The kilde tree is the one surface that
  offers such a group, and its chip is the way off it from anywhere else on the page. One id the two
  collections name differently is named by the facet's own copy wherever that facet reaches: its own
  checkbox, the chip and the trail step agree, while the tree names its rows from the collection it
  draws them out of. Shared links keep working — a `variabelgruppeIds` value restores as a tick in
  the tree, and its name reaches the chip and the hierarchy trail out of the tree's own collection
  rather than reading "Variabelgruppe". Opening
  and shutting a branch still narrows nothing and asks the API for nothing, so a selection and its
  chip survive folding the branch it was made in. No new class name. (Fhi.Metadata-km3zb)
