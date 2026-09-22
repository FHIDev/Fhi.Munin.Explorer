category: Notes for hosts
- **New class name `munin-explorer__lede` on the explorer's lede, styled by Fhi.Helsedata.Stiler from
  the release that carries Fhi.Metadata-35w0p.66.** It is a `<p>` and a direct child of
  `.munin-explorer`, immediately after the title `h2`, on Kelda and Runa alike, and only there when
  `Lede` holds text. Stiler gives it a top margin, a 65ch measure and, above 1024px, a full-width grid
  row of its own under the heading, opened by `:has(> .munin-explorer__lede)`. On an older Stiler the
  lede falls into the explorer grid's catch-all and lands in the results column beside the filters,
  so leave `Lede` unset until your Stiler carries the rule. A CMS host that picks parameters from a
  fixed list must add `Lede` to it, or the text is dropped before it is set. (Fhi.Metadata-35w0p.32)
