category: Fixed

- **A throttled reader is told they asked too often, not that the catalogue is down** - the API
  answers 429 with a `Retry-After` when too many requests arrive from one address, and the client
  used to throw that as the same generic `HttpRequestException` as a 500 or a timeout. So a reader
  who hit the limit was advised to try again shortly, which is the one thing that cannot help. The
  client now raises `MuninExplorerRateLimitedException`, carrying the wait the API asked for in
  either form the header takes, and the result list, the facet-fed panels, the kilde list and the
  kilde view each say so in their own place, in both languages. The wait is carried for a host that
  logs it and never rendered: a countdown against a window shared with every other reader is a
  promise this package cannot keep. Nothing retries by itself — helsedata's cluster reaches Munin
  as one address, so components retrying on a shared `Retry-After` would rebuild the burst that
  caused the 429. A 429 is also deliberately not mapped to "no hits" the way a 404 is: a search
  that was never run must not come back as a search that found nothing. (Fhi.Metadata-l9l2n.30)
