category: Changed
- **Nivålinjer is a real switch now, not a button that stays pressed.** The toggle over the
  variabelutforsker's facet tree was a `<button>` carrying `aria-pressed`, which a screen reader
  announces as a button held down; it is a `role="switch"` carrying `aria-checked` now, which
  announces as on and off — what the control has always meant. It is still a native `<button>`, so
  it keeps the keyboard behaviour a button has: Tab reaches it and Space activates it, with no key
  handler of ours in the way. Its label and its position in the toolbar are unchanged, and it still
  writes the same `data-level-lines` marker on the panel, so nothing a host stores or styles for the
  lines themselves changes. What did change is the control's own markup: it wears
  `munin-explorer-switch` alone, with a track and a thumb inside it, and no `hd-button-square` or
  `button-square--*` beside it — see the note for hosts. (Fhi.Metadata-l9l2n.87)
