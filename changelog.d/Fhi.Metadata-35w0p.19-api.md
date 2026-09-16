category: Added
- **`PropertyMetadataEntry` carries `GroupSortOrder`.** The Explorer API sends the order of the
  section itself on the surface the payload was fetched for, so a section's position no longer has
  to be inferred from whichever of its properties happen to hold a value. Nullable, like `GroupKey`
  beside it: null means no placement names the section there, or the API predates the field.
  (Fhi.Metadata-35w0p.19)
