// Opt-in browser data for the kilde detail hierarchy: shapes hierarchy.json, one captured kilde,
// does not contain. Served only for the ids below, so no captured kilde answers differently.
export const HIERARCHY_KILDE = 'dddddddd-0000-4000-8000-000000000001';
export const EMPTY_KILDE = 'dddddddd-0000-4000-8000-000000000002';
export const FAILING_KILDE = 'dddddddd-0000-4000-8000-000000000003';

// The stub puts this id on variable.json's kilde, so "Vis datakilde" opens the tree and the
// captured kilde the kildeutforsker lists first is left as it was.
export const VARIABLE_KILDE = 'dddddddd-0000-4000-8000-000000000004';

// One unbroken word as long as the catalogue's longest code, which is what decides reflow width.
export const LONG_WORD = 'V_LMR.VARE_ADMINISTRASJONSVEI_BESKRIVELSE';

export const names = {
  kilde: 'Hierarkiprøven',
  main: 'Hovedstudie',
  followUp: 'Oppfølging',
  deepest: 'Tredje delkildenivå',
  baseline: 'Grunnlinje',
  wave: 'Oppfølging 2020',
  uncategorised: 'Samling uten kategori',
  direct: 'Direkte samling under kilden, med et navn som er langt nok til å måtte brytes over flere linjer',
  shared: 'Delt gruppe',
  sharedChild: 'Undergruppe i delt gruppe',
  sharedGrandchild: 'Dypeste gruppe',
  unassigned: 'Gruppe uten datasamling',
  empty: 'Gruppe uten variabler',
  long: LONG_WORD,
};

// Top-level rows and their counts, in the order the view draws them: presentationOrder first.
export const topLevel = [[names.main, 300], [names.direct, 25]];

// Every placement of the one group that occurs under three owners.
export const SHARED_PLACEMENTS = 3;

// The datakategorier of names.wave, in the order the row draws and says them.
export const WAVE_CATEGORIES = ['RPDG', 'HGPD', 'ehds-cat:other'];

const id = n => `dddddddd-0000-4000-8000-${String(n).padStart(12, '0')}`;

const group = (n, name, count, children = [], order = null) =>
  ({ id: id(n), name, variableCount: count, childVariabelgrupper: children, presentationOrder: order });

// The same id wherever it hangs, as the API sends a group that belongs to several datasamlinger.
const shared = () => group(100, names.shared, 12, [
  group(101, names.sharedChild, 8, [group(102, names.sharedGrandchild, 3)]),
]);

const datasamling = (n, name, count, categories, groups, order = null) =>
  ({ id: id(n), name, variableCount: count, variabelgrupper: groups, presentationOrder: order, categories });

export function hierarchy(kildeId, empty = false) {
  return {
    kildeId,
    kildeName: names.kilde,
    totalVariableCount: empty ? 0 : 325,
    historicalVariableCount: 0,
    delkilder: empty ? [] : [{
      id: id(10),
      name: names.main,
      variableCount: 300,
      presentationOrder: 1,
      datasamlinger: [
        datasamling(20, names.baseline, 140, ['RPDG'], [shared(), group(103, LONG_WORD, 5)], 1),
      ],
      unassignedVariabelgrupper: [group(104, names.unassigned, 4), group(105, names.empty, 0)],
      children: [{
        id: id(11),
        name: names.followUp,
        variableCount: 120,
        presentationOrder: 2,
        datasamlinger: [datasamling(21, names.wave, 80, WAVE_CATEGORIES, [shared()])],
        unassignedVariabelgrupper: [],
        children: [{
          id: id(12),
          name: names.deepest,
          variableCount: 40,
          presentationOrder: null,
          datasamlinger: [datasamling(22, names.uncategorised, 40, [], [shared()])],
          unassignedVariabelgrupper: [],
          children: [],
        }],
      }],
    }],
    directDatasamlinger: empty ? [] : [datasamling(23, names.direct, 25, ['PHDR'], [], 2)],
  };
}

// The detail payload KildeView opens with, taken from a captured kilde so every field it reads is
// real. Its name and codes are replaced; its prose is still the capture's.
export function detail(captured, kildeId) {
  return { ...captured, id: kildeId, code: 'K_HP', preferredTerm: names.kilde, kortNavn: 'HP' };
}
