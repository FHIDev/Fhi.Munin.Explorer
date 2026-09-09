category: Changed
- **The package's Microsoft dependencies move from 10.0.11 to 10.0.12.** They are servicing
  patches with no API change, so nothing a host compiles against moves. What does move is the
  floor, and for more than the five references the package names directly: every Microsoft
  10.0.x package it pulls resolves at 10.0.12 too, all 21 of them, transitives included —
  `Microsoft.JSInterop`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Primitives`,
  `Microsoft.Extensions.DependencyInjection` and `Microsoft.AspNetCore.Components` among them,
  all plausible things for a Blazor host to pin. A host that pins any of the 21 at 10.0.11
  itself gets NU1605 on restore — an error, not a warning, since the .NET SDK raises it — and
  has to move its own pin to 10.0.12 or drop it. (Fhi.Metadata-aos2h)
