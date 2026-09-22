category: Fixed
- **The Status column in variable search now reads in the reader's language.** The result row
  wrote the API's raw token, so a Norwegian reader who turned the column on — or who included
  historical variables, which puts it on screen by itself — read "Active" and "Historical" beside
  Norwegian headers. It now goes through the same `Texts.VersionStatusLabel` the whole-variable
  page uses, giving "Aktiv" and "Historisk" in Norwegian and "Active" and "Historical" in English,
  whatever case the token arrives in. A token the map has not seen is still shown as it arrived,
  and a variable with no status still reads "Ikke oppgitt". (Fhi.Metadata-hq0b6)
