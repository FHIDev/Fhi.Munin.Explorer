category: Added

- **The saved list can be downloaded** — Excel or CSV, with or without codebooks, from the list view.
  `IMuninExplorerClient.ExportListAsync` posts the ids to `api/explorer/lists/export` and returns the
  file the API produced. That endpoint is anonymous: the ids travel in the body, so it has no need to
  know whose list they came from. (Fhi.Metadata-7mx2s)
- **The file's name and content type come back from the API, not composed here.** CSV *with*
  codebooks is answered as a zip of two files, so a caller that built the name from the format it
  asked for would hand the reader a `.csv` their spreadsheet refuses to open.
- **The download is every id in the list, not the page on screen.** The reader asked for their list;
  a file that quietly held only the 25 rows they happened to be looking at would be wrong in a way
  nobody notices until they open it.
- **No JavaScript file ships with the package.** A download started inside a Blazor Server circuit is
  not a link click — the bytes are on the server and the reader is at the end of a WebSocket — so the
  browser's own built-ins are driven through `IJSRuntime`: a `Blob` is built, an object URL minted, a
  synthetic anchor clicked, and the URL revoked. The packaging guard forbids a `wwwroot` because a
  stylesheet riding along would compete with the host's own; it is not a ban on interop, and the
  sample host already drives `history.replaceState` this way.
- **`ExportListAsync` carries a default body**, like `GetKildePropertyMetadataAsync` and for the same
  reader: a host that implements the contract rather than consuming `MuninExplorerClient` would
  otherwise stop building on the upgrade, and a version already on the feed cannot be taken back from
  whoever restored it. It refuses rather than answering emptily — an empty file is a worse answer
  than a clear no.
- **A refusal from the browser is said out loud.** A Content-Security-Policy without `blob:` would
  land in the catch, and the reader is told, rather than left with a button that appears to do
  nothing. A host whose Content-Security-Policy omits `blob:` will see that message.
