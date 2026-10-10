# Listing Search

A small full-stack search over property listings ingested from several MLS feeds.
**ASP.NET Core 8 (C#)** API · **React + TypeScript** UI · in-memory data · built around **domain-driven design**.

| | |
|---|---|
| Backend tests | 199 (xUnit) - domain, application, infrastructure, HTTP end-to-end, startup/failure modes, architecture |
| Frontend tests | 55 (Vitest + Testing Library) |
| Data | `sample_listings.json` (12 listings) shipped in `backend/src/ListingSearch.Api/Data/` |

## Run it

Prerequisites: .NET 8 SDK, Node 20+.

```bash
# terminal 1 - API on http://localhost:5080
cd backend
dotnet run --project src/ListingSearch.Api --launch-profile http

# terminal 2 - UI on http://localhost:5173 (proxies /api to the API, so no CORS setup)
cd frontend
npm install
npm run dev
```

Tests:

```bash
cd backend  && dotnet test
cd frontend && npm test          # also: npm run typecheck, npm run build
```

## API

`GET /api/listings/search` - all parameters optional:

| Parameter | Meaning |
|---|---|
| `minPrice`, `maxPrice` | inclusive price band |
| `minBedrooms` | at least this many bedrooms (0 allowed) |
| `city` | exact city, case- and whitespace-insensitive |
| `keyword` | every word must start a word in the description (`pet` finds "Pets allowed", not "carpet") |
| `targetBudget` | does **not** filter - only influences ranking |
| `page` (default 1), `pageSize` (default 10, max 100) | pagination |

Response: `{ items[], page, pageSize, totalCount, totalPages, hasNext, hasPrevious }`; each item has the listing fields plus
`relevanceScore` (0-100). `GET /api/cities` lists the cities that can be filtered on.

## Architecture (DDD)

One bounded context - *listing search* - in four layers. Source-code dependencies point inward only:

```
   Api  ───────────►  Application  ───────►  Domain
    │                      ▲                    ▲
    └────►  Infrastructure ┴────────────────────┘
```

| Layer | Project | Holds |
|---|---|---|
| **Domain** | `ListingSearch.Domain` (no references at all) | The model and its rules. See below. |
| **Application** | `ListingSearch.Application` | Use cases: `SearchListingsHandler`, `GetCitiesHandler`. Orchestration only - no business rules. Defines request/response DTOs and the failures a caller can see (`InvalidSearchRequestException`, `UnknownCityException`). |
| **Infrastructure** | `ListingSearch.Infrastructure` | `InMemoryListingRepository` (implements the domain's `IListingRepository`) and `JsonListingFeed`, an anti-corruption layer from the MLS JSON format to the domain. |
| **Api** | `ListingSearch.Api` | The HTTP adapter and composition root: query-string parsing, RFC 7807 error mapping, DI wiring. |

The dependency rule is enforced, not just intended: the project references make violations uncompilable, and
`LayerDependencyTests` additionally asserts the Domain references no other layer and no ASP.NET / `Microsoft.Extensions` / JSON library.

**Domain model** (`backend/src/ListingSearch.Domain`)

- **Aggregate root `Listing`** - identity is `ListingKey(source, id)`, because a feed's `id` is only unique *within* that feed.
  Invariants (positive price, valid coordinates, non-negative bedrooms...) are enforced in the constructor.
- **Value objects** - `Address`, `CityName` (compares case/whitespace-insensitively, fixing the "minor inconsistencies" between feeds),
  `Price`, `GeoPoint`, `ListingDescription` (tokenised once), `PriceRange`, `SearchTerms`, `TargetBudget`, `PageRequest`, `Relevance`.
- **Specifications** - each filter is one tiny `IListingSpecification`; `ListingCriteria` composes them.
- **Domain services** - `RelevanceScorer` (the formula) and `ListingRanker` (total ordering). Both pure; "today" is passed in.
- **Repository port** - `IListingRepository` is declared in the domain and implemented in infrastructure.
- **Paging** - `Page<T>` is a domain concept so its boundary rules are tested without HTTP.

Two error styles, chosen deliberately: *user search input* is expected to be wrong sometimes, so value-object factories return
`Result<T>` and collect **every** problem; *entity invariants* (a corrupt feed row) throw `DomainException`.

The React app mirrors the layering: `domain/` (types, filter validation, page-window maths - pure), `infrastructure/` (the fetch client and
its `ListingApi` port), `application/` (`useListingSearch`, which cancels superseded requests), `ui/` (components).

## Scoring

Relevance is 0-100, rounded to 2 decimals **once**, so the number shown is the number sorted on and ties are genuine.

```
score = 100 · (0.6 · budgetFit + 0.4 · recency) / (0.6 + 0.4)

budgetFit = max(0, 1 − penalty)
            penalty = |price − target| / target            if under target
            penalty = 2 · (price − target) / target        if over target
recency   = 0.5 ^ (ageInDays / 30)                         ← half-life of 30 days
```

*Why these shapes*

- **Budget fit is linear in the relative gap**, so $20k off matters more on a $200k home than on a $2M one. It hits 0 at a 100% gap
  (50% when over budget) and never goes negative.
- **Going over budget costs double.** A buyer can usually spend less than their target, but not more; a $550k listing therefore ranks below
  a $450k one for a $500k target.
- **Recency decays exponentially**: new listings are strongly preferred, but a month-old one keeps half its value rather than falling off a cliff.
- **Weights 60/40**: price fit decides most of the order; recency breaks near-ties and demotes stale inventory.
  All four numbers (`BudgetWeight`, `RecencyWeight`, `RecencyHalfLifeDays`, `OverBudgetPenalty`) are in the `Scoring` section of `appsettings.json`
  and validated at startup by `ScoringPolicy`.
- **No `targetBudget`** → there is nothing to fit, so the score is recency alone (newest first).
- A listing dated in the future counts as brand new (age is clamped at 0).

Worked example, `targetBudget=450000`, evaluated on 2026-10-09: listing A1 ($450,000, listed 41 days ago) →
`0.6·1.0 + 0.4·0.388 = 0.755` → **75.51**; listing A5 ($470,000, 35 days) → `0.6·0.911 + 0.4·0.445 = 0.725` → **72.48**.

**Tie-break** (so paging is stable and deterministic): higher score → newer `listedDate` → lower price → `source` → `id`.

## Handling bad input

Each case has a defined outcome - nothing crashes, nothing returns a quietly wrong answer.

| Input | Response |
|---|---|
| `minPrice` > `maxPrice` | `400`, error on `minPrice` quoting both numbers |
| `pageSize` ≤ 0 or > 100, `page` < 1 | `400` naming the field - **rejected, never silently clamped** |
| negative price / bedrooms, `targetBudget` ≤ 0 | `400` per field |
| `minPrice=abc`, `minBedrooms=2.5`, `1e5` | `400`: "must be a number / whole number (got 'abc')" |
| keyword with no letters/digits (`!!!`), over-long values | `400` (a keyword that matches everything would be wrong data) |
| unknown or repeated parameter (`minBedroom=3`) | `400` - a typo must not silently return unfiltered results |
| several problems at once | all reported together in one `errors` map |
| **city not in any feed** (`Sprngfield`) | `404` + `suggestions: ["Springfield"]` |
| **known city, filters exclude everything** | `200`, `totalCount: 0` - a legitimate empty result |
| `page` past the last page | `400`: "page 3 is out of range: there are 2 pages" (page 1 of an empty result is valid) |
| unexpected failure | `500` problem response; details logged, not leaked |
| feed row with a missing field / unknown status / duplicate key | row skipped and logged, rest of the feed loads. A feed that is not a JSON array fails startup. |

The distinction between the two "no matches" cases is intentional: an unknown city is almost always a typo, and an empty page would hide it.
Errors are `application/problem+json` with an `errors` map keyed by parameter. Malformed *syntax* (unparseable numbers, unknown parameters) is
reported first, then *rule* violations (min > max) - there is nothing meaningful to check on a value that did not parse.

The UI validates locally first (same grammar as the server), shows server errors beside the offending field, and has explicit loading,
no-results and error states (with clickable "did you mean" city suggestions).

## Trade-offs and known gaps

- **Duplicate listings across feeds are not merged.** The sample data contains the same property from both feeds (A1/B7, A2/B8, A3/B9, A5/B11),
  and both are returned. The brief does not ask for de-duplication and "the same place" is a fuzzy judgement (`123 Main St, Apt 4B` vs
  `123 Main Street, Unit 4B`; prices differ slightly; descriptions disagree), so I left identity as `(source, id)`. It would slot in between
  the repository and the ranker as a domain service (key: normalised address + coordinates), or in the feed ACL.
- **Keyword search is literal.** It knows nothing about negation, so `pets` matches "no pets"; word-*start* matching (not substring, not stemming) is
  a cheap middle ground. A pet-policy filter would be a new specification parsed from the description.
- **`status` is returned but not filtered**, since the brief lists no status filter - pending and sold listings appear (with a badge).
  Adding it is one specification.
- **The repository takes criteria but is in-memory**: it filters a list. For a real database the same `ListingCriteria` would be translated to a query; ranking
  would stay in the domain, or move to the store if result sets get large (ranking then paging requires seeing every match).
- **Scores depend on today's date** (recency). The clock is an injected `TimeProvider`, which is how tests pin it.
- **City matching is exact** (after normalisation), not fuzzy; fuzziness is used only to produce suggestions after a miss.
- **`zip` mismatches** between feeds (22150 vs 22151 for the same Oak Ave listing) are kept as published; nothing filters on zip.
- No auth, caching or persistence - out of scope for the exercise.

## Extending it

| Requirement | Where it goes |
|---|---|
| New filter (status, baths, sqft, pets, radius from lat/long) | one `IListingSpecification` + one line in `ListingCriteria.Create` + one parameter in `SearchQueryParser` |
| Different ranking | a new scorer, or new numbers in `Scoring` config; `ListingRanker` is unchanged |
| De-duplicate across feeds | a domain service between `FindAsync` and `Rank` |
| Another feed / a database | implement `IListingRepository` (or a new feed ACL beside `JsonListingFeed`); nothing inside the domain changes |

## Layout

```
backend/
  src/ListingSearch.Domain/          Listings/ Search/ Ranking/ Common/
  src/ListingSearch.Application/     Searching/ Cities/
  src/ListingSearch.Infrastructure/  Feeds/ Persistence/
  src/ListingSearch.Api/             Http/ Composition/ Data/sample_listings.json
  tests/ListingSearch.Tests/         Domain/ Application/ Infrastructure/ Api/ Architecture/
frontend/
  src/domain  src/infrastructure  src/application  src/ui
```
