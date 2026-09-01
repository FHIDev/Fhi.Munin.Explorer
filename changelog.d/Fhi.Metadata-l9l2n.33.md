category: Fixed

- **Taking a variable out of a list can no longer take the circuit down.** The call was made
  without a guard, so anything it threw — a 429 from the rate limiter, an API that had gone away —
  left the event handler and took the Blazor circuit with it: a blank page and a reconnect banner
  in place of the list the reader was pruning. It now says what happened and stays where it is, a
  throttled attempt saying too many requests and anything else saying the action failed. This was
  the last unguarded write in the list view. (Fhi.Metadata-l9l2n.33)
- **A removal the API refuses now says so.** Only a thrown removal was ever going to be reported;
  one the API declined returned `false` and was dropped in silence, leaving the row on screen and
  the press looking like it had not registered. It gets the same message as a failed rename.
