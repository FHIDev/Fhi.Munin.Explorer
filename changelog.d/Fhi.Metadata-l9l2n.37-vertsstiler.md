category: Notes for hosts

- **`munin-explorer-filters__toolbar` is a new name to style**, on the container now holding the
  filter panel's Utvid alle, Skjul alle and Nivålinjer. The three buttons no longer carry
  `margin-right` or `margin-bottom` of their own, so a host that defines nothing for the name gets
  them back in plain inline flow with only word spacing between them. Both sample stylesheets carry
  the rule — `display: flex` with a `gap`, and buttons that shrink and wrap their own labels rather
  than the row breaking apart at a longer translation. (Fhi.Metadata-l9l2n.37)
- **This needs the Stiler release carrying `.munin-explorer-filters__toolbar`** (Stiler PR 39046).
  That PR must ship before this package's, or a host on Stiler gets the three buttons with no
  spacing between them until it does. The rule is inert on a Stiler that has it before the package
  draws the container.
