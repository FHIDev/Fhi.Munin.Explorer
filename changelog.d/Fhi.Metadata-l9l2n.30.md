category: Fixed

- **A throttled reader is told they asked too often, not that the catalogue is down** - the API
  answers 429 with a `Retry-After` when too many requests arrive from one address, and the client
  used to throw that as the same generic `HttpRequestException` as a 500 or a timeout. So a reader
  who hit the limit was advised to try again shortly, which is the one thing that cannot help. The
  client now raises `MuninExplorerRateLimitedException`, carrying the wait the API asked for in
  either form the header takes, and the result list, the facet panel, the kilde list, the kilde view
  and the row's save button each say so in their own place, in both languages. The reads and the
  writes both raise it: a save refused by the limiter used to read as "could not save", and a list
  the reader still has is not a list they have lost. The wait is carried for a host that
  logs it and never rendered: a countdown against a window shared with every other reader is a
  promise this package cannot keep. Nothing retries by itself — helsedata's cluster reaches Munin
  as one address, so components retrying on a shared `Retry-After` would rebuild the burst that
  caused the 429. A 429 is also deliberately not mapped to "no hits" the way a 404 is: a search
  that was never run must not come back as a search that found nothing. (Fhi.Metadata-l9l2n.30)
- **A host substituting its own `IMuninExplorerClient` has to throw it too** - every non-2xx used
  to reach the components as `HttpRequestException`, so an implementation that wrapped its own
  `HttpClient` needed nothing beyond `EnsureSuccessStatusCode`. A 429 is now its own type in
  `Fhi.Munin.Explorer.Contracts`, and the rule the components rely on is stated on
  `IMuninExplorerClient`: it must not come back as null, as an empty collection, as `false` from
  one of the writes, or as a retry of the implementation's own. Catching around the client changes
  the same way - `MuninExplorerRateLimitedException` does not derive from `HttpRequestException`,
  so a host that catches the latter to log or to swallow will no longer see a throttled call.
  (Fhi.Metadata-l9l2n.30)
- **A refused list read no longer leaves the save buttons permanently wrong, or takes the page
  down** - reading which variables are in the reader's list happens once when the component mounts,
  alongside the search and the facet refresh, which is the burst the limiter counts. That read
  escaping a Blazor lifecycle method tore down the circuit - in a legacy Blazor Server host, the
  whole page rather than this component. It is now caught, and the read is tried again on the
  reader's next save rather than abandoned for the life of the circuit, so that press puts every
  other row's label right as well. Without it, "wait and try again" repaired the save and nothing
  else. Only a press retries: rendering does not, because the component reads this on every
  parameter set, and a membership read alongside every search and page turn would rebuild the
  burst that earned the 429. The press itself is decided from the row as the reader saw it, so a
  variable already in the list - drawn as "save" because the read was refused - is added rather
  than deleted when the repair arrives mid-press, and a repair that is refused again no longer
  costs the reader the save they asked for. Overlapping asks now join the read already running
  instead of each sending their own, and a read publishes its pages only once it has walked them
  all, so a walk that is refused partway through leaves no half-read list behind.
  (Fhi.Metadata-l9l2n.30)
