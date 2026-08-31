category: Notes for hosts
- **Shareable search links no longer need writing from scratch.** The parsing a host had to build
  to put explorer state in its own address bar is now `ExplorerUrlState.Parse` / `.ToQueryString`.
  What stays yours is what only you know: reading the incoming query server-side, the path the
  component is mounted on, where a sibling explorer lives, and the `history.replaceState` call.
  `ExplorerUrlState.QueryKeys` names the parameters we read, so you can tell them from your own —
  anything not in that list is left untouched.
  <br><br>
  Three things worth keeping if you write that glue: mount with `render-mode="Server"`, never
  `ServerPrerendered` (an `EventCallback` serialises to an empty delegate across a static-SSR
  boundary, so the URL silently stops following the view); build the path from `PathBase + Path`
  rather than `Path`, which is identical locally and wrong behind a reverse proxy; and use
  `replaceState` rather than `pushState`, or every filter change becomes a history entry the reader
  has to walk back through.
