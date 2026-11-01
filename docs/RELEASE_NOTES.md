Animal Management v1.0.1 for RimWorld 1.7 (built against 1.7.4985). No dependencies.

## 日本語

**追加**
- 完全な予測で警告日数以内に家畜の飼料不足が見込まれると、画面右にアラートを出します。クリックで予測ウィンドウを開きます。

**修正**
- 植物が多いマップでは、備蓄飼料にたどり着く前に走査上限に達し、備蓄が数えられないことがありました（v1.0.0から）。
- 需要を満腹時の消費速度で計算します。従来は空腹・飢餓中に需要が小さく（飢餓中は0に）見えていました。
- 到達性と食性の判定をまとめて行い、中規模コロニーでも上限に届きにくくしました。
- 食べる順番を段階化しました。人間は家畜が食べられない食料を先に、家畜は放牧の成長分 → 立ち草 → 備蓄の順に食べる前提です。
- 放牧地に生えている植物、囚人・寝たきりの入植者への配膳、乳児の授乳、放牧地外で草を食べる家畜（予測から除外）を扱います。
- 共有飼料による放牧地ごとの不足日は、割り当て次第である旨を表示します。走査が不完全なときは、確定的な警告を出しません。

**確認した範囲**: 実ゲーム（Linux、1.7.4985 rev1269）で次を確認しました。長時間の通常プレイは未確認です。
- 予測の計算とアラートが、例外なく動くこと。
- 備蓄・アクセス・不足日の算出が、手計算と一致すること。

## English

**Added**
- An alert on the right side of the screen when a complete forecast predicts an animal feed shortage within the threshold. Clicking it opens the forecast window.

**Fixed**
- On plant-rich maps the scan hit its cap before reaching stored food, so stock could go uncounted (since v1.0.0).
- Demand uses the fed consumption rate. Previously demand looked smaller while hungry or starving (0 when starving).
- Reachability and food suitability are evaluated once per district and food type, so mid-size colonies rarely hit the cap.
- Feeding order is staged: humans eat food animals cannot use first; animals eat daily growth, then standing plants, then stock.
- Standing pen plants, serving prisoners and downed colonists, breastfed babies and grazers outside pens (excluded) are handled.
- Shortage dates of pens sharing feed are labeled allocation-dependent. Incomplete scans give no definitive warning.

**Checked**: in the real game (Linux, 1.7.4985 rev1269) the following were checked. Long normal gameplay is not verified.
- Forecast calculation and the alert run without exceptions.
- Stock, access and shortage-day results match a manual calculation.

The DLL in this ZIP was built in CI against public reference assemblies (Krafs.Rimworld.Ref 1.7.4985).
