category: Fixed
- **Double-clicking or shift-clicking a control in a variable's panel leaves what it opened
  open** - "Vis datakilde", "Vis datasamling", the kilde step of the panel's trail, "Vis hele
  variabelen" and its way back, and a version row in the whole variable: the second click of a
  double-click toggled the view straight back shut, so it flashed and the reader landed where
  they started. A shift-click extending a selection across the panel's text did the same, at
  those controls and at "Vis koder". Both gestures are now refused the way a result row already
  refused them; deliberate repeated pressing still toggles both ways, and Enter or Space on the
  control is unaffected. Four disclosures outside this panel still have the defect and are
  tracked separately. (Fhi.Metadata-j1j3i, Fhi.Metadata-zel47)
- **"Vis hele variabelen" no longer promises a disclosure it does not make** - the button carried
  an `aria-expanded` that was always `false` and an `aria-controls` naming an element that is
  only in the document once the button is gone. It opens a view in place of the list rather than
  expanding anything beside itself, so it now carries neither, matching the trail's kilde step.
  (Fhi.Metadata-j1j3i)
