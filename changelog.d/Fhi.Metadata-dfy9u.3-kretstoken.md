category: Notes for hosts
- There is now a worked example of calling Munin as the signed-in user from a Blazor Server
  host, in `samples/LegacyHost/Authentication/`. It exists because the two obvious
  implementations are both wrong and both fail silently: `IHttpContextAccessor` is null during
  circuit activity, and the provider is a singleton so it cannot hold a user without eventually
  handing one person's token to another. The sample resolves the circuit per call instead, and
  the tests cover the two properties that are impossible to check by reading — concurrent
  circuits never see each other's token, and a circuit whose work throws does not leave its
  token behind.
