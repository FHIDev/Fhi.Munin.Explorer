category: Fixed
- **A variable list is titled with its own name.** `VariableListView`'s heading now reads the name
  of the list on screen, and follows a switch or a rename, where it used to read "Mine
  variabellister" for every list; those words move to the eyebrow above it, as on the variable,
  kilde and datasamling pages. The heading keeps its level, id and class, and before the lists
  load or when there are none it still reads "Mine variabellister" with no eyebrow. The table and
  its scroll region are now named by the heading through `aria-labelledby` rather than an
  `aria-label` of their own. No new class name: the eyebrow is `munin-explorer-page__eyebrow`.
