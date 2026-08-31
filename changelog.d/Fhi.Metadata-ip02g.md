category: Fixed

- **The startup failure for a missing `ApiBaseUrl` names a host that answers off the FHI network** -
  it offered `https://munin.skytest.fhi.no` as the example, which resolves to a private address and
  is reachable only from inside FHI. A host that copied it booted, called an address it could never
  reach and showed "Kunne ikke hente variabler nå" while the API logged nothing. The message now
  gives `https://runa.munin.skytest.fhi.no` and says why the prefix matters, and a test reads `src/`
  and `samples/` to keep the unprefixed host out of both. (Fhi.Metadata-ip02g)
