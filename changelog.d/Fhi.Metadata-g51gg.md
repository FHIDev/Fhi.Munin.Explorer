category: Changed
- **The Kilde facet's tree now reaches the variabelgrupper, and every branch it gains starts
  shut.** A datasamling holding variabelgrupper, a delkilde whose groups are in none of its
  datasamlinger, and a group with groups nested under it are all branches now, opened by the same
  control every other branch of that tree already had: a real button beside the row, named after
  what it opens, carrying `aria-expanded`, and operable from the keyboard. A leaf gets no
  disclosure at all, and what hangs under a shut branch is not rendered rather than hidden, so
  nothing inside one can be tabbed into. Opening a branch narrows nothing and asks the API for
  nothing — the whole tree, the groups included, is read off the `GET /api/explorer/filters` answer
  the panel already had, so a kilde's groups still cost no request of their own and every count is
  the cross-filtered one that answer sent. The Kilde facet's own search now matches group names
  too, and opens the branches down to what it matched. A group hangs under every datasamling its
  variables are in, so one group can be drawn at more than one place in the tree, and each of those
  places opens and shuts on its own. The groups are containers for now: ticking one is wired to the
  standalone Variabelgruppe facet's selection in a later change, and that facet itself is unchanged
  here. No new class name — the branch row and its disclosure wear the two names
  Fhi.Metadata-adog5 introduced. (Fhi.Metadata-g51gg)
