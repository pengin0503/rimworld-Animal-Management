# Animal Management

[日本語](#日本語) | [English](#english)

## 日本語

Animal Management は RimWorld 1.7 用の家畜飼料予測MODです。家畜の頭数と必要な栄養量、備蓄飼料、放牧地の草をまとめ、今後の飼料不足を前提条件付きで予測します。備蓄対策と限定的な製造指示を支援します。初期設定は確認付きで、自動化は機能・マップ内の対象ごとに許可が必要です。Harmony は不要です。

### 備蓄対策・生産・適正頭数

予測ウィンドウの冒頭で7・15・30日の目標と飼料・配送先を選び、追加必要数を計算します。干し草・キブルの既存の到達可能な備蓄を基に検証し、将来の天候・腐敗・部分走査が関わる予測は暫定表示とし、製造操作には使用しません。畑の参考面積は収穫の保証ではありません。

生産は対応するバニラのキブル指示に限定します。「材料を確認して生産プレビュー」で消費材料・設備を確認し、承認すると指示を作成します。自動化には設定の「自動」と設備・配送先の個別許可が必要です。手動指示との重複を防ぎ、指示の編集・停止で管理を解除します。指示は回数を固定した一括生産で、上限・クールダウンに従います。進捗・編集後に自動再調整しません。

頭数シナリオは他種・人間の需要を維持し、共有飼料を一度だけ配分します。推計は種別の代替案であり合算できません。自動屠殺の設定変更には別の設定許可と毎回の確認が必要です。妊娠・絆のある個体を保護しますが、設定変更後にバニラが不可逆な屠殺を行う可能性があります。

新機能の通常ゲーム内UI・セーブ再読込・270以上のMODでの検証は未実施です。

### 表示内容

- 種別・放牧地別の頭数と、1日あたりの栄養需要
  - 需要は個体ごとの満腹時の消費速度で計算します。空腹中は食料ゲージの減り方が遅くなりますが、その値は使いません。不足し始めたときに需要が小さく見えるのを防ぐためです。
- 入植者・囚人の需要と、人間と家畜で共有する備蓄
  - 共有している備蓄は二重に数えません。
  - 食べる順番を段階的に仮定します。人間は家畜が食べられない食料（食事など）を先に食べ、足りない分だけ共有の食料を使います。
  - 囚人やダウン中の入植者は、自由に動ける入植者が届けられる食料から配膳される前提です。
  - 授乳中の入植者がいる間、乳児は授乳で育つ前提として備蓄の需要から除きます。
- 備蓄だけで維持できる日数と、家畜で最初に飼料が不足する日
- 7・15・30日の飼料収支
  - 家畜はその日の放牧地の成長分を先に食べ、次に放牧地に今生えている植物、最後に備蓄を食べる前提です。
  - 備蓄からの消費、放牧・立ち草からの消費、不足量、不足開始日を表示します。
  - 放牧地ごとの不足量と不足開始日も表示します。他の放牧地や人間と飼料を共有している場合は、どこが不足するかは割り当て次第である旨を併記します。
- 放牧地ごとの今日の自然成長量と収支、放牧地に今生えている植物の栄養量
  - 季節による成長の変化を反映し、氷点下の予測日は成長をゼロとします。
  - その日に食べきれなかった成長分は翌日に持ち越しません。立ち草は食べると回復しない前提です。
- 腐敗による損失
  - 屋外は季節の気温、屋内は現在の室温が続く前提で計算します。
  - 予測期間中に腐る飼料は、腐る日以降には使いません。
- 放牧地の外で草を食べる家畜（ローマー種の放し飼いなど）は、野生の植物を食べるため予測から除外し、頭数と需要を別に表示します。
- 予測の前提条件、不確実性、予測が無効になる条件
- 任意のシナリオとして、30日間の出生数・死亡数を指定できます。現在の種構成に比例して需要を増減します。
- 完全な予測で、警告日数以内に家畜の飼料不足が見込まれると、画面右にアラートを出します。クリックすると予測ウィンドウ（マップを見ながら使えます）が開きます。走査が不完全なときは、不足の可能性としてだけ表示し、アラートは出しません。

### 利用できる飼料の判定

個体ごとに次の条件をすべて確認できた飼料だけを、その個体が利用できる飼料として数えます。

- 食性
- 食料ポリシー
- 許可エリア
- 禁止状態（設定で含めることもできます）
- 放牧地の境界
- 到達性

ダウン中の家畜は、自力で食べに行けないものとして扱います。食性によって得られる栄養量が違う場合は、共有する飼料ごとに最も少ない値を使います。到達性は地区ごと、食べられるかどうかは個体と飼料の種類ごとにまとめて判定します。確認件数の上限を超えたときは「不完全」と表示し、未確認の飼料は数えません。

### 使い方

1. リリースのZIPを展開した `AnimalManagement` フォルダを RimWorld の `Mods` フォルダに置き、有効化します。
2. 画面右のアラート、または「設定」→「MOD設定」→「Animal Management」→「予測ウィンドウを開く」で予測を確認します。予測は10,000 Tickごとに更新されます。「再計算」ですぐに更新することもできます。
3. 警告日数、禁止された飼料を含めるか、アラートの有無、出生・死亡シナリオを設定できます。

予測結果は再計算します。設定に加え、製造指示の所有情報・対象許可・操作履歴をマップ別に保存します。途中削除前に管理中の製造指示を停止してください。バニラの製造指示はMOD削除後も残ります。

### 制限事項と未検証範囲

- **予測は条件付きの推定で、不足日を確定するものではありません。** 予測上の飼料の割り当ては、実際のAIの行動と異なる場合があります。
- 次のものは予測に含めません。
  - 放牧地外での草食、飼育担当者による家畜への運搬・給餌
  - 異常気象、温度管理の故障、加工、成長段階の変化
  - 所持品・容器内の飼料
- 立ち草とその後の成長分は、どちらもバニラの推計にもとづく概算です。
- 消費者が400人・頭を超えるマップでは計算を行わず、失敗と表示します。
- **通常のゲームプレイでの長時間の動作確認は行っていません。** 確認した範囲は `docs/VALIDATION.md` を参照してください。
- 大規模マップでの計算時間・TPSへの影響と、多数のMODとの互換性は計測・検証していません。計算時間は予測の最下部に表示されます。

## English

### Reserve solutions, production and herd capacity

Choose a 7/15/30-day horizon, feed and destination at the top of the forecast window. Replacement quantities use verified accessible hay/kibble stocks. Weather, spoilage or incomplete observations make a scenario provisional and block production actions. The illustrated hay area is not a harvest guarantee.

Production supports the vanilla kibble recipe only. Preview ingredients and the workbench before approving an instruction. Automation requires both the feature Auto mode and permission for the specific bench/destination on this map. Existing manual bills are neither duplicated nor changed. Editing or suspending an instruction releases ownership. Instructions use a fixed finite batch count, caps and cooldowns. Production progress or edits release automatic ownership; they are not readjusted automatically.

Species capacity scenarios keep other species and human demand fixed and allocate shared feed once; their results cannot be added together. Slaughter configuration changes require a separate opt-in and confirmation every time. Pregnancy/bond protection is enabled, but vanilla may subsequently slaughter animals irreversibly.

Settings, bill ownership, target permissions and action history are saved per map; forecasts are recalculated. Stop managed bills before removing the mod: vanilla bills remain after removal. The new UI, whole-map save/reload and 270+ mod compatibility have not been verified in normal gameplay.


Animal Management is a livestock feed forecasting mod for RimWorld 1.7. It summarizes animal counts, nutrition demand, stored feed and pasture, and forecasts future feed shortages under stated assumptions. It adds reserve advice and bounded production assistance. Confirmation is the default; automation requires explicit feature and target permissions. Harmony is not required.

### What it shows

- Animal counts and daily nutrition demand by species and by pen
  - Demand uses each animal's fed consumption rate. The food meter falls more slowly while a pawn is hungry; that value is not used, so demand does not look smaller just when feed starts running out.
- Colonist and prisoner demand, and food stocks shared between humans and animals
  - Shared stocks are never counted twice.
  - Feeding order is staged: humans first eat food animals cannot use (such as meals), and use shared food only for the rest.
  - Prisoners and downed colonists are assumed to be served from food a free colonist can reach.
  - While a lactating colonist is present, babies are assumed to be breastfed and are excluded from stored-food demand.
- Days that stored feed alone can sustain, and the first day animals run short
- 7/15/30-day feed balances
  - Animals are assumed to eat today's pen growth first, then the plants currently standing in the pen, then stored feed.
  - Stock consumed, pasture and standing plants consumed, unmet nutrition and the first deficit day.
  - Per-pen deficit and first deficit day. When a pen shares feed with other pens or humans, the line notes that which pen runs short depends on the allocation.
- Today's natural growth and net balance for each pen, and the nutrition of plants currently standing in pens
  - Growth follows the seasons and is zero on forecast days below freezing.
  - Unused daily growth is not carried over; standing plants are not replenished once eaten.
- Spoilage losses
  - Outdoors uses seasonal temperatures; indoors assumes the current room temperature is maintained.
  - Feed that spoils during the forecast is not used from the day it spoils.
- Grazing animals outside pens (such as free-roaming roamer species) feed on wild plants, so they are excluded from the forecast and listed with their count and demand.
- Forecast assumptions, uncertainty and conditions that invalidate the forecast
- An optional scenario for births and deaths over 30 days, which scales demand in proportion to the current species mix
- When a complete forecast predicts an animal feed shortage within the warning threshold, an alert appears on the right. Clicking it opens the forecast window, which can be used while watching the map. An incomplete scan only shows a possible shortage and raises no alert.

### How usable feed is decided

Feed is counted for an animal only when all of these checks pass for that individual:

- diet
- food policy
- allowed area
- forbidden state (optionally included)
- pen boundary
- reachability

Downed animals are treated as unable to reach feed. When nutrition differs by eater, each shared pool uses the lowest value. Reachability is evaluated once per district, and food suitability once per animal and food type. When the check budget is exceeded, the result is labeled incomplete and unchecked feed is not counted.

### Usage

1. Extract the release ZIP, place the `AnimalManagement` folder in RimWorld's `Mods` folder and enable it.
2. Use the alert on the right, or Settings → Mod Settings → Animal Management → "Open forecast window". Forecasts refresh every 10,000 ticks; use "Recalculate" to refresh immediately.
3. Configure the warning threshold, whether forbidden feed is included, the alert and the birth/death scenario.

Forecasts are recalculated. Settings and map-local bill ownership, target permissions and history are saved. Stop managed instructions before removing the mod; vanilla bills remain after removal.

### Limitations and unverified scope

- **Forecasts are conditional estimates, not confirmed shortage dates.** The modeled feed allocation can differ from actual AI behavior.
- Not modeled:
  - grazing outside pens, and hauling or feeding of animals by handlers
  - weather events, temperature-control failure, food processing and life-stage changes
  - feed in inventories or containers
- Standing plants and later growth are both approximations based on vanilla estimates.
- Maps with more than 400 consumers are not calculated and are reported as failed.
- **Not yet tested in long normal gameplay.** See `docs/VALIDATION.md` for exactly what was checked.
- Calculation time and TPS impact on large maps, and compatibility with large mod lists, have not been measured or verified. The calculation time is shown at the bottom of the forecast.
