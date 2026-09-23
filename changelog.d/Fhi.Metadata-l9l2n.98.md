category: Added
- **The kilde table can show two coverage columns, Kodeverk % and Statistikk %** - Each is the share
  of a kilde's variables that carry a kodeverk or statistics, so a reader can see which sources are
  worth opening without opening each one. Both are optional columns in the column picker, off by
  default, with the keys `andelKodeverk` and `andelStatistikk`. The numbers are the kilde list's own
  (`KildeSummary.KodeverkShare` and `StatisticsShare`), shown as the API sends them, so no extra
  request is made. A measured nought reads "0 %". A kilde with no variables to measure shows a dash
  that a screen reader announces as "ikke målt" or "not measured". Both are dimmed like a nought
  count. The columns are not sortable. No new class name. With every column on, the scroll box's
  column-count modifier now reaches 18 with the selection column and 17 without.
  (Fhi.Metadata-l9l2n.98)
