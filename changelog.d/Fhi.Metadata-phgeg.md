category: Fixed
- A call that cannot reach Munin now gives up in about five seconds instead of up to a hundred.
  The client had no timeout of its own, so it inherited `HttpClient`'s hundred-second default, and
  an unreachable host is a connect the OS retries for roughly twenty-one seconds per address —
  measured at 12 and 33 seconds against a dropped network, under a spinner, with nothing the reader
  could press. `ConnectTimeout` is now five seconds and the whole request is bounded at thirty,
  which is far above any healthy search: the live catalogue answers in well under a second.
- The connect limit is set only where it exists. `SocketsHttpHandler` is unsupported on `browser`,
  so a WebAssembly host keeps the plain handler and is bounded by the thirty-second request timeout
  alone — fetch decides its own connect there and gives us no say. Getting that wrong is a build
  error rather than a host that fails to start, which is how it was caught.
