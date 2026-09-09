category: Notes for hosts

- **Three new class names for the active-filter row, styled from `Fhi.Helsedata.Stiler` PR 39206.**
  `munin-explorer-filters__active` is the row, `munin-explorer-filters__chip` the capsule around one
  ticked value and `munin-explorer-filters__chip-remove` the close control inside it. Handles, all
  three: a host that defines none of them still gets every word and every control, in inline flow
  rather than in a row of capsules, and the close control's accessible name is written down in the
  markup rather than drawn. A host writing its own rules owes the close control a 24×24 box — that
  is a WCAG 2.5.5 target and the only thing here that is lost rather than merely undressed — and
  owes the capsule's edge 3:1 against whatever the page ground is. Both sample stylesheets show the
  shape. Nothing else in the row is new: the heading wears Stiler's `caption margin--none`, where
  `margin--none` is load-bearing because a bare `caption` paragraph's block margins break the row's
  alignment, and the clear-all is new but wears no new name: `hd-button-square
  button-square--ghost` is Stiler's, and the facet panel's own fold toggle already wears it.
  (Fhi.Metadata-ofoyw)
