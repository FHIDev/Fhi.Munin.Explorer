category: Changed

- **Publiseringen gaar til helsedatas interne feed** i stedet for nuget.org. Det er feeden deres
  Optimizely-prosjekt allerede restorer fra, og der deres egne pakker - inkludert Stiler - ligger.
- **Credentialen er ikke laast til én form**: feeden tar imot baade en PAT og et Entra-token som
  passord, saa skriptet bryr seg ikke om hvilken, og en senere overgang til federert OIDC endrer
  bare hvordan hemmeligheten fylles inn. (Fhi.Metadata-l9l2n.5)
- **Ingen lagret hemmelighet og ingen personlig avhengighet**: publiseringen autentiserer med OIDC
  mot Entra, slik resten av FHIs repoer gjoer, i stedet for en PAT som henger paa én person.

