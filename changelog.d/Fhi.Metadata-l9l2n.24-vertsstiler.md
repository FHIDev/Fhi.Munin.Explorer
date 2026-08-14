category: Notes for hosts
- The host stylesheet must provide a `variabelutforsker-visuelt-skjult` rule — the usual
  clip-rect recipe that hides an element visually while leaving it readable by assistive
  technology, **not** `display: none`, which hides it from screen readers too. Without it the
  table caption and the "Ikke oppgitt" stand-in for empty cells appear on screen. Visible focus
  and contrast are the host's to get right for the same reason: the package ships no CSS. All
  three are listed on the doc comment on `Variabelutforsker`.
