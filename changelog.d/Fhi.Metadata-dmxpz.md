category: Changed
- **The kilde explorer's kildetype filter follows Munin's order.** It listed the kildetyper
  alphabetically by label; it now lists them in the order Munin's filters endpoint gives them.
  A Munin that ranks them puts Sentralt helseregister, Befolkningsbasert helseundersøkelse,
  Nasjonalt medisinsk kvalitetsregister and Annen datakilde first, then the rest. A type the endpoint does not
  list, or every type while it has not answered, still sorts by label.
