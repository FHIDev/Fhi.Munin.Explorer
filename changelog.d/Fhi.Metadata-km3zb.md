category: Changed
- **A variabelgruppe in the Kilde tree is a checkbox now, and it is the same selection as the
  standalone Variabelgruppe facet's.** Both surfaces write and read the one
  `VariableFilter.VariabelgruppeIds`, so ticking a group on either shows it ticked on the other,
  and one tick produces exactly one chip over the results however many places the tree draws that
  group at — a group hangs under every datasamling its variables are in, and every placement of it
  carries the same state. Each row now carries its cross-filtered count beside the name, as the
  levels above it already did. A group the API opts out of the standalone facet stays out of it
  whether or not it is selected: the kilde tree is the one surface that offers it, and its chip is
  the way off it from anywhere else on the page. Shared links keep working unchanged — a
  `variabelgruppeIds` value restores as a tick in the tree, and its name reaches the chip and the
  hierarchy trail out of the tree's own collection rather than reading "Variabelgruppe". Opening
  and shutting a branch still narrows nothing and asks the API for nothing, so a selection and its
  chip survive folding the branch it was made in. No new class name. (Fhi.Metadata-km3zb)
