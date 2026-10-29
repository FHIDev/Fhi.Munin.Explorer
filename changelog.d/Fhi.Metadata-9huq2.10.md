category: Fixed
- **A code for a saved list stops working when the list is deleted.** «Del liste» now asks Munin's
  `POST api/explorer/my/lists/{id}/share`, which builds the snapshot from the stored list (without
  notes or «Ønskede data») and ties the code to it, instead of posting the items to the anonymous
  `api/explorer/lists/share`. The caption under the code says so: «Koden slutter å virke hvis du sletter
  listen, og senest etter 90 dager». The view no longer reads the whole list before sharing it. Needs a
  Munin with Fhi.Metadata-9huq2.6 (Munin #6956).
