category: Changed
- **The status line now says which rows are on screen, not just how many** - "Viser 25 av 312
  variabler funnet" becomes "Viser 26–50 av 312 variabler funnet". It was only ever true of the
  first page, and it is also the results list's accessible name and the live announcement, so
  it is what tells a screen-reader user that a page turned. Hosts asserting on that sentence
  need to update. (Fhi.Metadata-l9l2n.12)
- **`PageSize` is clamped to 1–100** - the range the Explorer API itself accepts. A value
  outside it was previously passed through and silently changed by the server, which left the
  page count on this side describing a page size that was never used. (Fhi.Metadata-l9l2n.12)
