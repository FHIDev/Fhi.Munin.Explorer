category: Fixed
- **«Sist oppdatert (kildesystem)» reads as a date on the kilde and datasamling pages.** The catalogue stores it as text in `yyyyMMdd`, and the detail views drew it raw (`20200302`); it now reads «2. mars 2020» / «2 March 2020», as the kilde list's column already did. A value that is not a real day is still shown as given.
