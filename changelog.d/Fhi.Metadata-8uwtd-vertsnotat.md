category: Notes for hosts

- **Two renames, one line each.** A host mounting `KildeExplorerWithUrlState` mounts
  `KildeExplorer` instead — same three parameters, same behaviour. A host that deliberately wanted
  the bare list under the old `KildeExplorer` name now names `KildeSearch`. A CMS field holding
  `Fhi.Munin.Explorer.Blazor.KildeExplorerWithUrlState` resolves to nothing after this release and
  renders no component: it is a string, so nothing fails to compile first.
  <br><br>
  **`KildeExplorer` must be mounted at an interactive render mode** — `render-mode="Server"`, never
  `ServerPrerendered`; `@rendermode` with `prerender: false` in a modern host. It owns `?kilde=`
  and throws on initialisation rather than drawing a page whose URL never follows the view.
  `KildeSearch` has no such requirement, and no query handling either: a link to it always lands on
  the list.
  <br><br>
  **`VariableExplorerPath` is unchanged and still optional.** It is the one thing only the host
  knows, so a CMS that mounts by type name and cannot pass it gets no selection column and no
  handover button — the same page that mount renders today. Set it from a host you write yourself
  to offer the handover; it is not defaulted to a guess, because a button leading to a page you may
  not have is worse than no button.
  <br><br>
  **No new or removed class names**, and no `Fhi.Helsedata.Stiler` rule changes with this: the
  markup is the same component under a different name.
