# Animal Management v1.1.0 — RimWorld 1.7

## 日本語

**新機能**
- 7・15・30日先の飼料不足対策と冬越し備蓄目標、種別の適正飼育頭数推計を追加。
- バニラのキブル製造指示について、材料・設備の確認付き作成と個別許可のある限定自動化に対応。手動製造指示を保護し、実行上限・クールダウン・履歴を保持。
- 自動屠殺の設定変更案は独立した許可と毎回の確認が必要。デフォルトは確認付き。
- 飼料備蓄ゼロでも参考計画と適正頭数を表示。生産の実行には対象・材料の追加確認が必要。

**修正・改善**
- 複数の原材料を同じ栄養プールから使う場合の控除計算を修正し、安全性を確認できない材料の組み合わせは自動生産を禁止。
- キブル製造の正常な進捗・完了・期限切れと、プレイヤーによる編集・停止を区別。
- 放牧・季節の影響がある場合も、予測と放牧ゼロの保守的条件の両方で各消費者の栄養状態を悪化させないと証明できる場合のみ、自動生産候補として扱う。
- 種別の飼育頭数推計、操作履歴・停止理由の表示を改善。
- 飼料アクセス集合を固定長キーにして不要な文字列生成・プール検索を削減し、予測計算の重複処理を削減。

**検証範囲**
- 公開GitHub Actionsでモデル・回帰テスト、Releaseビルド、翻訳・配布構成の検証を実施。
- 1.1.0新機能の通常ゲーム操作、完全なセーブ・ロード、270以上のMODとの互換性、ゲームTPS/GCの比較は未検証。計算結果は保証ではありません。

## English

**Added**
- Actionable 7/15/30-day feed reserves, winter stock targets and sustainable capacity estimates per animal species.
- Confirmed and separately permissioned bounded vanilla kibble bill production. Player-authored bills remain protected, with caps, cooldown and action history.
- Slaughter-policy suggestions require a separate permission and confirmation for each change. Confirmation is the default.
- Advisory feed-free estimates and herd capacity are available without existing hay or kibble stacks; production still requires a verified destination and ingredients.

**Fixed and improved**
- Corrected pooled ingredient-nutrition debits and denied uncertain ingredient choices for automated production.
- Distinguished natural bill progress/completion/expiry from user edits and suspension.
- Under weather uncertainty, production needs independent proof under both observed growth and zero-grazing conditions without worsening any consumer's food shortage.
- Improved species-specific capacity calculations and visibility of automatic stop reasons.
- Reduced access-mask string allocations, linear pool lookups and duplicated forecast work.

**Verification**
- Public GitHub Actions regression tests, Release compilation, translation and package validation passed.
- Normal in-game operation of 1.1.0 features, full save/reload, 270+ mod compatibility and actual TPS/GC improvements are **not verified**.

---
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
