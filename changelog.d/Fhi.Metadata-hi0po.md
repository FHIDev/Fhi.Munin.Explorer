category: Fixed

- **The kilde, datasamling and variable views no longer push the page into horizontal scrolling, and
  their sidebar is legible.** Their fact lists asked for the panel grid's two-lane default, which in
  a 320px sidebar is 148px a lane — narrower than the Norwegian words in it, so
  `Personopplysningsloven` overflowed its lane, the sidebar overflowed its box by 64px, and the
  document gained a horizontal scrollbar at every width above 1280px. They now ask for
  `munin-explorer-meta__grid-1`, the single-lane modifier the same family already defines and the
  variable panel already uses. Measured in the samples: page overflow 17px → 0 at 1281–2560px, and
  the values go from about 148px wide to 288px. The metadata blocks in the wide middle column are
  untouched and still two lanes. (Fhi.Metadata-hi0po)
