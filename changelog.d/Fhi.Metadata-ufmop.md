category: Fixed
- **Every date the explorer shows a reader is now written the same way.** A kodeverk code's
  validity dates and the dataperiode chip in the filter panel wrote the culture's all-numeric
  short date — `01.01.2010` — while the same values elsewhere read `1. jan. 2010`, and the
  results list wrote a data period as month and year (`jan 1979 – des 2024`) over a variable page
  writing it as days (`1. jan. 1979 – 31. des. 2024`). All four now go through the shared date
  helper, so the day is spelled out with the ordinal dot in Norwegian and without it in English.
  The claim is about the day format and not about the whole range: a period with a missing start
  is still joined three different ways, which `Fhi.Metadata-msax9` settles rather than this. The
  ISO round-trip of the date-picker and of the API query string is unchanged. (Fhi.Metadata-ufmop)
- **A data period now shows the day the catalogue holds instead of rounding it to a month.**
  `dataFrom` and `dataTo` are day-precise at source — a `date` column computed from the ingest's
  `yyyyMMdd` `DataFra` string — and 212 of 1000 variables sampled off the live catalogue carry a
  start that is not the first of a month (`2016-06-17`, `2022-12-09`). `MMM yyyy` was discarding
  that day, and made `31. des. 2024` and `1. des. 2024` read alike. (Fhi.Metadata-ufmop)
