category: Fixed
- **A marked selection in the kildeutforsker no longer fails once many kilder are ticked.** With any
  datasamling marked, `KildeExplorer` used to expand every ticked kilde into all its datasamlinger,
  which on helsedata's test site meant 174 ids and a 414 for the ten largest kilder. It now hands
  over the ticked kilder as `kildeIds`, only the marked datasamlinger as `datasamlingIds`, and
  `treeTicks=or`. Munin then ORs the two, and a ticked kilde holding a mark narrows to its marked
  datasamlinger. A ticked kilde with no mark in it now brings its variables outside any
  datasamling too, so the note saying they were left out is gone, and the list no longer fetches
  each ticked kilde's datasamlinger before the button can be pressed. A selection too large to
  travel (more than 100 kilder or more than 100 marks, or more than
  `KildeSearch.MaxHandoverQueryLength` characters of query) is refused, and the selection line says
  so. The same 100-kilde limit now applies with nothing marked: the variable explorer reads back at
  most 100 kilder, and used to drop the rest without saying so. (Fhi.Metadata-9huq2.13)
