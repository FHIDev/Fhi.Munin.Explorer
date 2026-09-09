category: Notes for hosts
- The kilde detail hierarchy requires host styling for `munin-explorer-hierarchy`,
  `munin-explorer-hierarchy__nodes`, `munin-explorer-hierarchy__branch`,
  `munin-explorer-hierarchy__leaf`, `munin-explorer-hierarchy__count` and
  `munin-explorer-hierarchy__metadata`. Preserve native `details`/`summary` disclosure behavior,
  list semantics, visible keyboard focus and wrapping of long names. Count badges are text,
  not controls; leaf rows must not appear expandable.
- Helsedata styling is tracked separately in `Fhi.Metadata-wihod`; this change alone does not
  supply those rules in Stiler. The sample styles are provisional helsedata-based stand-ins.
  The component ships no CSS or JavaScript assets. Tab visits disclosure summaries, and
  Enter or Space toggles them locally in the browser.
