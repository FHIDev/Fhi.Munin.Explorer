category: Fixed
- **The datatype and status cells in the result rows are no longer marked as Norwegian.** The
  datatype name arrives in the reader's language and the status is the API's token, so
  `lang="no"` had an English reader's screen reader pronounce them with a Norwegian voice. Both
  cells now inherit the host page's language, in the variable search and in the saved lists alike;
  the code, kilde, datasamling and variabelgruppe cells keep `lang="no"`. The text shown is
  unchanged.
