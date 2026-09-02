# Contract drift

The API this package reads lives in another repository. This is what stops that from being a
problem you find out about from a visitor.

---

## The gap it closes

`Fhi.Munin.Explorer.Contracts` is a hand-written description of what `/api/explorer/*` returns.
Munin owns the endpoints; we own the description. Nothing connects the two.

In one repository they would be the same pull request: change the endpoint, break the DTO, watch
CI go red before anyone merges. Split across two, a rename lands on a Tuesday and the first sign
of it is a heading with nothing in it on helsedata.no. Deserialisation is what makes it silent —
`System.Text.Json` ignores a field it does not recognise and leaves the property at its default,
so a renamed `preferredTerm` does not throw, it renders as `""`.

Two tests answer two different halves of that:

| | Runs | Reads | Answers |
| --- | --- | --- | --- |
| `ContractCoverageTest` | every commit | `Testdata/*.json` | Do the contracts still match the payloads we last captured? |
| `ContractDriftTest` | nightly | the live API | Are those payloads still what is being served? |

The first is fast and offline and can never notice a change made in Munin. The second is the one
that can, which is why it is scheduled rather than triggered — the change that breaks us is not
one of ours, so there is nothing here to trigger on.

## What it checks

One representative response from every endpoint the component calls, fetched through the real
`IMuninExplorerClient` — the URLs and the query strings are part of what can drift, so a test that
spelled them out itself would keep passing after the client stopped working. Ids come from the API
too: the kilde with the most delkilder, a variable from the first page of a search. A hard-coded
id is a kilde somebody can unpublish.

Each response is round-tripped — deserialised into the DTO, serialised straight back — and the two
documents are compared by shape rather than by value. Values are today's data; keys are the
contract. Both directions are reported, because the two failures are different and both are quiet:

- **A field in the response with nothing to hold it.** The API sends something the contracts drop.
  This is also what a rename looks like from our side.
- **A field in the contract the API did not send.** The DTO's own default is being rendered as
  though it were data — an empty string in a heading is indistinguishable from a variable with no
  name.

The second is only reported when the round trip wrote something there. Null, an empty array and an
empty object are the three ways a contract says *nothing here*, and where it said that there is
nothing to tell apart — an omitted field and a field sent as null, or as `[]`, produce exactly the
same DTO. `filters.json` is the standing example: captured before the datatype facet gained
`displayName`, it carries none, and that is not drift.

That costs one thing, and it is worth knowing rather than discovering: a collection the API stops
sending altogether reads exactly like one it sends empty, so a withdrawn `delkilder` would pass.
The alternative — reporting every field this package models ahead of the API it is pointed at, and
every field at once on the day somebody turns on "omit nulls" server side — is a job that is red
for reasons nobody can act on, which fails sooner and more quietly than this does.
`ShapeDriftTest` has a test named after the blind spot, so it is a decision rather than an oversight.

## Running it yourself

It skips itself unless you say otherwise, so `dotnet test` never leaves the machine:

```bash
MUNIN_EXPLORER_LIVE=1 dotnet test --filter Category=ContractDrift
```

Against another environment:

```bash
MUNIN_EXPLORER_LIVE=1 MuninExplorer__ApiBaseUrl=https://localhost:7134 \
  dotnet test --filter Category=ContractDrift
```

The default is `https://runa.munin.skytest.fhi.no` — public, anonymous, read-only. There is no secret
to hold and no token provider to register.

One arm is the exception, and it is skipped rather than failed when it cannot run. The `my/lists`
routes are behind the API's signed-in explorer policy, so the "Ønskede data" annotation — the one
field in this package a reader writes rather than reads — cannot be round-tripped anonymously.
Give it an explorer access token, raw and without the `Bearer` prefix, and it writes, edits,
over-runs the cap and clears one annotation on a list it creates and deletes again:

```bash
MUNIN_EXPLORER_LIVE=1 MUNIN_EXPLORER_TOKEN=eyJhbGci... \
  dotnet test --filter Category=ContractDrift
```

Without the token that arm says so in the run output. That is the honest state of the nightly job:
the read half of the contract is checked against the API every night, and the written half is
checked by whoever runs it with a token in hand.

## When the nightly job goes red

`.github/workflows/contract-drift.yml` runs at 04:17 UTC and opens an issue when it fails, or
comments on the one already open. The failure message names each difference and the path it sits
at. Then:

1. Update the DTO under `src/Fhi.Munin.Explorer.Contracts`.
2. Re-capture the matching file under `test/Fhi.Munin.Explorer.Tests/Testdata/` — for example
   `curl https://runa.munin.skytest.fhi.no/api/explorer/filters > filters.json` — so the offline test is
   reading the same payload the live one saw.
3. Write a changelog fragment. A contract change is one every host sees.
4. Raise a bead in the Munin workspace with `--label=helsedata --label=rcl`.

A red build here is not necessarily our bug. It is the API telling us something changed, on the
day it changed, which is the whole point.

## When it goes red without reaching the API

A red run has two causes, and they want opposite responses. Either a payload no longer fits the
contracts, or nothing answered and nothing was compared.

The second says nothing at all about the contracts, so reporting it as drift is worse than saying
nothing: it names a cause that does not exist and sends whoever picks it up to edit DTOs that were
correct. That is not hypothetical — the job spent three nights filing "the Explorer API no longer
matches Fhi.Munin.Explorer.Contracts" while every one of its nine tests was dying on a TCP connect
timeout, because the base URL named the Munin test API by a host that resolves to a private
address. It answers by hand from inside FHI's network and not at all from a hosted runner, which
is the shape of mistake that survives being checked (Fhi.Metadata-ghxh4).

So the two are separated end to end. `LiveApiConnection` catches the transport failure and fails
with `LiveApi.UnreachableMarker` and the base URL rather than a list of differences;
`scripts/drift-failure-kind.sh` reads the results file for that marker; and the workflow titles its
issue from the answer, so an outage and a real difference never land in the same thread. Where
nothing was recorded at all the answer is `drift`, because that is the case where somebody has to
read the log and the drift report is the one that says so.

Both halves are covered offline in `ShapeDriftTest`, including the connect-timeout form — an
unroutable address surfaces as `TaskCanceledException` rather than `HttpRequestException`, and
catching only the obvious one would have left the real case reported as drift. A third test pins
the marker to the spelling the script greps for, since nothing else connects a C# constant to a
line of bash.

## Why the job cannot quietly stop working

The tests gate themselves on `MUNIN_EXPLORER_LIVE`, and that gate is also the way the check could
fail silently: rename the variable, mistype the filter, move the trait, and the job runs zero
tests, reports success, and goes on reporting success for as long as nobody looks. A green square
would then mean "nothing was looked at" while reading as "the contracts are fine".

So the job does not get to decide it passed. `scripts/assert-drift-ran.sh` reads the test results
afterwards and fails unless every expected test actually executed. If a drift test is deliberately
removed, the count passed to that script has to come down with it — a decision somebody makes,
rather than a number that quietly goes down.

`ShapeDriftTest` covers the other half of the same worry, on every commit and offline: captured
payloads broken on purpose — a field added, a field renamed, a field withdrawn — with the
untouched payload as the control. The last of those drives the whole nightly path against a stub,
so "a deliberate mismatch fails the build" is something the build proves rather than something
this document claims.
