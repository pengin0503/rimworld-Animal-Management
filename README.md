# Animal Management

[日本語](#日本語) | [English](#english)

## 日本語

RimWorld 1.7の家畜栄養予測MODです。動物種・放牧地ごとの頭数と日次需要、家畜と人間の共有備蓄、備蓄のみの不足日、7・15・30日の季節別予測を表示します。放牧由来と備蓄由来の消費を分け、同じ餌は一度だけ割り当てます。

食性、食料ポリシー、許可エリア、禁止、放牧地、到達性を確認します。確認上限を超えた場合は「不完全」と表示し、未確認の餌を加算しません。出生・死亡は、既存の種構成に比例した成体換算の任意シナリオとして指定できます。

**開発版です。通常プレイ、マップ保存・読み込み、270以上のMOD環境、実スキャン時間・TPSへの影響は未検証です。** 季節予測はバニラの季節別成長推計を使い、季節モデルの氷点下では成長ゼロとします。腐敗期限と摂取者ごとの栄養量を考慮します。異常気象、将来の飼育者の給餌、成長段階の変化は予測条件の変化として扱います。室内飼料は現在の温度管理が維持される前提です。予測の餌割当は実際のAIと異なる場合があります。食料や放牧地を変更したら再計算してください。

### 利用方法

RimWorld 1.7用の開発版ビルドをModsフォルダに配置して有効化し、「設定」→「MOD設定」→「Animal Management」を開きます。スクロールして前提条件と全放牧地の結果を確認できます。自動給餌・自動屠殺・設定変更は行いません。重要なセーブでの利用は避けてください。

## English

A RimWorld 1.7 animal nutrition forecasting mod. It shows animal counts and daily demand by species and pen, shared animal/human food stocks, stock-only shortage timing, and seasonal 7/15/30-day scenarios. Pasture and stock consumption are separate; each feed pool is allocated only once.

It checks diets, food policies, allowed areas, forbidden state, pen boundaries and reachability. Budget-limited results are labeled incomplete and unverified food is not credited. Optional birth/death scenarios use adult-equivalent demand proportional to existing species composition.

**Development build. Normal gameplay, full-map save/load, 270+ MOD compatibility and actual scan/TPS overhead are unverified.** Seasonal projections use vanilla quadrum growth estimates, set to zero at modeled freezing temperature. Spoilage deadlines and eater-specific nutrition are modeled. Unusual weather, future handler feeding and life-stage changes invalidate the assumptions; current indoor temperature control is assumed to persist. Forecast allocation may differ from game AI. Recalculate when food or pens change.

### Usage

Install a RimWorld 1.7 development build in your Mods folder and enable it. Open Settings → Mod Settings → Animal Management. Scroll to inspect assumptions and every pen. The mod does not automatically feed, slaughter or change animal settings. Avoid important saves until validated.
