category: Fixed
- **Metadata values show their links and line breaks instead of printing `<br>` and `[label](url)` as
  text.** Eight free-text keys the catalogue authors with markup (`BeskrivelseEngelsk`, `Kvalitetsnote`,
  `Innsamlingsmetode`, `InklusjonsOgEksklusjonskriterier`, `Forskrift`, `GeografiskAvgrensning`,
  `FormaalFlerspraklig`, `Kommentar`) now go through the same renderer as the descriptions: links and
  line breaks only, everything else literal. It applies wherever a metadata section is drawn — kilde,
  datasamling and whole-variable pages, and the variabelutforsker's row panel — so a variable's
  `Kommentar` also keeps the line breaks it was written with. The datasamling page's own Inklusjons- og
  eksklusjonskriterier section renders the same way. No new class names.
  (Fhi.Metadata-x0etk)
