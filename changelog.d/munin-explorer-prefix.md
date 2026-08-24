category: Notes for hosts

- **The component now writes `munin-explorer-*` class names instead of helsedata's own.** It used to
  borrow `variable-explorer`, `variable-data-list`, `variable-dataitem` and `variable-meta`, and
  inherit their rules for free from the variable page's stylesheet — the page it exists to replace.
  **Hosts need `Fhi.Helsedata.Stiler` 0.1.13 or later**, which is where those rules now live; on an
  older Stiler the component renders at browser defaults.
- **A host outside helsedata.no can style all but one name from Stiler.** 92 of the 95 class names
  the component emits were in Stiler 0.1.13. Two of the three that were not were the pager's, and
  `Fhi.Metadata-hyyxl` renamed those into the prefix with the rest — see that entry, including
  which Stiler release carries their rules. The one still outstanding is `skiplink-pagination`,
  helsedata's own, which needs a single visually-hidden-until-focused rule from any host outside
  their estate.
- **Design-system names are unaffected.** `hd-button-square`, `searchbox__freetext`, `headline`,
  `caption`, `infobox` and the rest are Stiler's, are still borrowed deliberately, and are not part
  of this rename.
