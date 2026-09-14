category: Changed
- **`VariableListView` is drawn on the detail pages' chassis, and its heading is a real heading.**
  The saved-list view was the one surface with no chassis at all: its root element carried no class,
  so every layout rule the kilde, datasamling and variable views answer to missed it, and it sat at
  whatever width and rhythm the host happened to give a bare `<div>`. It now renders through
  `DetailPage` like the other three — `munin-explorer-page` on the root, the name block above the
  fold, and the list, its controls and its pager in `munin-explorer-page__body` /
  `munin-explorer-page__main`. The version marker `data-munin-explorer-version` has not moved: it is
  still on the root element, which is now the chassis's. The table, the scroll container around it
  and the pager are untouched, and paging stays flat.
  The heading used to be assembled as a string and injected as raw markup, so it carried neither a
  class nor an id — it missed `headline headline-s` and nothing could link to it. It comes from the
  same heading helper the other three views use now, at the level the host asked for, with an id of
  its own. (Fhi.Metadata-35w0p.13)
- **This view deliberately has no contents nav, so its body is one column and never a rail.**
  The Kilde filter beside the list already does the grouping a contents nav would do, so
  `DetailPage.Contents` is left null and `munin-explorer-page__toc` is not emitted. Both sample
  stylesheets now take the second track back rather than merely not giving it — the rule that gives
  a body two tracks is gated on the contents column, but the pinned Stiler's own copy is ungated and
  was winning for a body with one child, which is a 250px empty rail down the left of a 1440px page.
  (Fhi.Metadata-35w0p.13)
