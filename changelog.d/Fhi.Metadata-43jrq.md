category: Fixed

- **Kilde and variable detail pages no longer show the catalogue's storage vocabulary in a
  field's label.** "(språkmerket)" and "(flerspråklig)" - and the English "(language-tagged)" /
  "(multilingual)" - describe how the catalogue stores a value, not something a reader needs;
  they are stripped from every label and group name at render time, in both languages.
- **Formål no longer renders twice on a kilde page.** `Formaal` and its EHDS/HealthDCAT-AP mirror
  `FormaalFlerspraklig` curate the same prose in every fixture that populates both, so the plain
  field is now dropped from the metadata list once the mirror also holds a value; a source
  curating only the mirror still shows it. `Rettslig grunnlag`/`hasLegalBasis` and
  `Tittel`/`TittelFlerspraklig` were suspected of the same duplication but are NOT deduplicated:
  real fixtures show their EHDS mirror can carry a translation the plain field lacks, so hiding it
  would delete content rather than tidy the page. Both still render, and both still lose the
  "(språkmerket)"/"(flerspråklig)" qualifier from their label. (Fhi.Metadata-43jrq)
