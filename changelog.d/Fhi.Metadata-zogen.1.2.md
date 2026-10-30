category: Added
- **The datasamling page says where its inherited validity and identification level come from.** Gyldig fra, gyldig
  til and grad av personidentifikasjon carry the «Arvet fra kilden …» / «Arvet fra delkilden …» line when Munin says
  the delkilde or kilde supplied them, and none when the datasamling holds them itself. `DatasamlingDetail` gains
  `ValidFromSource`, `ValidToSource` and `PersonIdentificationLevelSource`, of the new `ValueSource`; each is null
  for an own value, and against an API or a cached response from before Munin sent them. The Gyldighet row that draws
  both ends is captioned only when both came from the same place.
  (Fhi.Metadata-zogen.1.2)
