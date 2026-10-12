category: Fixed
- **The variable page shows each datasamling's data period, not the day the variable was registered in it.**
  The dates after every name under "Datasamlinger" were the membership's `validFrom`/`validTo`, so
  every row read alike ("14. mai 2026 – Pågående"). They are now `dataFrom`/`dataTo` — the period
  the variable has data for in that datasamling — under a caption that says so. The periods appear
  once the Munin API fills those two members on every entry (FHIDev/Munin#6599); against an API
  that does not, the rows carry no dates and no caption rather than the registration dates. No new
  class name and no host change. (Fhi.Metadata-9lenf)
