// The API the accessibility scan reads, served from the contract-drift fixtures.
//
// runa.munin.skytest.fhi.no is geo-filtered: Norwegian traffic is admitted and a GitHub runner is
// not (#127). A scan pointed there renders an empty shell, and axe reports no violations in a page
// with nothing on it — which is what this gate did until Fhi.Metadata-wr31i.
//
// The fixtures are reused rather than copied: one set for a human to re-capture when a drift
// report asks for it, instead of a second set here that nothing would ever look at again.
//
// They are not one snapshot: filters.json reports 46037 variables where variables.json holds
// 18289, and its facet counts are that catalogue's. Nothing here reads a count back out, and
// re-capturing the corpus together is its own job.
//
// Usage:  node scripts/axe-stub-api.mjs <port>
import { createServer } from 'node:http';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const port = Number(process.argv[2]);
const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const fixtures = join(root, 'test', 'Fhi.Munin.Explorer.Tests', 'Testdata');

if (!Number.isInteger(port) || port <= 0) {
  console.error('usage: node axe-stub-api.mjs <port>');
  process.exit(2);
}

// One entry per route the explorer calls, longest first so `variables/{id}` cannot swallow
// `variables/{id}/timeline`. The literal is the one route with no fixture, and it answers with
// what the client would have fallen back to anyway: an empty vocabulary.
const routes = [
  [/^\/api\/explorer\/kilder\/b37d66c3-d2a4-4e58-b13e-12f96d38e9f3$/, 'kilde-med-delkilder.json'],
  [/^\/api\/explorer\/variables\/[^/]+\/kodeverk\/[^/]+\/[^/]+\/codes$/, 'kodeverk-codes.json'],
  [/^\/api\/explorer\/variables\/[^/]+\/timeline$/, 'timeline.json'],
  [/^\/api\/explorer\/variables\/[^/]+$/, 'variable.json'],
  [/^\/api\/explorer\/variables$/, 'variables.json'],
  [/^\/api\/explorer\/filters$/, 'filters.json'],
  [/^\/api\/explorer\/kilder\/egenskaper$/, '[]'],
  [/^\/api\/explorer\/kilder\/[^/]+\/hierarchy$/, 'hierarchy.json'],
  [/^\/api\/explorer\/kilder\/[^/]+$/, 'kilde.json'],
  [/^\/api\/explorer\/kilder$/, 'kilder.json'],
  [/^\/api\/explorer\/datasamling\/[^/]+$/, 'datasamling.json'],
  [/^\/api\/explorer\/my\/lists\/[^/]+\/variables$/, 'my-list-variables.json'],
  [/^\/api\/explorer\/my\/lists$/, 'my-lists.json'],
];

// Read at startup: a renamed fixture should stop the stub here, where the message is about a
// missing file, rather than per request as a 404 the page renders as an empty panel.
const bodies = new Map();
for (const [pattern, source] of routes) {
  bodies.set(pattern, source.startsWith('[') ? source : readFileSync(join(fixtures, source), 'utf8'));
}

// The hierarchy capture belongs to Tromsø, which the short list capture omits. Make that source
// reachable without relabelling another source's hierarchy or changing the captured JSON files.
const study = JSON.parse(readFileSync(join(fixtures, 'kilde-med-delkilder.json'), 'utf8'));
const listRoute = routes.find(([, source]) => source === 'kilder.json')[0];
const countCollections = node => node.datasamlinger.length +
  (node.delkilder ?? node.children ?? []).reduce((count, child) => count + countCollections(child), 0);
bodies.set(listRoute, JSON.stringify([
  ...JSON.parse(bodies.get(listRoute)),
  { ...study, navn: study.preferredTerm, aktiv: true, harVariabelbeskrivelse: study.totalVariables > 0,
    datasamlingCount: countCollections(study), delkildeCount: study.delkilder.length },
]));

// No kilde in the captured catalogue carries biobank, the one kildetype the Kilde facet draws a
// badge for, so a verbatim payload renders that markup nowhere and a scan reports no violations in
// what is not there - the finding this whole stub exists for. One row is retyped, its facet with it.
const filtersRoute = routes.find(([, source]) => source === 'filters.json')?.[0];
const filters = filtersRoute === undefined ? undefined : JSON.parse(bodies.get(filtersRoute));
const badged = new Map([['MS', ['biobank', 'Biobank']]]);

// The same re-capture the check below is about can drop the fixture outright or rename the arrays
// in it. Said here, or it arrives as a TypeError out of a stub whose caller only sees axe time out.
if (!Array.isArray(filters?.kilder) || !Array.isArray(filters?.kildeTyper)) {
  console.error('stub: filters.json is missing, or carries neither kilder nor kildeTyper');
  process.exit(2);
}

const retyped = filters.kilder.filter(one => badged.has(one.kortNavn));

// Loud here rather than as a state timing out later: a re-capture that drops or renames one of
// these would leave the badge off every page, which is the silence the note above is about.
if (retyped.length !== badged.size) {
  console.error(`stub: filters.json holds ${retyped.length} of the ${badged.size} kilder the badge states need`);
  process.exit(2);
}

