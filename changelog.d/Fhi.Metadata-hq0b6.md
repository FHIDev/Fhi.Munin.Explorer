category: Fixed
- **The Status column in variable search now reads in the reader's language.** The result row
  wrote the API's raw token, so a Norwegian reader who turned the column on — or who included
  historical variables, which puts it on screen by itself — read "Active" and "Historical" beside
  Norwegian headers. It now goes through the same `Texts.VersionStatusLabel` the whole-variable
  page uses, giving "Aktiv" and "Historisk" in Norwegian and "Active" and "Historical" in English,
  whatever case the token arrives in. A token the map has not seen is still shown as it arrived,
  and a variable with no status still reads "Ikke oppgitt". (Fhi.Metadata-hq0b6)
- **A Norwegian-spelled `aktiv` is now translated for an English reader, as `historisk` already
  was.** The status map took either spelling of Historical but only the English spelling of
  Active, so an `Aktiv` from the catalogue would have reached an English reader as the Norwegian
  word while its sibling translated. Both words are now taken in both spellings, on the
  whole-variable page's version table as well as in the search results. (Fhi.Metadata-hq0b6)
