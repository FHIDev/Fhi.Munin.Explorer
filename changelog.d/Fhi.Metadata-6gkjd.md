category: Notes for hosts

- **The README now lists every `munin-explorer*` class name the package emits, name by name.** The
  eight `VariableView` writes — `munin-explorer-whole` with its `__header`, `__code`,
  `__description`, `__body`, `__main`, `__aside` and `__list` — had been in no document here at all,
  and the hand-written counts beside the other views had all drifted, so the counts are gone and an
  inventory table with a kind per name replaces them.
  (Fhi.Metadata-6gkjd)
- **`scripts/assert-class-names-listed.sh` keeps that list honest.** It reconciles the whole prefix
  against the README on every CI run, in both directions, where the older check could only ask about
  names new on a branch. (Fhi.Metadata-6gkjd)
