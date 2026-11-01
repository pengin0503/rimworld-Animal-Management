# Feed competition implementation plan

**Goal:** Replace independent species stock ratios with shared-inventory daily forecasts and explicit uncertainty.
**Architecture:** Game adapters snapshot per-pawn diet/access and per-pen quadrum growth. A pure allocation model shares each feed pool once, reserves human consumption first, uses pasture before stock for animals, and reports 7/15/30-day shortages. Birth/death inputs are optional proportional adult-equivalent scenarios. No automation of animal management.
**Spec:** User request 2026-11-01 and Issue #1.

- [ ] RED: shared food, disjoint diets, human competition, seasonal zero growth, pasture/stock separation, population scenario, invalid nutrition, no animals.
- [ ] GREEN: adjacency-list flow allocator; daily inventory ledger; no stock reuse or carryover of unused pasture.
- [ ] Integrate bounded per-pawn accessibility, pen association, seasonal projections, bilingual scrollable UI and persisted scenario settings.
- [ ] Exact-DLL build, regression tests, model benchmark, package and translation checks; self-review.
- [ ] PR/CI/merge development checkpoint; retain gameplay/save-load/performance release blockers in #1.

Review focus: closed/forbidden food, pens sharing one enclosure, changes to pens/food policies, multiple maps, modded nutrition and unsupported access.
Ruling: proceed inline under the supplied scope without approval round or subagents. Conservative omission of unverified accessibility is labeled partial, never counted as available feed.

Checkpoint: pure allocator, per-consumer dates, seasonal adapters, bilingual UI, settings and packaging implemented. RED/GREEN observed; 16 assertions pass. Exact-DLL Release build has zero warnings/errors. Game acceptance and scan/TPS measurement remain release blockers. Self-review performed inline.
