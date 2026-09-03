category: Fixed
- A variable whose payload names the same kodeverk twice no longer crashes the component when
  its kodeverk list is re-rendered. The two lines were given the same Blazor key, and diffing
  that list threw inside the renderer — which in a Blazor Server host takes down the page the
  component is embedded in, not just the component.
