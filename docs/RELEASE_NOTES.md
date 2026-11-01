Animal Management v1.0.0 for RimWorld 1.7 (built against 1.7.4985). No dependencies.

## 日本語

家畜の頭数・栄養需要・備蓄飼料・放牧地の成長をまとめ、前提条件付きで飼料不足を予測する表示専用MODです。自動給餌・自動屠殺・設定変更は行いません。

**主な機能**
- 種別・放牧地別の頭数と日次栄養需要、入植者・囚人の需要と共有備蓄（二重計上なし）
- 備蓄のみの維持可能日数、家畜で最初の不足日、7・15・30日の飼料収支と放牧地別の不足日
- 季節ごとの放牧地の成長（氷点下では停止、使わなかった分は持ち越さない）、腐敗による損失
- 個体ごとの食性・食料ポリシー・許可エリア・禁止・放牧地境界・到達性を確認した飼料だけを算入
- 前提条件・不確実性・無効化条件の表示、任意の出生・死亡シナリオ

**導入**: ZIPを展開し、`AnimalManagement` フォルダを `Mods` に置いて有効化します。

**未検証範囲・既知の制限**
- 予測は条件付きの推定で、実際のAIの給餌順序とは異なる場合があります。
- 放牧地外での草食、飼育担当者の給餌、異常気象、所持品・容器内の飼料は予測に含めません。
- 通常のゲームプレイでの動作は確認していません。確認済みなのは、実物ゲームDLLを参照したビルド、静的な参照照合、モデル単体の確認、設定の保存形式の確認です。
- 大規模マップでの計算時間・TPS、多数MOD環境での互換性は未計測・未検証です。
- このZIPに含まれるDLLは、公開参照アセンブリ（Krafs.Rimworld.Ref 1.7.4985）でCIビルドしたものです。

## English

An information-only mod that summarizes livestock counts, nutrition demand, stored feed and pasture growth, and forecasts feed shortages under stated assumptions. It never feeds, slaughters or changes settings.

**Features**
- Counts and daily nutrition demand by species and pen; colonist/prisoner demand and shared stocks, never counted twice
- Stock-only coverage days, first animal shortage day, 7/15/30-day feed balances and per-pen deficit days
- Seasonal pasture growth (zero below freezing, no carry-over of unused growth) and spoilage losses
- Feed counts only when diet, food policy, allowed area, forbidden state, pen boundary and reachability are verified per animal
- Assumptions, uncertainty and invalidation conditions shown; optional birth/death scenario

**Installation**: extract the ZIP, place the `AnimalManagement` folder in `Mods` and enable it.

**Unverified scope and known limitations**
- Forecasts are conditional estimates and can differ from actual AI feeding order.
- Off-pen grazing, handler feeding, weather events and feed in inventories/containers are not modeled.
- Not tested in normal gameplay. Checked: a build against the real game DLLs, static reference matching, model unit checks and settings save-format checks.
- Calculation time/TPS on large maps and compatibility with large mod lists are not measured or verified.
- The DLL in this ZIP was built in CI against public reference assemblies (Krafs.Rimworld.Ref 1.7.4985).
