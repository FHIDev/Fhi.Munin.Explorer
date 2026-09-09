category: Notes for hosts
- **The 3:1 the level-guide rule owes is now due on first paint.** The guides used to appear only
  once a reader pressed `Nivålinjer`, so a rule that drew them too faintly was hard to notice;
  they are drawn at every first render now. A guide line is a non-text control under WCAG 1.4.11
  and has to clear 3:1 against whatever the host's own page ground paints — the sample stylesheets
  use `--grey60` for 6.76:1, and `--grey30`, the token every other border in that panel wears,
  measures 1.16:1 and is invisible on a desktop. `Fhi.Helsedata.Stiler` carries the rule; a host
  with its own is the one that owes the ratio. (Fhi.Metadata-dfygj)
