category: Fixed

- **A payload carrying `"navn": null` — or a null on any other string the contracts declare
  non-nullable — no longer takes the host's Blazor circuit down.** The null used to pass
  deserialisation and throw at render time instead, past the try/catch around the fetch, so the
  component's own error message never appeared. Those properties now read an explicit null as
  `""`, exactly as they already read an absent key; properties declared `string?` are untouched.
  (Fhi.Metadata-o355u)
