category: Changed

- **The detail panel has Runa's two tabs** - Detaljer and Data, in helsedata's `variable-meta__tabs`
  vocabulary, with `role="tablist"`, correct `aria-selected` and arrow-key movement. Only the
  selected tab is in the tab order, so the tablist costs one tab stop rather than one per tab, which
  is what makes the arrow keys necessary rather than decorative. The tab returns to Detaljer when a
  different row is opened.
- **The panel's fields are grouped and laid out in lanes** - Identifikasjon and Plassering, Runa's
  groups, with the group heading as a small uppercase eyebrow rather than a heading-sized heading.
  The fields sit side by side in helsedata's `variable-meta__grid` (two lanes above 1280px, one
  below) instead of stacking. Runa uses three lanes; two of helsedata's beats three of ours.
- **The data period is drawn as a bar** - Runa's rule, taken from her implementation rather than
  guessed: the fill is the share of the variable's own lifetime that its data covers, floored at 5%
  so a short period still marks, and a period with no end date is drawn full and in a different
  colour, because "no end" means still running rather than unknown.
- **The kilde in the trail opens the kilde** - Runa links it to her own kilde route; this component
  has no routes, so the same affordance discloses the kilde in place. It shares `aria-expanded` and
  `aria-controls` with the existing button, so it reads as one control in two places.
- **Three fields take Runa's names** - the trail is a `Kildesti`, not a Datakilde; the panel's period
  is the `Dataperiode`; and the column header is plain `Kilde`. (Fhi.Metadata-7mqzs)
