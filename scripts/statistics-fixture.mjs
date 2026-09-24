// Opt-in statistics for the row drawer's Data tab: no captured variable carries any, so without
// these the coverage line, the bars and the figures are never drawn in a browser (Fhi.Metadata-9mxmw).
// A reserved search term renames the first row and points it at one of these ids, so every other
// scan keeps its captured payload.
export const STATISTICS_SEARCH = 'browser-statistics';
export const SUPPRESSED_SEARCH = 'browser-statistics-suppressed';
export const STATISTICS_NAME = 'Matallergi diagnostisert av lege';
export const SUPPRESSED_NAME = 'Matallergi med skjermet statistikk';

// Sixty characters, and a word no wider than the 320px list: it has to wrap beside its count.
export const LONG_LABEL = 'Ja, diagnostisert etter utredning i spesialisthelsetjenesten';

const id = n => `ffffffff-0000-4000-8000-${String(n).padStart(12, '0')}`;

export const variables = new Map([
  [STATISTICS_SEARCH, { id: id(1), name: STATISTICS_NAME, suppressed: false }],
  [SUPPRESSED_SEARCH, { id: id(2), name: SUPPRESSED_NAME, suppressed: true }],
]);

const frequency = (localId, term, count) => ({
  code: `2.16.578.1.12.4.1.1.449.${localId}`,
  preferredTerm: term,
  beskrivelse: null,
  additionalProperties: { KodeverkLokalID: localId, GyldigeTilfeller: count },
});

/** The statistikker array for one of the ids above, or null for any other variable. */
export function statistics(variableId) {
  const variable = [...variables.values()].find(one => one.id === variableId);
  if (variable === undefined) {
    return null;
  }

  // Munin's marker replaces the figures, so the suppressed statistic carries none to replace.
  const figures = variable.suppressed ? {} : { MIN: '0', MED: '1', MAX: '3', AVG: '1.2' };

  return [{
    id: id(10),
    code: 'MATALLERGI',
    preferredTerm: variable.name,
    additionalProperties: {
      SisteOppdaterteAarssett: '2026',
      GyldigeTilfeller: '15578',
      ManglendeTilfeller: '3461',
      ...figures,
    },
    kodefrekvenser: [
      frequency('0', 'Ja', '6826'),
      frequency('1', 'Nei', '5911'),
      frequency('2', LONG_LABEL, '2711'),
      frequency('3', 'Vet ikke', '130'),
    ],
    avsloringskontroll: variable.suppressed ? 'deskriptivStatistikkIkkeGitt' : null,
  }];
}
