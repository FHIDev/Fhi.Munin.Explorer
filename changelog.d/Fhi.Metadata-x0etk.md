category: Fixed
- **Metadata values show their links and line breaks instead of printing `<br>` and `[label](url)` as
  text.** Ten free-text keys the catalogue authors with markup (`Beskrivelse`, `BeskrivelseFlerspraklig`,
  `BeskrivelseEngelsk`, `Kvalitetsnote`, `Innsamlingsmetode`, `InklusjonsOgEksklusjonskriterier`,
  `Forskrift`, `GeografiskAvgrensning`, `FormaalFlerspraklig`, `Kommentar`) now go through the same
  renderer as the page descriptions: links and line breaks only, everything else literal. It applies
  wherever a metadata section is drawn — kilde, datasamling and whole-variable pages, "Alle metadatafelt",
  and the variabelutforsker's row panel — so a variable's `Kommentar` also keeps the line breaks it was
  written with. The datasamling page's own Inklusjons- og eksklusjonskriterier section renders the same
  way. A link inside a `- ` or `1. ` list item and a reference-style link (`[label]` with a `[label]: url`
  line) now render too, descriptions included; list markers stay literal text and no list element is
  built. A Lovverk written as one markdown link shows its words in the page's key facts and becomes a
  link in the source-information box. No new class names.
  (Fhi.Metadata-x0etk)
