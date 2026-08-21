category: Changed

- **`docs/running-locally.md` now gets you through a cold start of helsedata's suite.** It still
  told you to reference the two projects the package merge removed, and it said nothing about the
  `UseLocalStiler` switch, the dashboard's one-time token, recovering the SA password from the
  existing SQL container, or the podman VM being asleep — each of which stops the run with an error
  that points somewhere else.
