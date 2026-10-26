category: Changed
- **The kilde explorer's kildetype filter lists kildetyper in Munin's fixed order.** It listed them
  alphabetically by label; it now puts Sentralt helseregister, Befolkningsbasert
  helseundersøkelse, Nasjonalt medisinsk kvalitetsregister and Annen datakilde first, in that
  order, then the rest by label, in every language. The order is by code and does not depend on
  which kildetyper the filters endpoint lists, so a type with no variables keeps its place.
