category: Changed
- **The package's Microsoft dependencies move from 10.0.11 to 10.0.12.**
  `Microsoft.AspNetCore.Components.Web`, `Microsoft.Extensions.Configuration.Abstractions`,
  `Microsoft.Extensions.Http`, `Microsoft.Extensions.Logging` and
  `Microsoft.Extensions.Logging.Abstractions` are all servicing patches with no API change, so
  nothing a host compiles against moves. What does move is the floor: a host that pins any of
  the five at 10.0.11 itself now gets NU1605 on restore, and has to raise its own pin to 10.0.12
  or drop it. The two logging references travel with the others rather than separately, because
  `Microsoft.Extensions.Http` 10.0.12 requires them at 10.0.12. (Fhi.Metadata-aos2h)
