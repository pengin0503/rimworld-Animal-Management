# Animal Management Review Remediation Implementation Plan
> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement task-by-task. Steps use checkbox (`- [ ]`). **This plan contains no implementation.**
**Goal:** Address issues #19–#26 without regressing guarded auto-production; profile and reduce significant overhead.
**Architecture:** Keep `FeedForecast` and `FeedScenario` as model sources. Separate safe ingredient-debit modeling, bill ownership transitions, planning display, scenario safety, and optional telemetry. Optimize the hot path only after correctness and baseline measurements.
**Tech Stack:** C#; .NET 10; RimWorld 1.7; GitHub Actions; public reference `Krafs.Rimworld.Ref`.
**Spec:** `docs/superpowers/specs/2026-11-02-review-remediation-design.md`

## Global constraints
- Original defaults Off/Confirm/Auto remain; no new Auto permission without opt-in and exact live proof. No destructive changes to player bills.
- Baseline main `f382c94e11303ccb350681706eb29215616b0abc`; do not rewrite existing major components without measured evidence.
- Public CI may run often; no game DLL/R2 secrets in public artifacts. `RimWorld-for-mod-test` is optional and not a dependency.
- All changed UI labels Japanese/English, save compatibility and 270+ MOD target.
- Issue #6 owns general in-game/Scribe/performance acceptance, not a reason to mark code fixes unimplemented.

