category: Fixed

- **The filter panel's toolbar keeps its three buttons on one row.** Utvid alle, Skjul alle and
  Nivålinjer sat in inline flow with a margin each, and the last one's trailing 16px counted
  against the line: at the 369px an expanded panel leaves once it grows a scrollbar, the row needed
  369.05px and Nivålinjer dropped onto a row by itself. They now sit in a container of their own,
  spaced by `gap`. (Fhi.Metadata-l9l2n.37)
