category: Added
- **The contents nav on a detail page now marks the section the reader is in.** As the reader
  scrolls, the package's browser module sets `aria-current="location"` on the one entry whose
  section has reached the line a jump to it lands on: its `scroll-margin-top`, plus the scroller's
  `scroll-padding-top`. It removes the attribute from every other entry. At the bottom of the page
  the last entry is marked, unless the reader just pressed one of the sections that cannot scroll up
  to that line. Nothing is sent to the server while the reader scrolls. A host that does not serve
  the module still gets the nav, with no entry marked. (Fhi.Metadata-35w0p.15)