for (const kilde of retyped) {
  const was = filters.kildeTyper.find(type => type.value === kilde.kildeType);
  if (was !== undefined) {
    was.count -= kilde.count;
  }
  const [value, displayName] = badged.get(kilde.kortNavn);
  kilde.kildeType = value;

  // Added to the facet the value already has rather than beside it: a re-capture where the
  // catalogue has grown one would otherwise put two headings for one kildetype in the panel.
  const facet = filters.kildeTyper.find(type => type.value === value);
  if (facet === undefined) {
    filters.kildeTyper.push({ value, displayName, count: kilde.count });
  } else {
    facet.count += kilde.count;
  }
}

// The symmetric case to the merge above: a kildetype whose only kilde was retyped away would stay
// as a heading with no rows under it, a shape the real API never serves and the scan would measure.
filters.kildeTyper = filters.kildeTyper.filter(type => type.count > 0);
filters.kildeTyper.sort((a, b) => a.displayName.localeCompare(b.displayName, 'nb'));
bodies.set(filtersRoute, JSON.stringify(filters));

// The one route whose fixture cannot be served verbatim. my-list-variables.json is a real capture:
// 247 entries reported, two of them kept. Served as-is for every page, it says "page 1 of 3" every
// time, and VariableListState walks every page of the active list — so the walk never advances and
// the circuit asks forever (measured at ~7000 requests a second). The counts are made to agree with
// the entries actually being served instead.
const listVariables = /^\/api\/explorer\/my\/lists\/[^/]+\/variables$/;

function pagedListVariables(body, query) {
  const page = Math.max(1, Number(query.get('page') ?? 1) || 1);
  const size = Math.max(1, Number(query.get('pageSize') ?? 100) || 100);
  const items = JSON.parse(body).items ?? [];
  const slice = items.slice((page - 1) * size, page * size);

  return JSON.stringify({
    items: slice,
    totalCount: items.length,
    page,
    size,
    totalPages: Math.max(1, Math.ceil(items.length / size)),
  });
}

// A fetch held open for a while, one request per hold asked for and none unless asked. It is how
// scripts/state-scan.mjs stages a press the component DROPS - `if (_loading) return` - which no
// browser can stage on its own: the request is the HOST's, made over the circuit, so Playwright's
// route interception never sees it and cannot slow it down.
//   POST /__stub/hold-next?path=/api/explorer/variables&ms=6000
//   GET  /__stub/hold-next  ->  {"held":[{"path":...,"ms":...}],"holding":[...]}
//   DELETE /__stub/hold-next  ->  drops every unspent hold AND answers every one already spending
// Read back rather than assumed spent: a hold nothing ever asked for would leave a press that was
// never in flight looking exactly like one that was. `held` stays unspent-only for that reason;
// one already in flight has left it, and is in `inFlight` until its timer fires or DELETE takes it.
const held = [];
const inFlight = new Set();

// A staging that armed a 6000ms hold, triggered it and threw would otherwise leave its client
// waiting out the remainder with nothing able to recall it, and the timer alive across teardown.
function answerNow(one) {
  clearTimeout(one.timer);
  inFlight.delete(one);
  one.spend();
}

function control(url, request, response) {
  if (request.method === 'POST') {
    const path = url.searchParams.get('path');
    const ms = Number(url.searchParams.get('ms'));
    if (path === null || !path.startsWith('/') || !Number.isInteger(ms) || ms <= 0) {
      response.writeHead(400, { 'content-type': 'application/json' }).end('{"error":"path,ms"}');
      return;
    }
    held.push({ path, ms });
  }

  if (request.method === 'DELETE') {
    held.length = 0;
    for (const one of [...inFlight]) {
      answerNow(one);
    }
  }

  const holding = [...inFlight].map(({ path, ms }) => ({ path, ms }));
  response.writeHead(200, { 'content-type': 'application/json' })
    .end(JSON.stringify({ held, holding }));
}

function serve(url, request, response) {
  const path = url.pathname;
  const route = routes.find(([pattern]) => pattern.test(path));

  if (route === undefined) {
    // Loud, because a route nobody serves renders as an empty panel that axe is happy with.
    console.error(`stub: no fixture for ${request.method} ${path}`);
    response.writeHead(404, { 'content-type': 'application/json' }).end('null');
    return;
  }

  const body = listVariables.test(path)
    ? pagedListVariables(bodies.get(route[0]), url.searchParams)
    : bodies.get(route[0]);

  response.writeHead(200, { 'content-type': 'application/json' }).end(body);
}

const server = createServer((request, response) => {
  const url = new URL(request.url, 'http://localhost');

  if (url.pathname === '/__stub/hold-next') {
    control(url, request, response);
    return;
  }

  const holding = held.findIndex(one => one.path === url.pathname);
  if (holding < 0) {
    serve(url, request, response);
    return;
  }

  const [{ path, ms }] = held.splice(holding, 1);
  console.error(`stub: holding ${request.method} ${url.pathname} for ${ms}ms, as asked`);
  const one = { path, ms, spend: () => serve(url, request, response) };
  inFlight.add(one);
  // unref'd, so a hold still counting down cannot hold this process open past its teardown. The
  // listening server is what keeps it alive meanwhile, and DELETE is what answers the client.
  one.timer = setTimeout(() => answerNow(one), ms);
  one.timer.unref();
});

server.listen(port, '127.0.0.1', () => console.log(`stub: serving the Testdata fixtures on ${port}`));
