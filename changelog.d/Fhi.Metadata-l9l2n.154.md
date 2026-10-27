category: Changed
- **The package's Microsoft dependencies move to 10.0.14.** `Microsoft.AspNetCore.Components.Web`,
  `Microsoft.Extensions.Configuration.Abstractions`, `Microsoft.Extensions.Logging` and
  `Microsoft.Extensions.Logging.Abstractions` go from 10.0.13, and `Microsoft.Extensions.Http` from
  10.0.12. They are servicing patches with no API change, so nothing a host compiles against moves.
  What moves is the floor, transitives included: a host that pins any Microsoft 10.0.x package this
  one pulls below 10.0.14 gets NU1605 on restore, and has to move its own pin or drop it.
  (Fhi.Metadata-l9l2n.154)
