category: Fixed

- **Removing a variable from a list can no longer take the circuit down.** The call was made
  without a guard, so a 429 from the rate limiter left the event handler and took the Blazor
  circuit with it — a blank page and a reconnect banner in place of the row the reader wanted
  gone. A throttled removal now says the reader has asked too often, anything else says to try
  again, and the view stays where it is. (Fhi.Metadata-l9l2n.33)
- **A throttled switch to a newly created list names the cause.** It was guarded already, but
  every failure read as "kunne ikke hente listen"; a 429 now says so, the way the create half of
  the same handler does.
