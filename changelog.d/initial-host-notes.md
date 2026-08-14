category: Notes for hosts
- The component ships **no CSS**. Styling comes from the host — on helsedata.no that is
  `Fhi.Helsedata.Stiler`. Class names are prefixed `variabelutforsker-`.
- The component sets no render mode. The host decides at the mount site — `render-mode="Server"`
  on the `<component>` tag helper in a legacy Blazor Server host, or `@rendermode` in a modern
  Blazor Web App.
- A host mounting it must contain at least one `.razor` file (an `_Imports.razor` is enough), or
  the Blazor framework script is not served to the project and the circuit never starts.
