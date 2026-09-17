// Opt-in browser data for shapes a live catalogue capture cannot promise to contain.
// The reserved search terms keep existing scans on their captured payloads.
export const TREE_SEARCH = 'browser-tree-fixture';
export const EMPTY_SEARCH = 'browser-tree-empty';
export const LARGE_COUNT = 120;
export const names = {
  kilde: 'Prøvekilde', delkilde: 'Delkilde', first: 'Samling A', second: 'Samling B',
  direct: 'Direkte samling', offered: 'Felles gruppe', excluded: 'Kun i treet',
  unset: 'Uten filterverdi', unassigned: 'Uten datasamling', root: 'Direkte under kilde',
  container: 'Gruppestamme', child: 'Tilbudt undergruppe', large: 'Stort gruppetre',
};
const id = n => `eeeeeeee-0000-4000-8000-${String(n).padStart(12, '0')}`;
const owner = (delkildeId = null, datasamlingId = null) => ({ kildeId: id(1), delkildeId, datasamlingId });
const repeated = [owner(id(2), id(3)), owner(id(2), id(4))];
const group = (n, name, filter, owners, parentId = null, count = 1) =>
  ({ id: id(n), name, filter, owners, parentId, count, global: true });

export function treeFilters(empty = false) {
  const groups = [
    group(10, names.offered, '1', repeated),
    group(11, names.excluded, '2', repeated),
    group(12, names.unset, null, [owner(null, id(5))]),
    group(13, names.unassigned, '1', [owner(id(2))]),
    group(14, names.root, null, [owner()]),
    group(15, names.container, '2', [owner(id(2), id(3))], null, 0),
    group(16, names.child, '1', [owner(id(2), id(3))], id(15)),
    group(17, names.large, '2', [owner(null, id(5))], null, 0),
    ...Array.from({ length: LARGE_COUNT }, (_, i) =>
      group(100 + i, `Gruppe ${String(i + 1).padStart(3, '0')}`, '2', [owner(null, id(5))], id(17))),
  ];
  if (empty) groups.forEach(one => { one.count = 0; });
  const count = empty ? 0 : LARGE_COUNT + 7;
  return {
    kildeTyper: [{ value: 'annenDatakilde', displayName: 'Annen datakilde', count }],
    kilder: [{ id: id(1), name: names.kilde, kortNavn: '', kildeType: 'annenDatakilde', count }],
    delkilder: [{ id: id(2), name: names.delkilde, kildeId: id(1), parentDelkildeId: null, count }],
    datasamlinger: [
      { id: id(3), name: names.first, delkildeId: id(2), kildeId: id(1), count },
      { id: id(4), name: names.second, delkildeId: id(2), kildeId: id(1), count },
      { id: id(5), name: names.direct, delkildeId: null, kildeId: id(1), count },
    ],
    hierarkiVariabelgrupper: groups,
    variabelgrupper: groups.filter(one => one.filter !== '2' || one.name === names.container),
  };
}
