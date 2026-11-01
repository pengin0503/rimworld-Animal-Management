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
