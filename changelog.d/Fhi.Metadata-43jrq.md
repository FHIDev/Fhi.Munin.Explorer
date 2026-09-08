category: Fixed

- **Kilde and variabel detail pages no longer show the catalogue's storage vocabulary in a
  field's label.** "(språkmerket)" and "(flerspråklig)" - and the English "(language-tagged)" /
  "(multilingual)" - describe how the catalogue stores a value, not something a reader needs;
  they are stripped from every label and group name at render time, in both languages.
- **Formål, Rettslig grunnlag and Tittel no longer render twice on a kilde page.** The captured
  Barnediabetes source curates the same prose in both a plain field and its EHDS/HealthDCAT-AP
  mirror (`Formaal`/`FormaalFlerspraklig`, `hasLegalBasis` against the sidebar's Lovverk fact,
  `TittelFlerspraklig` against the name heading), so both used to draw under near-identical
  labels. The mirror field is now dropped from the metadata list whenever the fact it repeats is
  already shown elsewhere on the page; a source curating only the mirror still shows it.
  (Fhi.Metadata-43jrq)
