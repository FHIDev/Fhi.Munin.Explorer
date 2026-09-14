category: Added
- **The kilde, datasamling and variable pages get a contents nav.** A list of links down the left
  of the page, one per section the page actually drew, so a reader can reach the metadata, the
  datasamlinger, the version history or the statistics without scrolling for them. It fills the
  contents column `DetailPage` has had since the chassis landed, and a page that drew no section
  draws no column at all rather than an empty rail. The links are plain `#fragment` anchors — the
  browser does the scrolling and the package still ships **no JavaScript**; the highlight that
  follows the reader down the page is a separate change and needs a script, so it is not here.
  Every entry is built from the same condition its section renders under, so a nav never offers a
  link to a block the page suppressed, and the `href` values are the fixed English section ids in
  both languages while the words translate — a link one reader sends another lands in the same
  place whichever language either is reading. (Fhi.Metadata-35w0p.12)