## Review focus / required regressions
- [ ] Two ingredient ThingDefs share one nutrition Pool: convert debits equal both actual resource removals (#25).
- [ ] One ingredient ThingDef in two pools: no duplicate or missing debit under permitted ingredient choices (#25).
- [ ] One vanilla RepeatCount job finishes then another: continued ownership without revocation; manual edit revokes (#21).
- [ ] Zero Hay/Kibble stock and other-species shortage: displays a truthful plan, no unsupported auto writes (#19/#22).
- [ ] Partially inspected stock and changing winter/pasture: never turns uncertainty into an unsafe Auto approval (#20).

---
### Task 1: Remove unsafe pooled ingredient debit (#25) — gate all auto writes
**Files:** Modify `Source/ManagedFeedBills.cs`, optionally `Source/AnimalManagement.cs` and `Source/FeedActionPlanner.cs`; Tests `Tests/Program.cs`, `Tests.Game/Program.cs`.
**Interfaces:** Preserve `ManagedFeedBills.Preview(Map,FeedForecast,FeedAnchor,FeedActionPlan,Building_WorkTable)` and `Execute(...)` externally; internal debit composer has a testable pure data contract e.g. `bool TryBuildMaterialDebits(IReadOnlyList<(pool,nutrition)> actualRemoval, double[] poolAvailable, out Dictionary<int,double> debits)`.
- [ ] Add failing test: ingredient A+B in same Pool; expected Pool debit A+B, not last assignment.
- [ ] Add failing test: identical Def in multiple Pools with variable access, prove conservative balance or decline Auto (never assume ordered selection).
- [ ] Verify game's actual Bill ingredient choice and multi-ingredient/filter semantics from 1.7 API; default-deny when selection not locked/defended.
- [ ] Implement safe debit aggregation, pool budget checks, before/after human and animal shortage gate; no unproven stock transformation.
- [ ] Run targeted and full tests, public build and local review; commit with `Fixes #25` only when complete.

### Task 2: Correct controlled Bill ownership lifecycle (#21)
**Files:** Modify `Source/AnimalActionState.cs`, `Source/ManagedFeedBills.cs`, `Source/ManagedBillPolicy.cs`; Test `Tests/OwnershipTests.cs`, `Tests.Game/Program.cs`.
**Interfaces:** Keep persisted `ManagedFeedBill` backward-loadable; introduce explicit programmatic states and a checkable classification of normal progress vs manual intervention.
- [ ] Add failing tests: normal RepeatCount decrement, natural completion, manual edit/suspend, expiry, delete, permission revoke, Scribe reload.
- [ ] Confirm which fields change during vanilla bill work with RimWorld DLL. Do **not** treat any mutated field as proof of user modification.
- [ ] Define progress-insensitive owner fingerprint for settings that users control; track runtime progress separately. Suspended/edited bill releases without auto-restoration.
- [ ] Revoke only on explicit user override/conflict, not natural completion/expiry; next daily replenishment may create a new safe bill with same prior authorization.
- [ ] Run tests/build/review and commit with `Fixes #21`.

### Task 3: Display planning without an existing feed stack (#19, #24)
**Files:** Modify `Source/FeedPlanningUI.cs`, `Source/AnimalManagement.cs`, `Source/AnimalManagementUI.cs`, translations; Test `Tests/Program.cs` and UI/in-game acceptance notes.
**Interfaces:** Distinguish forecast advice candidates from `FeedAnchor` (existing reachable stock). Production always requires a verified concrete storage/bench/recipe context; capacity calculation does not.
- [ ] Failing tests for no Hay/Kibble stack, empty map, and ProductionMode=Off + capacity display.
- [ ] Build candidate advice from actual food Defs only when diet/utility can be grounded; otherwise show missing prerequisite, not false numeric certainty.
- [ ] Remove early return that hides `DrawCapacity`; separate display toggles from production permission; preserve old persisted settings with safe defaults.
- [ ] Test UI state and translation keys, build/commit `Fixes #19, Fixes #24`.

### Task 4: Correct independent herd capacity (#22)
**Files:** Modify `Source/HerdCapacityPlanner.cs`; Tests `Tests/HerdTests.cs`.
**Interfaces:** Preserve `Calculate(FeedScenario,bool[],int,int,int,int)` or adjust callers/tests together, documenting changed semantics.
- [ ] Failing test: unrelated hungry A species, fully fed B species; B sustainable capacity still calculated, A baseline deficit displayed.
- [ ] Failing test: shared feed with B growth harms A; reject that capacity increase.
- [ ] Separate selected-species deficit from other-species baseline deficit; enforce nonworsening across **each relevant non-target consumer** rather than just total deficit, including humans.
- [ ] Handle target zero-demand species and bounds clearly; run tests, commit `Fixes #22`.

### Task 5: Relax weather blocker only with bounded conservative proof (#20)
**Files:** Modify `Source/AnimalManagement.cs`, `Source/FeedActionPlanner.cs`, `Source/ManagedFeedBills.cs`, `Source/AnimalActionState.cs`; Tests `Tests/Program.cs`, `Tests/OwnershipTests.cs`.
**Interfaces:** A distinct status for incomplete observation vs scenario-dependent forecast; never make `Partial` or unknown accessibility pass.
- [ ] Failing test: incomplete scan stays blocked, pasture ordinary + guaranteed safe minimal production becomes eligible, winter uncertainty conflicting cases stay blocked.
- [ ] Model conservative worst-case growth/rot bounds and compare output need across cases; never assume production yields at first tick.
- [ ] Auto permitted only when ingredient availability, nutrition impact, demand and product output are safe for every considered scenario and exact initial state is revalidated.
- [ ] Run regression/CI and commit `Fixes #20` after #25 and #21 have passed.

### Task 6: Action history and denial audit (#26)
**Files:** Modify `Source/AnimalActionState.cs`, `Source/FeedPlanningUI.cs` / settings window, translations; Tests `Tests/OwnershipTests.cs`.
- [ ] Failing test: distinct create/completion/manual override/denial reasons are reported and bounded/deduplicated; no per-tick spam.
- [ ] Display existing history per-map, represent stop cause and permission; keep settings and save format safe.
- [ ] Validate strings/translation equality; run CI; commit `Fixes #26`.

### Task 7: Profile and optimize hotspot (#23)
**Files:** Modify only measured hotspots in `Source/AnimalManagement.cs` / `Source/FeedActionPlanner.cs`; tests `Tests/Program.cs`; update `docs/VALIDATION.md`.
- [ ] Measure baseline using `docs/review/2026-11-02-remediation-benchmark-matrix.md`. Label metrics by environment (pure-model vs game FPS/TPS).
- [ ] Add equivalence tests (multiple diets, expiry/access, partial state, 7/15/30 days); record baseline outputs.
- [ ] Use structural access key and a saved pool index to remove per-stack character strings and `IndexOf`; memoize only stable immutable inputs, never stale forecast.
- [ ] Measure 100/400 consumers, 1k/12k feed stacks, max-flow scenario with and without planning UI; retain optimization only if results match, document deltas (not invented TPS).
- [ ] Run CI and review then commit `Fixes #23` when acceptance met.

### Task 8: Integration and handoff
**Files:** Existing `.github/workflows/build.yml` (only if a new test cannot run there), `docs/VALIDATION.md`, README/RELEASE_NOTES as needed.
- [ ] All old/new tests, translation/package validator, Release build, game-fixture compile, public PR CI.
- [ ] Verify safety of Auto-mode, owned Bill lifecycle and Scribe; normal-game tests/270+MOD/TPS only if feasible, otherwise accurately update existing #6.
- [ ] PR review; merge when checked, close issues individually only after their criteria pass, avoid release tag unless requested.
- [ ] Provide SHA/PR, tested/unverified, performance measurements or `not measured`, and residual issues.

**CI commands (known from workflow):** `python3 tools/validate_package.py`; `dotnet run --project Tests/Tests.csproj -c Release`; `dotnet restore Source/AnimalManagement.csproj`; `dotnet build Source/AnimalManagement.csproj -c Release --no-restore`; `dotnet build Tests.Game/Tests.Game.csproj -c Release`; `python3 tools/package_dev.py`.
