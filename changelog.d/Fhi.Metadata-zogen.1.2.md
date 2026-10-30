category: Added
- **The datasamling page says where its inherited validity and identification level come from.** «Gyldig fra»,
  «Gyldig til» and «Grad av personidentifikasjon» carry the «Arvet fra kilden …» / «Arvet fra delkilden …» line when
  Munin says the delkilde or kilde supplied them, and none when the datasamling holds them itself.
  `DatasamlingDetail` gains `ValidFromSource`, `ValidToSource` and `PersonIdentificationLevelSource`, of the new
  `ValueSource`; each is null for a value the datasamling holds itself, and when the API or a cached response
  predates them. The «Gyldighet» row, which draws both ends, is captioned only when every end it shows came from the
  same place.
  (Fhi.Metadata-zogen.1.2)
