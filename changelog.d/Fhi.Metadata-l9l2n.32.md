category: Fixed

- **Creating a list can no longer take the circuit down.** The call was made without a guard, so
  anything it threw — a 429 from the rate limiter, an API that had gone away — left the
  event handler and took the Blazor circuit with it. The reader got a blank page and a reconnect
  banner in place of the list they were building. It now says what happened and stays where it is:
  a throttled attempt says too many requests, anything else says the save failed.
  (Fhi.Metadata-l9l2n.32)
- **Switching to the newly created list is guarded too**, the same way choosing one from the picker
  already was. The list exists on the server either way; only the switch to it is lost, which is
  what the message says.
- **The alert answers for the action the reader just took.** Four conditions share that one region,
  and a load that had failed earlier outranked all of them — a failed save read as "kunne ikke hente
  listen". Starting an action now clears the other three; a load that fails again says so again.
