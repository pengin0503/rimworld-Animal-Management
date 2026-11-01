# Validation / 検証記録

## Verified / 確認済み

- **Game build:** RimWorld 1.7.4985 rev1269, Linux x86_64 WaylithClr (private game snapshot).
- **Source commit tested:** `b253272dafbdb3dcb8e748646bd08064925dc9cc`.
- **Public-reference build:** `dotnet build` with .NET SDK 10 and `Krafs.Rimworld.Ref 1.7.4985-unstable` succeeded (0 warnings and 0 errors).
- **Real game-code load (A):** `status=passed`, `exitCode=0`, `reasonCodes=[]` for AnimalManagement.
- **Evidence:** https://github.com/pengin0503/RimWorld-for-mod-test/actions/runs/41275665138
- **Method:** isolated A-level comparison via `RimWorld-for-mod-test`. Original game content stayed in a private R2-backed validation snapshot; no game DLL was published.

## Not yet verified / 未確認

- This host **does not run the normal playable RimWorld application**; no in-game UI interaction, time-dependent job behavior or save/reload verification was performed.
- Seasonal 7/15/30-day pen forecasts, feed sharing/accessibility, breeding demand and gameplay/save-load verification.
- **A=passed does not mean that the mod is a completed, gameplay-verified release.** Avoid claiming v1.0 readiness until the remaining items are independently validated.

日本語: Linux版のゲーム本体（1.7.4985 rev1269）でAレベルのMOD読み込み検証に合格しました。通常のゲーム起動・画面操作・セーブとロード・長時間プレイでの動作は未検証です。完成版として扱わないでください。


## 2026-11-01 shared-inventory implementation (development)

- Linux x86_64, SDK 10.0.111 / .NET 10.0.11. Release build against the exact private 1.7.4985 rev1269 DLLs: 0 warnings/errors.
- `dotnet run --project Tests/Tests.csproj -c Release`: 16 assertions pass. Shared-pool conservation, immutable inputs, disjoint diets, unreachable/forbidden-style excluded links, human priority, winter zero growth, separation of pasture/stock, no pasture carryover, birth/death scaling, empty maps, absent population baseline, nonfinite values, flow rerouting and independent shortage dates are covered.
- RED was observed for the absent forecast engine and missing per-consumer dates. The absent population baseline test then failed behaviorally; adding the explicit rejection returned the whole suite to green.
- One Release model benchmark, 100 animals with one shared pool for 30 days: 7.66 ms. This excludes actual map scans, pen calculator, food-policy evaluation, pathfinding and other MODs. It is not measured TPS overhead or an optimization comparison.
- Package metadata, 29 used translation keys with English/Japanese parity and a complete five-file development mod ZIP pass.
- API lookup reused the audited 1.7.4985 type/member/chunk/IL indexes. FoodUtility.WillEat's indexed calls include the current respected FoodPolicy; per-pen quadrum growth and connected-region APIs were verified separately from runtime behavior.

### Model assumptions and limits

Each individual contributes a diet/access mask. A shared stock pool is allocated once; human stock demand is served before animals, then pasture precedes stock. Flow can reroute within a feeding stage to avoid artificial specialist starvation. These are explicit scenario assumptions, not a reproduction of RimWorld's feeding AI.

Seasonal pen growth uses vanilla quadrum estimates and seasonal freezing-temperature zeroing. Regions shared by multiple pen markers are canonicalized so the same pen's growth is not added twice. Existing plants are excluded from stocks and unused daily pasture is not banked. Animals without a validated pen receive no pasture credit. Birth/death scenarios are proportional adult equivalents, not a reproductive simulation.

Scans run every 10,000 game ticks with a 12,000-object / 2,000-access-check limit. Unverified feed is not counted and the result is labeled incomplete. More than 400 consumers aborts the snapshot transaction; the UI flags prior data as stale. Each map owns independent state; changing settings flags the snapshot stale until recalculation. Error logs are suppressed after the first consecutive failure.

### Not verified / 正式版までの未確認項目

- At the initial checkpoint, new A/map/tick checks had not run. The final evidence below updates those scopes; real feeding, UI, full-map save/load, mid-save addition/removal, migration/pen changes and 270+ MOD acceptance remain unverified.
- Full WaylithClr host execution is still required. The initial direct Scribe attempt failed on missing game dependencies/uninitialized prefs; final primitive settings integration below now passes. It is not a whole-map save/load test.
- Spoilage, weather events, handler feeding, food digestion modifiers, food processing, maturation, inventories/containers and off-pen grazing are outside the current model. Seasonal growth/access/competition adapters need real-game acceptance; model tests alone do not prove no optimistic estimate.
- Actual large-map scan time, allocations and TPS impact remain unmeasured. `ScanMilliseconds` exposes an end-to-end measurement when running in-game; no value is invented here.
- Self-review only, no subagents. Fixed missing population baseline, per-consumer shortage dates, duplicate pen growth and transactional snapshot failures. Issue #1 remains open. No v1.0 release.

