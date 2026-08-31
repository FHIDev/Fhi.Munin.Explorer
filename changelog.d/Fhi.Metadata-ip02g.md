category: Fixed

- **The startup failure and the README snippet for `ApiBaseUrl` name a host that answers off the FHI
  network** - both offered the Munin test host without its `runa` prefix, which resolves to a private
  address reachable only from inside FHI. A host that copied either one booted, called an address it
  could never reach and showed "Kunne ikke hente variabler nå" while the API logged nothing. Both now
  give `https://runa.munin.skytest.fhi.no`, the exception says why the prefix matters, and a test
  reads the whole checkout - sources, samples, tests, docs, scripts, workflows and the packaged
  README - to keep the unprefixed host out of everything a host developer can copy. (Fhi.Metadata-ip02g)
