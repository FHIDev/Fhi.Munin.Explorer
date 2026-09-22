category: Fixed
- **Datasamling pages show inherited inclusion and exclusion criteria.** The page uses the
  API's resolved criteria, falling back to the collection's own text for older responses.
  Criteria inherited from a delkilde or kilde now appear instead of being silently omitted.
  The text appears under About the data collection, with a subordinate heading and no separate
  contents entry. Existing links to the criteria still reach the text.
