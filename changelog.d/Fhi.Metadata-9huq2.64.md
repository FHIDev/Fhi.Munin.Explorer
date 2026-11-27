category: Fixed
- **Back or Forward no longer reopens a shared list's tab after the reader went back to the results.** A reader who
  opened a `?delekode=` link, chose «Søkeresultat» and opened a row, then pressed Back, got the list tab again, because
  the address still carried the code. A code now opens its list tab once until the page is reloaded, on the arrival
  that first brought it, as a `?listeId=` does: a later arrival with the same code, by Back, Forward or a link, leaves
  the results tab selected. A link with a code not seen before still opens its tab.
  (Fhi.Metadata-9huq2.64)
