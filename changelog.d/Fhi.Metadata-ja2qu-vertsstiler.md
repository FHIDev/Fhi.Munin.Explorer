category: Notes for hosts
- **Rename the rule if you wrote one for `skiplink-pagination`.** The class is
  `munin-explorer-skiplink-pagination`, the last borrowed name the component emitted. A host that
  styled the old one keeps a rule that no longer matches anything, and the failure reads backwards
  from an ordinary missing rule: what goes missing is the rule that *hides* the link, so it turns
  up visible above every multi-page result list rather than turning up unstyled. Both sample hosts'
  `host.css` carries the renamed rule — off-screen by default, revealed in place on `:focus`, never
  `display: none`, which would take it out of the tab order too. (Fhi.Metadata-ja2qu)
- **The Stiler floor is 0.1.14 for the pager and its skip link, 0.1.13 for everything else.**
  0.1.13 shipped before both were renamed into the `munin-explorer` prefix, so on 0.1.13 the pager
  renders at browser defaults and the skip link is permanently visible rather than hidden until it
  is focused. 0.1.14 carries both under `components/munin-explorer/`, and the skip link's rule is
  unscoped there — it matches the anchor wherever in the component's markup it is rendered.
  Checked against the published 0.1.14 package on the `Fhi.Helsedata.no` feed rather than against
  Stiler's sources: `staticwebassets/css/main.css` and `main.min.css` both carry
  `.munin-explorer-skiplink-pagination`. (Fhi.Metadata-ja2qu)
