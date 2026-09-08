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
  anything the reader typed. (Fhi.Metadata-l9l2n.47)
