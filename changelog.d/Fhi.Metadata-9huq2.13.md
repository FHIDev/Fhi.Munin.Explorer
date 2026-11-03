category: Fixed
- **A marked selection in the kildeutforsker no longer fails once many kilder are ticked.** With any
  datasamling marked, `KildeExplorer` used to expand every ticked kilde into all its datasamlinger,
  which on helsedata's test site meant 174 ids and a 414 for the ten largest kilder. It now hands
  over the ticked kilder as `kildeIds`, only the marked datasamlinger as `datasamlingIds`, and
  `treeTicks=or`. Munin then ORs the two, and a ticked kilde holding a mark narrows to its marked
  datasamlinger. A ticked kilde's variables outside any datasamling now come along too, so the
  note saying they were left out is gone, and the list no longer fetches each ticked kilde's
  datasamlinger before the button can be pressed. A selection too large for the address (more than
  `KildeSearch.MaxHandoverQueryLength` characters of query) is refused, and the selection line
  says so. (Fhi.Metadata-9huq2.13)
