category: Changed
- **Ticks in different branches of the Variabelutforsker's kilde tree are combined with OR.** A whole datasamling
  ticked beside variabelgrupper under another datasamling now lists both instead of nothing, as the old
  helsedata.no explorer did (Inge, 28.10: «alt som er krysset av»). A tick with a more specific tick below it still
  only narrows, the other facets are still ANDed, and the list, the rows, the counts and the export follow the same
  rule. «Vis bare variabler fra denne datakilden/datasamlingen» and a kilde handed over from the kildeutforsker
  still mean «this kilde's variabler in that datasamling» and stay ANDed. Needs a Munin with Fhi.Metadata-jk7fp
  (Munin #6961); an older Munin ignores the parameter and keeps ANDing.
