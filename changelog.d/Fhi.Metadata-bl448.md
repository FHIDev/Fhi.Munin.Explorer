category: Added
- **A signed-in reader can redeem an account-linking code from inside the component.** The same
  person gets one `ExplorerUser` per ID-porten client, so lists saved through helsedata.no were
  invisible in Runa and the other way round. "Koble konto" in the header actions takes a code
  minted on the other login, shows what linking will do, and redeems it against
  `POST /api/explorer/my/link/redeem` with the bearer token the component already holds. Signing
  in starts nothing and navigating starts nothing — the component only ever *receives* a link,
  because it runs inside a CMS page that is not ours. Drawn only when `IsAuthenticated` is true.
  (Fhi.Metadata-bl448)
- **`IMuninExplorerClient.RedeemIdentityLinkAsync` and `IdentityLinkOutcome`.** Each refusal the
  API distinguishes — an unknown code, an expired one, a spent one, one presented by the login
  that minted it, and two logins already linked — comes back as its own `IdentityLinkOutcome`
  rather than as an exception, so the caller can say which of them happened in the reader's own
  language. A 429 still throws `MuninExplorerRateLimitedException`, as every other write does.
  The member carries a default implementation that throws `NotSupportedException`, so a host
  implementing the interface itself keeps building. (Fhi.Metadata-bl448)
