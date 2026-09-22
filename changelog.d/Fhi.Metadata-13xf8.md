category: Fixed
- **The datatype and status cells in the result rows are no longer marked as Norwegian.** The
  datatype name arrives in the reader's language and the status is the API's token, so
  `lang="no"` had an English reader's screen reader pronounce them with a Norwegian voice. In the
  variable search both cells now inherit the host page's language; in the saved lists, which have
  no status column, the datatype cell does. The code, kilde, datasamling and variabelgruppe cells
  keep `lang="no"`, and the text shown is unchanged.
