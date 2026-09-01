// The API the accessibility scan reads, served from the contract-drift fixtures.
//
// runa.munin.skytest.fhi.no is geo-filtered: Norwegian traffic is admitted and a GitHub runner is
// not (#127). A scan pointed there renders an empty shell, and axe reports no violations in a page
// with nothing on it — which is what this gate did until Fhi.Metadata-wr31i.
//
// The fixtures are reused rather than copied: one set for a human to re-capture when a drift
// report asks for it, instead of a second set here that nothing would ever look at again.
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
  [/^\/api\/explorer\/my\/lists$/, 'my-lists.json'],
];

// Read at startup: a renamed fixture should stop the stub here, where the message is about a
// missing file, rather than per request as a 404 the page renders as an empty panel.
const bodies = new Map();
for (const [pattern, source] of routes) {
  bodies.set(pattern, source.startsWith('[') ? source : readFileSync(join(fixtures, source), 'utf8'));
}

const server = createServer((request, response) => {
  const path = new URL(request.url, 'http://localhost').pathname;
  const route = routes.find(([pattern]) => pattern.test(path));

  if (route === undefined) {
    // Loud, because a route nobody serves renders as an empty panel that axe is happy with.
    console.error(`stub: no fixture for ${request.method} ${path}`);
    response.writeHead(404, { 'content-type': 'application/json' }).end('null');
    return;
  }

  response.writeHead(200, { 'content-type': 'application/json' }).end(bodies.get(route[0]));
});

server.listen(port, '127.0.0.1', () => console.log(`stub: serving the Testdata fixtures on ${port}`));
