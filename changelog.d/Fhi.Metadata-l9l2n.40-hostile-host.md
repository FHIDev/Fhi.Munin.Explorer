category: Added
- A third sample host, `samples/HostileHost`, renders the component against helsedata's real
  `Fhi.Helsedata.Stiler` stylesheet under a header positioned over the top of document flow, and
  `scripts/check-hostile-host.sh` measures it with `getBoundingClientRect` as well as scanning it
  with axe. It found two defects on its first run that every existing check was green through.
