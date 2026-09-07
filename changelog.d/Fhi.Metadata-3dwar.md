category: Notes for hosts

- **The sample stylesheets are a stand-in for `Fhi.Helsedata.Stiler`, and 178 of their declarations
  do not match it.** A host that copies `samples/*/host.css` as a starting point gets those
  differences with it. They are now listed, one per line, in `test/sample-css-known-divergences.txt`
  — the missing row-collapse block below 1280px, the eleven `munin-explorer-meta` table rules, the
  absent base `.munin-explorer` rule, and the rest — so the list can be read before the stylesheet
  is trusted. A new guard compares the two files' declarations against the published package on
  every pull request, so the count can only go down from here. Hosts that link Stiler itself are
  unaffected; this is about what the samples claim to reproduce. (`Fhi.Metadata-3dwar`)
