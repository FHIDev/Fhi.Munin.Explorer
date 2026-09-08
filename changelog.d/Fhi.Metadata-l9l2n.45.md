category: Fixed

- **A saved list's "Sist endret" no longer moves when variables are added or removed.** Munin
  moves `updatedAt` on a rename only, so the stamp the component showed after an add or a remove
  jumped back to the day of the last rename on the next refresh. The variable count beside it
  still moves on both. (Fhi.Metadata-l9l2n.45)
