category: Fixed
- **A failure inside the explorer now says what it was in the host's log.** Twenty-nine of the
  thirty `catch (Exception)` sites in the package caught the exception and threw it away — the
  thirtieth rethrows — and every one of them now records it through `ILogger<T>` before it writes
  the sentence the reader sees, as do the sixteen branches beside them that handle a 429 or a 401.
  `LogError` for a failure, `LogWarning` where the outcome is expected, and the exception as the
  first argument so the stack survives. Nothing on screen changed, and nothing changed about when
  it is shown. What did change is that a fault on a host's own server was previously diagnosable
  only by elimination: the kildeutforsker rendering "Kunne ikke laste kilder nå" while the
  variabelutforsker beside it worked took an afternoon across two repositories, the CMS, the
  cluster and the live API, and never reached an answer. The message templates carry a kilde,
  variable or list id, a page number and the like — never a URL, a token, a response body, or
  anything the reader typed, and never the component's own name, which the log category already is.
  A cancelled call writes nothing: the hierarchy view cancels its own fetch on every new kilde, so
  logging above that guard would have made ordinary clicking produce Error entries for a kilde that
  loaded fine. And what the components log through is wrapped, so a host whose sink throws loses a
  log line rather than the page — `Logger<T>` rethrows a provider's failure, and these calls sit
  inside the catches that exist to keep the circuit up. (Fhi.Metadata-l9l2n.47)
