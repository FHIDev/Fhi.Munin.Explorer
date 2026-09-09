category: Notes for hosts
- **One new class name for the kildeutforsker's result row, `munin-explorer-results__toolbar`,
  styled from `Fhi.Helsedata.Stiler` PR 39220.** It is the row holding the result count, the Sorter
  control and the Kolonner picker. A handle, and the plainest kind: a host that defines nothing for
  it gets the three back as three blocks in ordinary flow, which is exactly what shipped before the
  name existed, so what a rule buys is two rows of vertical space and no reader loses a word or a
  control. The rules landed in Stiler after 0.1.42 was cut, so the floor is the first release that
  follows it — a host on 0.1.42 or older is in the undressed case above rather than a broken one.
  A host writing its own owes the row three things the sample stylesheets show: the count is the
  row's first child and has to take the slack, or it stops holding the left edge; the row has to
  end its wrapped lines at the trailing edge, or the picker's right-aligned menu opens off the
  screen; and the open menu needs a width of its own, because a row makes
  `munin-explorer-header__actions` content-wide and the menu otherwise shrinks to the width of the
  Kolonner button with the column names spilling out of it. The name says `results` and the element
  sits above `munin-explorer-results` rather than inside it, which is deliberate: the results
  container is drawn only when there are rows, and the count in this row is the component's one
  polite live region, which has to be in the DOM before its text arrives. (Fhi.Metadata-tciss)
