category: Notes for hosts

- **Wire `ExploreVariablesRequested` or you get no selection column.** The checkbox column, the
  count and both buttons are drawn only when that callback has a delegate, because the ticks exist
  to reach a page only you can name — a column over a button that leads nowhere would cost the
  reader the work of choosing before telling them there was nothing to choose for.
  (Fhi.Metadata-5ghur)
- **Create that callback inside an interactive component, not in a static parent.** An
  `EventCallback` does not survive being passed from a statically-rendered parent into an
  interactive island: Blazor rejects a bare delegate parameter, but `EventCallback` is a struct,
  so it is serialised as `{"HasDelegate":true}` and read back inside the circuit as empty. Putting
  `@rendermode` on the `KildeExplorer` tag does **not** fix this — that makes the mount point
  interactive while the parent creating the callback stays static. Mount the component inside a
  small wrapper component that the host renders interactively, and put the handler there; both
  sample hosts now do exactly that, and it is the arrangement helsedata's Optimizely host already
  uses. Get it wrong and the selection column is simply absent, with nothing to say why. The same
  applies to `SelectedKildeIdChanged` and to every `EventCallback` on `VariableExplorer`, where
  it has no visible symptom at all. (Fhi.Metadata-5ghur)
- **One class name to style if you are not on Stiler**: `munin-explorer-kilder__select`, on the
  checkbox column's header cell and on every row's. The declaration it needs is a **width** — a
  table shares itself out between its columns, so one holding a single checkbox otherwise takes
  the same share as Dataansvarlig and squeezes the eight columns that carry words. Both sample
  hosts' `host.css` carries `width: 1%` for it, right after the kilde list's count rule; a test in
  this repository asserts that the rule is a width and not merely a rule. The boxes themselves
  wear no class — a bare `<input type="checkbox">` is an element every stylesheet already dresses,
  the same call the facet panel makes. (Fhi.Metadata-5ghur)
