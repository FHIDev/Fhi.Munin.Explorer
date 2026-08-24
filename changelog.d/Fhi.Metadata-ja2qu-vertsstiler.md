category: Notes for hosts
- **Rename the rule if you wrote one for `skiplink-pagination`.** The class is
  `munin-explorer-skiplink-pagination`, the last borrowed name the component emitted. A host that
  styled the old one keeps a rule that no longer matches anything, and the failure reads backwards
  from an ordinary missing rule: what goes missing is the rule that *hides* the link, so it turns
  up visible above every multi-page result list rather than turning up unstyled. Both sample hosts'
  `host.css` carries the renamed rule — off-screen by default, revealed in place on `:focus`, never
  `display: none`, which would take it out of the tab order too. (Fhi.Metadata-ja2qu)
- **The Stiler floor is 0.1.14 for the pager and its skip link, 0.1.13 for everything else.**
  0.1.13 shipped before both were renamed into the `munin-explorer` prefix. It does contain a
  `skiplink-pagination` rule under `components/munin-explorer/`, but scoped as
  `.munin-explorer-header .skiplink-pagination`, which matches nothing: that header opens and
  closes entirely inside the column picker, while the anchor is rendered beside the result list.
  0.1.14 has it unscoped. (Fhi.Metadata-ja2qu)
