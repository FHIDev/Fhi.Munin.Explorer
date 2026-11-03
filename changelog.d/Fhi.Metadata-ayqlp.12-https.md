category: Security
- **BREAKING for hosts: `ApiBaseUrl` must be https.** `AddMuninExplorer` now fails at startup unless `MuninExplorerOptions.ApiBaseUrl` is an absolute https URL, because the host's bearer token is sent on every call. Plain http is still accepted for a loopback host (`localhost`, `127.0.0.1`, `::1`), for local stubs. (Fhi.Metadata-ayqlp.12)
