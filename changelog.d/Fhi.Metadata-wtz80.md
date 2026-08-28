category: Changed

- **A kilde's datasamlinger are now shown under the delkilde each belongs to.** `KildeView` — the
  view both `VariableExplorer` and `KildeExplorer` open a source with — drew one flat table of every
  datasamling the source holds, gathered through the delkilder and then sorted as if they were one
  list. It now draws the source's own in that table and then a nested `<ul>`, one item per delkilde,
  each carrying its own datasamlinger and any delkilder below it, walked to whatever depth the
  catalogue nests them. For a study series this is the difference between what the source holds and
  how it is arranged: Tromsø's fourteen datasamlinger are three of the study's own and eleven spread
  over five waves, and the waves are the study's organising fact. (Fhi.Metadata-wtz80)
- **A kilde with no delkilder is unchanged** — one table, same columns, same order. That is most
  kilder, and the section a host has already styled. (Fhi.Metadata-wtz80)
- **Each delkilde's name is a heading one level below the section's**, and one level deeper again
  for each level of the tree, flattening at `h6`. A host that sets `HeadingLevel` to keep its page's
  outline unbroken gets the tree in the outline too. (Fhi.Metadata-wtz80)
