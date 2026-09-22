category: Changed
- **The result row's "Lagre i liste" button is quieter.** It wears
  `button-square--ghost-blue`, the variant the saved-list view's own buttons already use, rather
  than the filled `button-square--secondary`. It is drawn once per result row, so a default page
  of 20 results drew 20 primary-weight controls competing with the variable names being scanned.
  Ghost-blue draws no border either, which is what took the button off `button-square--ghost` in
  the first place (Fhi.Metadata-q7i5e) — but that variant's text is `--dark`, the row's own
  colour, so it read as bold prose, while ghost-blue's is `--primary`, the colour this package
  already relies on to mark a control the reader can press. Saved and unsaved stay told apart by
  their words and by `aria-pressed`, not by the colour. No new class name and no new rule: both
  variants are Stiler's. (Fhi.Metadata-35w0p.64)
