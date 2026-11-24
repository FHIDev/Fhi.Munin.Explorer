category: Fixed
- **An instrument's description keeps its paragraph spacing when it holds a list.** With `CatalogueFormatting` on, a
  description containing a list is drawn in a `div` rather than a `p`, which lost the browser's paragraph margins
  under the instrument's name, because no host rule spaces the bare `ingress` class there. That `div` now carries the
  same `margin-block: 1em` inline; a description with no list is unchanged, and so are the kilde, datasamling and
  variable descriptions, whose host rules set their own margin.
