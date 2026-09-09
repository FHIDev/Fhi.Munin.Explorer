category: Fixed

- **The kilder table's expand control now clears the 24 x 24 minimum target size.** It drew a
  literal "+", and since `hd-button-reset` strips a button's padding and font, that narrow glyph was
  most of the control: measured, the box was 20 x 24 CSS px, under what WCAG 2.5.8 asks for. It now
  discloses with helsedata's chevron — Stiler's `icon` is a 24px box — so the kilder table and the
  variable table open a row the same way and both controls are big enough to hit.
  (Fhi.Metadata-mpx2p)
