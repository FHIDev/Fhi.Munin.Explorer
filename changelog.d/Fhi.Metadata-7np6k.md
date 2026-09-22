category: Fixed
- **A link to a section of an open kilde or variable can be copied out of the address bar again.**
  Pressing an entry in a detail view's contents nav — or arriving on a link that names a section —
  left the fragment in the address for only as long as it took the explorer to mirror its state
  over it, so the reader scrolled to the right place and then held a link to the top of the page,
  and Back did not return to the section they came from. Both explorers now keep the incoming
  fragment for as long as the view it names is the one on screen, and drop it once the reader
  presses through to a different kilde, datasamling, search or sort. In the kildeutforsker Back
  returns to the section as well, because a navigation arrives with an address of its own whose
  fragment is honoured in its turn; the variabelutforsker reads the address once, at initialisation,
  so a fragment it has already dropped does not come back.
  Links the explorers build are unaffected and
  still carry no fragment: one naming a section of the view being left would name nothing in the
  view the link opens. (Fhi.Metadata-7np6k)
