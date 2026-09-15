category: Added
- **Kilde and delkilde rows in the variable explorer's Kilde facet now lead with a folder glyph,
  and a biobank kilde says so in a badge.** The folder is the same glyph at both levels, as in
  Kelda's own hierarchy tree and in Runa: what tells a kilde from a delkilde is where its row sits,
  so a second picture would invite a reader to look for a difference the tree does not draw. It
  sits between the checkbox and the name, exactly where a datasamling's datakategori glyphs already
  do, and the checkbox keeps its indentation whatever leads the name beside it. The glyph is
  `aria-hidden` and is named nowhere: unlike a datakategori it repeats the nesting the list already
  carries, so there is nothing for it to add in words.
  The badge is read off the `kildeType` the `GET /api/explorer/filters` answer already carries for
  each kilde, so nothing new is fetched to draw it, and it is real text inside the `<label>` rather
  than a rule or a picture — it is part of the checkbox's accessible name, and it is a member of its
  own separate from the glyphs, so turning decoration off cannot take it off the row. Biobank is the
  whole of the vocabulary marked out: the API's kildetype list holds eight values and Prøvesamling
  is not among them — that word is an EHDS datakategori, on a field the filters answer does not
  carry for a kilde, so marking one is `Fhi.Metadata-wxn6g` rather than part of this. Any other
  kildetype, and a kilde carrying none at all, wear no badge; an empty capsule would say they were
  one of the kinds the badge names. The word is this package's own bilingual copy rather than the
  API's resolved kildetype label, unlike every other kildetype word in this panel, because the badge
  marks membership and is drawn under a group heading already carrying the API's word for the same
  value. A chip for a badged kilde still carries the name alone.
  (Fhi.Metadata-aw203)
