category: Fixed
- **The kilde explorer's kildetype filter keeps Munin's order for every type.** It ranked by the
  filters endpoint's list, which holds only kildetyper with variables, so a Sentralt helseregister
  with none sorted last. It now ranks the four fixed types directly: Sentralt helseregister,
  Befolkningsbasert helseundersøkelse, Nasjonalt medisinsk kvalitetsregister, Annen datakilde,
  then the rest by label, whatever the endpoint lists or whether it has answered.
