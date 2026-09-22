category: Changed
- **A datasamling page places its inclusion and exclusion criteria where Munin's placement rows put
  them, instead of always after every other section.** The criteria block now answers to the
  built-in key `inklusjons-og-eksklusjonskriterier`, which Munin seeds on DatasamlingDetalj at band
  2000 (Fhi.Metadata-87tng), so an untouched page reads Om datasamlingen, the criteria,
  Kvalitetsnote, Variabler, Datakilde, Alle metadatafelt, and a curator's reorder in Sideoppsett
  reaches the page without a release here. The section's id stays `criteria`, so existing deep links
  still land. A payload with no placement row for the criteria, or no `sections` at all, draws them
  where it did before; empty criteria still draw no section and no contents link.
  (Fhi.Metadata-l9l2n.120)
