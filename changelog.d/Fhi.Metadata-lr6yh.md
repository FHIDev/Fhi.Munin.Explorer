category: Changed
- **A datasamling page now draws the sections Munin's placement rows declare, under their names
  and in their order, instead of one fixed "Metadata" block and three of the view's own.** Where
  the catalogue has placed a datasamling's properties, each of its sections is a section of the
  page — "Om datasamlingen", "Variabler", "Datakilde", "Alle metadatafelt" and whatever else the
  placement rows carry — so the section "Kildeinformasjon" held is the curator's "Datakilde",
  and Kvalitetsnote gets a section of its own the day a row gives it one, without this package
  being changed again. The fact rows the placement did not take follow the fields it did into
  the same section rather than heading a second one about the same subject, so the source
  block's parent, kildetype and Munin timestamps sit under "Datakilde" and the variable count
  under "Variabler"; nothing is dropped. A box follows the placement as a whole or not at all:
  where the fields it draws were placed in two different sections, or in a section this payload
  draws no rows for, it keeps its own block rather than taking every remaining row under a
  heading that names none of them. Ordering, ids and the fallback are the kilde page's, shared
  rather than written again: sections the rows name come first, in the order the API sent them,
  then the groups they name nowhere under the view's own "Metadata" heading, then the criteria,
  source and statistics blocks. A payload carrying no `sections` collection — an API predating
  the placement rows — renders exactly as before. (Fhi.Metadata-lr6yh)
