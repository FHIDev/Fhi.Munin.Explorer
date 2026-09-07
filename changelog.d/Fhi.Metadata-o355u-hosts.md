category: Notes for hosts

- **A null `int`, `bool`, `Guid` or enum still fails the whole call, on purpose.** Unlike a name or
  a list, those have no value that means "nothing" — a kilde reported as having `0` datasamlinger
  when it has fourteen is worse than the "could not load" the component draws instead. Munin backs
  that up: each is a primary key, a `NOT NULL` column or a `Count()` aggregate, so a null in one is
  a broken payload rather than a shape the API can send. (Fhi.Metadata-o355u)
