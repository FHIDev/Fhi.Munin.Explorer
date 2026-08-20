category: Added

- **Sorting and paging are now two-way**, joining search, filter and selection, so a host can mirror
  the whole view into its URL and restore it from one. The component never touches the address bar
  itself - the host owns the URL.
- **A shared link that outlived its result set lands on the last real page** instead of an empty one,
  and the URL corrects itself so the next person it is sent to gets a working link.
- **LegacyHost shows how**, in one small wrapper component helsedata can copy. (Fhi.Metadata-eujqw)
