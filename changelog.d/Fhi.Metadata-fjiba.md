category: Added

- **The list view can rename and delete a list.** The holder patches its own copy, so the new name
  is on screen without a round trip, and a deletion is confirmed first because the API offers no
  undo. (Fhi.Metadata-fjiba)
- **Deleting the list on screen leaves another one active, or none.** The active list used to go on
  pointing at the deleted one, so the view asked for the variables of a list the API no longer has
  and drew an empty table for a list that is gone.
- **Neither takes the circuit down when the API refuses.** A throttled rename or delete says the
  reader has asked too often; any other failure says to try again. Both stay inside the handler.
