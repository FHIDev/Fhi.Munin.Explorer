category: Added
- **A detail page's catch-all section now lists the complete record rather than the fields the other
  sections did not want.** The section Munin keys `alle-metadatafelt` is drawn with a lead paragraph
  and a collapsed `<details>` over every property the payload holds a value for — those the named
  sections above already drew included — plus the counts and dates no property definition carries
  (total variables, data collections, data period, last updated in Munin). The repetition is the
  point: the sentence over it says nothing is left out, and on the Tromsø payload that is 34 rows
  where the section drew 8. A source's name, code, short name and kildetype are in it too, which no
  section drew before. The section is recognised by its `groupKey` and never by its heading, so a
  curator renaming it in Munin changes what a reader sees and nothing else; a payload that carries
  no `groupKey` has no such section and is drawn exactly as before. (Fhi.Metadata-35w0p.21)
