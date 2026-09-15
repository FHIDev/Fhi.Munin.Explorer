category: Fixed
- **A detail view's contents nav scrolls to its section instead of leaving the page.**
  Every entry carried a bare `#id`, which a browser resolves against the document's
  `<base href>` rather than against the page being read. helsedata's Optimizely host sets that to
  `/`, so each press navigated to the site root and dropped both the route and the open
  `?kilde=` — the reader lost their place entirely. The hrefs now carry this page's own path and
  query in front of the fragment, taken from the address the explorer last wrote rather than from
  `NavigationManager.Uri`, which a `history.replaceState` mirror leaves behind. Each target
  section also carries `tabindex="-1"`, so on a host that does not intercept the press the
  browser's own fragment jump moves keyboard focus into the section rather than only the
  viewport. No JavaScript: fragment navigation is plain HTML. (Fhi.Metadata-l9l2n.114)
