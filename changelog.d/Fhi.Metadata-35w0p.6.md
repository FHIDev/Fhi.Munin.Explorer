category: Changed
- **The kilde, datasamling and variable detail views read downwards in one column.** The sidebar
  each of them kept beside the main column is gone, and every block that lived in it now sits in
  the main flow in the order it was already read in: Kildeinformasjon and Statistikk on a kilde and
  a datasamling, and Kildeinformasjon, Dataperiode, Datatype, Variabelgrupper and Datasamlinger on
  a variable. Nothing was dropped and nothing was reordered — the document order of the blocks is
  the order it was before, so a deep link into any of the nine section ids still lands where it
  landed. They come after the view's own blocks and before whatever the explorer or the host passes
  into the `Sections` slot, which stays last. The visible change is at desktop widths only: below
  1024px the two columns already stacked, so a phone or a tablet reads the page it read before.
  (Fhi.Metadata-35w0p.6)
