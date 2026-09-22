category: Notes for hosts
- **New class names `munin-explorer-kilder__bar` and `munin-explorer-kilder__bar-fill` on Kelda's
  proportion bar, styled by Fhi.Helsedata.Stiler from the release that carries Fhi.Metadata-35w0p.68
  (commit 54a900f8).** The track `munin-explorer-kilder__bar` is an empty, `aria-hidden` span, the
  last child of a non-zero Variabler cell, directly after the digits; the fill
  `munin-explorer-kilder__bar-fill` is the one span inside it, with its width inline as
  `style="width:N%"`, a whole percent from 1 to 100. Stiler draws the track as a block under the
  digits so the column does not widen; set no width on the fill. On an older Stiler both spans are
  empty inline elements and draw nothing. (Fhi.Metadata-35w0p.30)
