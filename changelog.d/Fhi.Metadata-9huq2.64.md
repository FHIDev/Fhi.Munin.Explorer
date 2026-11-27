category: Fixed
- **Back or Forward no longer reopens a shared list's tab after the reader went back to the results.** A reader who
  opened a `?delekode=` link, chose «Søkeresultat» and opened a row, then pressed Back, got the list tab again, because
  the address still carried the code. A code now opens its list tab once, on the arrival that brought it, as a
  `?listeId=` does. A new `delekode` link still opens its tab. `VariableSearch` has a new parameter,
  `ShareCodeArrived`, which defaults to `true`, so a host that draws `VariableSearch` itself keeps today's behaviour.
  (Fhi.Metadata-9huq2.64)
