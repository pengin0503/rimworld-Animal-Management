# Animal Management

[日本語](#日本語) | [English](#english)

## 日本語

Animal Management は RimWorld 1.7 用の家畜飼料予測MODです。家畜の頭数と必要な栄養量、備蓄飼料、放牧地の成長をまとめ、今後の飼料不足を前提条件付きで予測します。表示専用で、自動給餌・自動屠殺・飼育設定の変更は一切行いません。Harmony は不要です。

### 表示内容

- 種別・放牧地別の頭数と、1日あたりの栄養需要（個体ごとの実際の消費速度から算出）
- 入植者・囚人の需要と、人間と家畜で共有する備蓄
  - 共有している備蓄は二重に数えません。
  - 人間が先に食べる前提で、家畜に残る量を予測します。
- 備蓄だけで維持できる日数と、家畜で最初に飼料が不足する日
- 7・15・30日の飼料収支
  - 備蓄からの消費、放牧からの消費、不足量、不足開始日を表示します。
  - 放牧地ごとの不足量と不足開始日も表示します。
- 放牧地ごとの今日の自然成長量と収支
  - 季節による成長の変化を反映し、氷点下の予測日は成長をゼロとします。
  - その日に食べきれなかった放牧分は翌日に持ち越しません。
- 腐敗による損失
  - 屋外は季節の気温、屋内は現在の室温が続く前提で計算します。
  - 予測期間中に腐る飼料は、腐る日以降には使いません。
- 予測の前提条件、不確実性、予測が無効になる条件
- 任意のシナリオとして、30日間の出生数・死亡数を指定できます。現在の種構成に比例して需要を増減します。

### 利用できる飼料の判定

個体ごとに次の条件をすべて確認できた飼料だけを、その個体が利用できる飼料として数えます。

- 食性
- 食料ポリシー
- 許可エリア
- 禁止状態（設定で含めることもできます）
- 放牧地の境界
- 到達性

ダウン中の個体は、自力で食べに行けないものとして扱います。食性によって得られる栄養量が違う場合は、共有する飼料ごとに最も少ない値を使います。確認件数の上限を超えたときは「不完全」と表示し、未確認の飼料は数えません。

### 使い方

1. リリースのZIPを展開した `AnimalManagement` フォルダを RimWorld の `Mods` フォルダに置き、有効化します。
2. 「設定」→「MOD設定」→「Animal Management」を開きます。予測は10,000 Tickごとに更新されます。「再計算」ですぐに更新することもできます。
3. 警告日数、禁止された飼料を含めるか、出生・死亡シナリオを設定できます。

予測結果はセーブデータに保存しません（設定のみ保存されます）。途中導入・途中削除ができます。

### 制限事項と未検証範囲

- **予測は条件付きの推定で、不足日を確定するものではありません。** 予測上の飼料の割り当ては、実際のAIの行動と異なる場合があります。
- 次のものは予測に含めません。
  - 放牧地外での草食、飼育担当者による運搬・給餌
  - 異常気象、温度管理の故障、加工、成長段階の変化
  - 所持品・容器内の飼料
- 消費者が400人・頭を超えるマップでは計算を行わず、失敗と表示します。
- **通常のゲームプレイでの動作確認は行っていません。** 公開前に確認したのは次の範囲です。
  - 実物のゲームDLLを参照したビルド
  - 静的な参照照合
  - モデル単体の確認
  - 設定の保存形式の確認
- 大規模マップでの計算時間・TPSへの影響と、多数のMODとの互換性は計測・検証していません。計算時間は画面の最下部に表示されます。

## English

Animal Management is a livestock feed forecasting mod for RimWorld 1.7. It summarizes animal counts, nutrition demand, stored feed and pasture growth, and forecasts future feed shortages under stated assumptions. It is information only: it never feeds animals, slaughters them or changes animal settings. Harmony is not required.

### What it shows

- Animal counts and daily nutrition demand by species and by pen, from each animal's actual consumption rate
- Colonist and prisoner demand, and food stocks shared between humans and animals
  - Shared stocks are never counted twice.
  - Humans are assumed to eat first, and the forecast shows what remains for animals.
- Days that stored feed alone can sustain, and the first day animals run short
- 7/15/30-day feed balances
  - Stock consumed, pasture consumed, unmet nutrition and the first deficit day.
  - Per-pen deficit and first deficit day.
- Today's natural growth and net balance for each pen
  - Growth follows the seasons and is zero on forecast days below freezing.
  - Unused daily pasture is not carried over to the next day.
- Spoilage losses
  - Outdoors uses seasonal temperatures; indoors assumes the current room temperature is maintained.
  - Feed that spoils during the forecast is not used from the day it spoils.
- Forecast assumptions, uncertainty and conditions that invalidate the forecast
- An optional scenario for births and deaths over 30 days, which scales demand in proportion to the current species mix

### How usable feed is decided

Feed is counted for an animal only when all of these checks pass for that individual:

- diet
- food policy
- allowed area
- forbidden state (optionally included)
- pen boundary
- reachability

Downed animals are treated as unable to reach feed. When nutrition differs by eater, each shared pool uses the lowest value. When the check budget is exceeded, the result is labeled incomplete and unchecked feed is not counted.

### Usage

1. Extract the release ZIP, place the `AnimalManagement` folder in RimWorld's `Mods` folder and enable it.
2. Open Settings → Mod Settings → Animal Management. Forecasts refresh every 10,000 ticks; use "Recalculate" to refresh immediately.
3. Configure the warning threshold, whether forbidden feed is included, and the birth/death scenario.

Forecasts are not saved; only settings are. The mod can be added to or removed from an existing save.

### Limitations and unverified scope

- **Forecasts are conditional estimates, not confirmed shortage dates.** The modeled feed allocation can differ from actual AI behavior.
- Not modeled:
  - grazing outside pens, and hauling or feeding by handlers
  - weather events, temperature-control failure, food processing and life-stage changes
  - feed in inventories or containers
- Maps with more than 400 consumers are not calculated and are reported as failed.
- **Not yet tested in normal gameplay.** Before release, only the following were checked:
  - a build against the real game DLLs
  - static reference matching
  - model unit checks
  - settings save-format checks
- Calculation time and TPS impact on large maps, and compatibility with large mod lists, have not been measured or verified. The calculation time is shown at the bottom of the panel.
