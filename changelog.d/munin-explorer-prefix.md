category: Notes for hosts

- **The component now writes `munin-explorer-*` class names instead of helsedata's own.** It used to
  borrow `variable-explorer`, `variable-data-list`, `variable-dataitem` and `variable-meta`, and
  inherit their rules for free from the variable page's stylesheet — the page it exists to replace.
  **Hosts need `Fhi.Helsedata.Stiler` 0.1.13 or later**, which is where the rules now live; on an
  older Stiler the component renders at browser defaults.
- **A host outside helsedata.no can style the component for the first time.** The rules it needs no
  longer sit in a bundle only that one site carries.
