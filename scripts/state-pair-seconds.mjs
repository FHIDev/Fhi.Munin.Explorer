// Median CI seconds per (state, assertion) pair, from the shard logs. state-scan.mjs packs the shards by them
// (Fhi.Metadata-56lxp); a pair missing here costs DEFAULT_SECONDS, about what the cheapest take.
// Most of the time is PRESS_SETTLE_MS waits, once per width and press; remeasure when one moves.
export const DEFAULT_SECONDS = 5;

export const pairKey = (state, name) => `${state}: ${name}`;
export const pairCost = (state, name) => pairSeconds[pairKey(state, name)] ?? DEFAULT_SECONDS;

export const pairSeconds = {
  'utforsker-list: from 480px opening a list menu leaves every button in its row where it was and as wide': 40,
  'variables-list: from 480px opening a list menu leaves every button in its row where it was and as wide': 40,
  'kilder-list: from 480px opening a list menu leaves every button in its row where it was and as wide': 27,
  'kilde-hierarchy-collapsed: Tab and Shift+Tab never leave the focused control wholly under the stuck name bar': 26,
  'utforsker-list: on a phone a press beside an open sheet reaches nothing behind it, and Escape closes it': 23,
  'variables-list: on a phone a press beside an open sheet reaches nothing behind it, and Escape closes it': 23,
  'utforsker-list: on a desktop an open list menu lies over no row of the list, and Escape closes it from the keyboard': 21,
  'variables-list: on a desktop an open list menu lies over no row of the list, and Escape closes it from the keyboard': 21,
  'variables-list: a native dataperiode field accepts 1940 and recovers from a reversed interval': 17,
  'kilde-hierarchy-collapsed: a contents-nav press carries the sticky fact bar with it, both ways': 16,
  'kilder-list: on a phone a press beside an open sheet reaches nothing behind it, and Escape closes it': 15,
  'variables-list: an incomplete native dataperiode preserves the applied bounds and announces the error': 15,
  'kilder-list: on a desktop an open list menu lies over no row of the list, and Escape closes it from the keyboard': 13,
  'utforsker-list: on a phone every list menu and column picker opens as a named full-width bottom sheet': 13,
  'variables-list: on a phone every list menu and column picker opens as a named full-width bottom sheet': 13,
  'kilde-hierarchy-collapsed: a hashchange alone brings the sticky fact bar back to what the hero row says': 12,
  'kilde-hierarchy-collapsed: one jump back to the top puts the sticky fact bar away again': 12,
  'variables-list: a facet press made mid-fetch is applied after it and its checkbox agrees with the filter in force': 14,
  'kilde-hierarchy-collapsed: one jump straight past the hero row still shows the sticky fact bar': 11,
  'kilde-hierarchy-collapsed: the sticky fact bar stays away until the hero row has left the viewport upwards': 11,
  'variable-whole: a contents jump leaves its heading below a wrapped sticky bar': 11,
  "explorer-tabs: on a phone Tab and Shift+Tab stay inside an open sheet, the saved list's inside its tab panel too": 10,
  'kilder-list: on a phone every list menu and column picker opens as a named full-width bottom sheet': 10,
  'variable-whole: the sticky fact bar stays away until the hero row has left the viewport upwards': 10,
  'variable-datasamling: nested criteria fragment clears a fixed host header without a contents entry': 8,
  'variable-datasamling: the compact collection action remains focused until focus leaves the returning hero': 8,
  'variables-list: a refused column toggle leaves the picker agreeing with the columns drawn': 8,
  'variables-list: on a touch phone a tap beside an open sheet closes it and presses nothing behind it': 8,
  'variables-list: Arrow, Home and End move focus between the rows of an open «Last ned utvalg»': 7,
  'variables-list: a press outside «Last ned utvalg» closes it, and Tabbing out of it does not': 7,
  'kilde-hierarchy-collapsed: a detail page loaded on a phone starts with its contents list folded': 6,
  'kilde-hierarchy-collapsed: exactly one contents-nav entry is current, and it follows the reader to the last': 6,
  "kilder-list: Back from a kilde row's variables link reopens the row where the reader left it": 6,
  'kildeutforsker-list: Back from a kilde opened from the list returns to the list, and Forward opens it again': 6,
  'tree-populated: Felles gruppe: one tree selection ticks only its own placement and produces one chip': 6,
  'tree-populated: Kun i treet: one tree selection ticks only its own placement and produces one chip': 6,
  'tree-populated: a tree selection half-ticks the levels above it and nothing else (Fhi.Metadata-cjezd)': 6,
  'tree-populated: clearing a group from its standalone clears every tree placement': 6,
  'utforsker-list: Back from a variable opened from the list returns to the list, and Forward opens it again': 6,
  'variable-whole: exactly one contents-nav entry is current, and it follows the reader to the last': 6,
};
