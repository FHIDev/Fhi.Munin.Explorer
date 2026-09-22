category: Fixed
- **Every date the explorer shows a reader is now written the same way.** A kodeverk code's
  validity dates and the dataperiode chip in the filter panel wrote the culture's all-numeric
  short date — `01.01.2010` — while the same values elsewhere read `1. jan. 2010`, and the
  results list wrote a data period as month and year (`jan 1979 – des 2024`) over a variable page
  writing it as days (`1. jan. 1979 – 31. des. 2024`). All four now go through the shared date
  helper, so the day is spelled out with the ordinal dot in Norwegian and without it in English,
  and one variable's dataperiode reads identically on the result row, in the open panel, in a
  saved list and on its own page. The ISO round-trip of the date-picker and of the API query
  string is unchanged. (Fhi.Metadata-ufmop)
