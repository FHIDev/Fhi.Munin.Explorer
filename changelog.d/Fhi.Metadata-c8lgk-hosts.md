category: Notes for hosts
- **The new-window icon is your stylesheet's.** The package draws no icon; helsedata's Stiler marks every `a[target="_blank"]` with its own, and the samples now carry that rule. A host without such a rule shows the link with no visible mark, and the hidden text still reaches a screen reader through `screenreader-only`, the class the package already uses.
