category: Fixed

- **Saving a variable when the API answers 401/403 now tells the reader to sign in, not to try
  again.** A host that declares `IsAuthenticated` true while its token provider sends nothing the
  API accepts used to draw enabled save buttons that failed with "try again shortly" — advice
  that could never work, because the failure was never one a retry could fix. The my/lists write
  and read methods on `IMuninExplorerClient` now throw the new `MuninExplorerUnauthorizedException`
  for a 401/403 instead of the general `HttpRequestException`; a host with its own implementation
  of the interface should throw it too. (Fhi.Metadata-h5o3o)