日本語: 共有在庫を一度だけ割り当てる予測、食性・アクセスの確認、放牧地の季節別成長、7/15/30日の不足と放牧地別内訳、任意の出生死亡シナリオ、前提・不確実性表示を追加しました。実物DLL参照ビルドと16項目のモデル検証・日英キー・開発版パッケージは合格しました。ただし実マップ、給餌、保存と読み込み、途中導入・削除、270以上のMOD、実スキャン時間・TPSは未検証です。腐敗や異常気象等は未モデル化で、表示する餌割当も実AIとは異なり得ます。正式版にはしません。

## Final private-runtime evidence (2026-11-01)

- [Pinned private-host run](https://github.com/pengin0503/RimWorld-for-mod-test/actions/runs/41278778403) completed successfully. Only safe rebuilt summaries were printed; raw game files, saves and private logs were not published.
- Fresh qualified **A=passed**, exit 0, no reason codes, for the source revision in [runtime-smoke.json](evidence/2026-11-01-runtime-smoke.json).
- Generated-map finite-tick smoke: actual game tick and independent registered `fixture.counter` both equal **0, 60, 600, 12,000**. **D stage=completed**, but **overall=incomplete** with `experimental`, `dNotQualified`, `mapNotQualified`. The host CI success means these limited expectations were met; it is not promotion to qualified D or game feature acceptance. No new errors/warnings were reported by that comparison.
- The registered observer measures its fixture counter, not our work diagnosis, feed accuracy, apparel effects or scan timing. The fixture map and Core/Japanese conditions do not represent a populated or 270+ MOD colony. No TPS claim is made.
- Local model benchmark conditions: Linux x86_64, AMD EPYC 9V74, 9 logical CPUs exposed, SDK 10.0.111 / .NET 10.0.11, Release, single-run measurements with game API/GUI/pathfinding excluded. No before/after optimization claim.

### Primitive Scribe integration

`dotnet run --project Tests.Game/Tests.Game.csproj -c Release -p:RimWorldManaged=<matching-private-game-dir> -p:GameExtraManaged=<matching-private-extra-dir>` exercises the real game Scribe under stock .NET for isolated primitive data. The extra directory contains only matching R2 ObjectPool and Steamworks managed dependencies. The fixture supplies the game's real default `PrefsData` because normal engine startup is absent. These files are test inputs only and are never committed or packaged.

**PASS:** WarnDays, IncludeForbidden, PopulationScenario, Births and Deaths settings round trip. Forecast snapshots are recomputed rather than serialized. This does not verify map transitions or actual feeding. Static index comparison: 86 member signatures and 60 types matched; 170 external-framework references remain outside the indexed assembly set, with no indexed game-reference mismatch.

**Still blocked for v1.0:** normal populated gameplay, full-map save/load and installation/removal, actual task/feeding/wear acceptance, real outfit-manager/race/DLC interactions, 270+ MOD compatibility and actual large-map scan/TPS measurement. Earlier implementation gaps and scenario limitations remain; Issue #1 is open and no v1.0 release is created.

日本語: 最新コミットで実ゲーム読み込みAは合格し、実ゲームTickと独立fixtureが12,000 Tickまで一致しました。ただしD・マップ生成は未適格化で総合判定はincompleteです。実物Scribeによる限定的なデータ往復も合格しましたが、通常プレイやマップ全体の保存・読み込み、実際の業務動作、270以上のMOD環境・実TPSの検証にはなりません。Issue #1と正式版の保留を維持します。

## Implementation continuation: feed lifetime and usable nutrition

Stock pools now retain a conservative last-fresh day, with current rot progress projected across seasonal outdoor temperatures or maintained current indoor temperature. Food that spoils during a modeled day is excluded from that day; leftovers are expired once, not re-counted as consumed. Stocks with different lifetimes are not combined. Eater-specific FoodUtility nutrition is evaluated for verified consumers; the minimum is used for a shared pool, avoiding optimistic genotype/raw-food conversion. Pasture access also checks food policy.

Added modeled whole covered days (30-day horizon), spoilage loss, current natural pen production and daily net balance. The 30-day climate vector is calculated once per snapshot. Existing forecast assertions plus one expiry regression pass; exact-DLL Release build and translation checks pass. No broad new test suite or populated-game acceptance was run. Delivery, weather events and changed temperature control remain explicit invalidation conditions rather than automatic animal-management actions. Historical runtime evidence does not cover new production bytes.

## v1.0.0 release checks (2026-11-01)

Fixes made during release review (code review only; no new gameplay run):

- The stock-only warning and shortage day previously included human demand. A colonist food deficit could therefore raise an animal feed warning even when animal feed was sufficient. `Projection.FirstAnimalShortageDay` now tracks animal demand only and drives the warning. The combined first deficit stays visible and labeled. Human competition is still allocated first, so an animal shortage caused by human consumption is still reported.
- Settings panel tolerates a missing map component.

Checks performed:

- 19 model assertions pass (two new).
- `validate_package.py` passes.
- Release build has 0 warnings and 0 errors against the exact private 1.7.4985 rev1269 DLLs.
- Release build with `Krafs.Rimworld.Ref 1.7.4985-unstable` also has 0 warnings and 0 errors.
- Static reference check of the public-reference DLL: 94 members and 63 types matched, 0 mismatches. The 175 unverified references are BCL only.

Release-review findings that are not blocking:

- Shared stocks are allocated once by max-flow.
- Unchecked access and budget overflow are not credited.
- Spoilage, freezing and the daily expiry of pasture growth are modeled.
- Maps are independent.

Still unverified, and not claimed: normal populated gameplay, full-map save/load, actual feeding behavior, large-map calculation time/TPS and compatibility with large mod lists. The earlier A-level and 12,000-tick evidence was produced on earlier DLL bytes.

## v1.0.1 (2026-11-01)

### Issues resolved

| Issue | Change |
|---|---|
| #7 | A definitive warning and the alert require a complete, current snapshot. A partial scan only shows a "possible shortage" line. |
| #8 | A pen that shares stored feed with other consumers labels its shortage date as allocation-dependent. |
| #9 | Prisoners and downed colonists are served from food any capable free colonist can reach. |
| #10 | Japanese and English About.xml descriptions carry the same scope and cautions. |
| #11 | Demand uses `FoodFallPerTickAssumingCategory(Fed)`. The current-category rate is 0.5x while hungry, 0.25x while urgently hungry and 0 while starving (decompiled `HungerLevelUtility.HungerMultiplier`). |
| #12 | Reachability is cached per (consumer, district) and `WillEat` per (consumer, def) for stacks without ingredients. Only cache misses count against the 2,000-check budget. |
| #13 | Plants standing in pens (`IsEdibleByPastureAnimals`, nutrition stat) are a non-replenishing pool for pen animals, capped at 30,000 pen cells. |
| #14 | Staged allocation. Humans use food no animal can use before shared food. Animals use daily growth, then standing plants, then stock. Babies are excluded while a lactating colonist is present. |
| #15 | Grazers outside pens are listed and excluded from the forecast instead of being charged to stock. |

Also added:
- a standard alert for a predicted animal feed shortage (complete snapshots only), which opens a non-modal forecast window;
- an alert on/off setting.

The warning threshold no longer marks the snapshot stale. The warning and alert use the forecast including pasture and standing plants. The stock-only figures are still shown.

### Found by the in-game probe and fixed

The stock scan walked `listerThings.AllThings` with a 12,000-entry cap. On a generated map, trees and grass consumed the cap before any stored food, and the result was partial with no stock counted. This was present since v1.0.0. The scan now walks `ThingRequestGroup.FoodSource` and skips plants before counting.

### Optimisation

- One simulation serves the 7/15/30-day horizons; shorter horizons are prefixes of the longest one.
- Together with the stock-only run, this is 2 simulations per refresh instead of 4.
- The access caches above remove repeated `CanReach`/`WillEat` evaluations.

### Checks performed

- 25 model assertions pass, including staged allocation with cross-stage rerouting (review follow-up) and multi-horizon equivalence.
- `validate_package.py` passes (45 keys).
- Release build has 0 warnings and 0 errors against the exact private 1.7.4985 rev1269 DLLs, and also against `Krafs.Rimworld.Ref`.
- Static reference check: 136 members and 86 types matched, 0 mismatches.
- Real game, Linux WaylithClr 1.7.4985 rev1269, private host. The probe was private and not published.
  - D bounded tick (2,400 ticks, Crashlanded): D stage completed; overall incomplete as always reported.
  - `Recalculate` ran without exceptions.
  - With the default setting, all 12 start stacks are forbidden and were excluded (stock 0, complete scan). The husky's shortage alert was active ("Feed shortage: day 1").
  - With forbidden food included: 12 stacks and 51.3 nutrition were counted with 52 access checks. The first animal shortage was day 10, consistent with 4.8 + 0.8 nutrition per day.
  - `Window_FeedForecast` constructs without exceptions.

Still unverified: long normal gameplay, pens and standing plants on a populated colony, babies and prisoners, rendering of the alert and window, large-map calculation time/TPS and large mod lists.

Review follow-up (PR #16):
- Staged preference now opens source capacity stage by stage inside one residual graph. Later stages can reroute earlier pasture/meal flow, so a preference never creates a deficit that a single max-flow would avoid. Two regression assertions were added.
- The feed alert switches to an affected map before opening the window when the current map is not affected.
- The bounded real-game probe was re-run with identical results.
