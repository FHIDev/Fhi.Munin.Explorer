category: Fixed
- The retry offered beside a failure now stands next to the sentence it answers and is drawn as a
  button. It was a ghost-styled control on the line below a coloured infobox, which read as stray
  text under a box rather than as something to press. Both retries move — the search's and the
  filters' — so two failures reported at once do not look like two different kinds of thing.
- While that retry is running, the box says so rather than emptying. It used to clear its sentence
  the moment the fetch started and leave the button standing on its own, which reads as a control
  with nothing to answer. The box cannot leave — the button inside it would go out from under the
  focus of whoever pressed it — so its words change instead, and it carries `aria-busy` while they do.
