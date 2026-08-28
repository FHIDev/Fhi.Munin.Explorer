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
- A read that fails because the connection under it had died is sent once more, on a fresh one.
  A pooled connection can be dead with nothing having said so — the network goes away, the sockets
  stay in the pool, and the next request is written into one and fails on the read after seventeen
  seconds of retransmission. No connect happens there, so no connect timeout shortens it, and
  .NET's own retry does not cover it: that one repeats a request the connection refused before it
  was sent. Only GET and HEAD, and only once — a reset during the response read says nothing about
  whether the server processed the request, so a save must not be repeated, and a second failure is
  the network being down rather than one stale connection.
- Connections are retired on a schedule this package chooses rather than on whichever of two
  mechanisms fired first. `PooledConnectionLifetime` is thirty seconds and the factory's handler
  rotation is off: supplying a primary handler without setting the first leaves DNS refresh to the
  factory discarding the handler every two minutes, which is the pairing the setting exists to
  replace rather than race.
