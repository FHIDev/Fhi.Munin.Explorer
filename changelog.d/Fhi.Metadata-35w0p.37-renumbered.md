category: Changed
- **`SortField`'s existing members are renumbered — rebuild against this version.** The four new
  members are inserted at their own columns rather than appended, because the enum is declared in
  the order a UI should offer the orders in and a host is free to build its control from
  `Enum.GetValues`. Source compatible, so a rebuild is the whole of it: `SortField.Kilde` still
  names the kilde order, and a link carrying `?sort=Kilde` still reads back as one, since the URL
  and the wire both carry a name rather than a number. A host that has stored the underlying
  `int` has to remap it. (Fhi.Metadata-35w0p.37)
