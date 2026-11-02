# Animal Management Actionable Automation Implementation Plan
> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`). This is a plan, NOT an implementation.
**Goal:** 2026-11-02の設計に従い、実行可能な飼料不足解消プラン、飼料生産支援、維持可能頭数を安全に提供する。
**Architecture:** `FeedForecast`の既存計算結果を読取専用で利用する提案エンジンと、操作前条件を再検証する独立の実行層に分割。操作状態はMapComponent、ユーザー設定はModSettingsへ。
**Tech Stack:** C#, .NET 10, RimWorld 1.7 API, existing `Tests`/`Tests.Game`, GitHub Actions.
**Spec:** `docs/superpowers/specs/2026-11-02-actionable-automation-design.md`

## Global Constraints
- 既存の正常動作（ユーザーによるプレイ確認）と既存UI・栄養予測の互換性を維持。
- Confirmが既定。Autoは機能ごとに明示ON。危険な処理は追加許可。部分スキャン/古い結果ではAuto禁止。
- ゲームDLLはGitHubに保存しない。R2参照資料を先に確認する。
- 既存利用者のセーブを読み込めること。日本語・英語同等。

## Review Focus
- [ ] 共有備蓄を人間と異なる食性の動物が奪い合うときの必要追加量は二重計上されない（Task 1）。
- [ ] 部分スキャン/腐敗/冬季の変動がある場合、確定提案や自動変更が行われない（Tasks 1, 3）。
- [ ] プレイヤーが手動編集した製造指示を所有し続けない（Task 3）。
- [ ] 低温・栽培不可期の干し草必要面積を保証値として提示しない（Task 2）。
- [ ] 自動屠殺の許可をONにしていない限り不可逆行為に通じる変更をしない（Task 4）。

### Task 1: 不足の逆算
**Files:** Create `Source/FeedActionPlanner.cs`; Modify `Source/NutritionForecast.cs`（必要時のみ）; Test `Tests/Program.cs`.
- [ ] 追加飼料の種類・利用者別到達性を指定したwhat-ifテストを先に追加し、期待どおり失敗することを確認。
- [ ] `ForecastSnapshot`および共有配分モデルを使う最小限の`FeedActionPlan`/ `FeedActionPlanner`を実装。完成した計画に前提・信頼性・対象・不足量・必要個数を含める。
- [ ] 食性分離・余剰/不足・腐敗・部分データのテストを通す。スナップショットの不変性を確認。
- [ ] 自動テスト実行・コードレビュー・タスク単位コミット。

### Task 2: 冬越し目標・UI
**Files:** Modify `Source/AnimalManagementUI.cs`, `Source/AnimalManagement.cs`, language XMLs; Test `Tests/Program.cs`, `Tests.Game/Program.cs`.
- [ ] 7/15/30日、冬季成長0、必要備蓄・必要生産・不確実性表示の失敗テストを追加。
- [ ] 既存予測と整合する不足対策UI、候補飼料の切替、ワンクリック再計算、対象位置への誘導、条件の表示を実装。
- [ ] 保存設定の旧セーブ初期値・日英翻訳・大規模UIの表示を確認してコミット。

### Task 3: 生産指示の確認付き作成・限定Auto
**Files:** Create `Source/ManagedFeedBills.cs`, `Source/AnimalActionState.cs`; Modify `Source/AnimalManagement.cs` / UI / language XMLs; Test `Tests/Program.cs`, `Tests.Game/Program.cs`.
- [ ] 既存の手動Billを変更しない、二重作成なし、権限のない台を使わない、ロード後も所有Billを認識する失敗テストを追加。
- [ ] `RecipeDef`, `Bill_Production`, `BillStack.AddBill`等の実物APIとレシピ制約を検証して実装。Confirmはプレビュー、Autoは明示許可・上限・クールダウン内に限る。
- [ ] 監督対象のBillをユーザーが編集・停止した場合は管理権限を放棄。干し草の畑については場所・作期・規模の提案を優先し、無断新設しない。
- [ ] セーブ/ロード・重複防止・停止・回帰テスト後コミット。

### Task 4: 飼育頭数・屠殺設定の安全な提案
**Files:** Create `Source/HerdCapacityPlanner.cs`; Modify `Source/AnimalManagementUI.cs` / `AnimalSettings` and language XMLs; Test `Tests/Program.cs`, `Tests.Game/Program.cs`.
- [ ] 頭数増減のwhat-if計算・食性別上限・共通餌の競合・繁殖シナリオの失敗テストを追加。
- [ ] 推定可能頭数と対象選択肢を計算して表示。バニラ `Verse.AutoSlaughterConfig` / `AutoSlaughterManager`の編集可能性と副作用を実物で確認。
- [ ] 標準Autoモードでも屠殺設定に触れない。追加許可ONと対象確認がある場合に限り設定変更プレビューを許す。妊娠・絆のある動物の保護条件を維持。
- [ ] 手動設定上書き防止・副作用注意・日英UI・保存/ロードを確認してコミット。

### Task 5: 統合・負荷・成果物
**Files:** Existing `Tests*`, `docs/VALIDATION.md`, `README.md`, `docs/RELEASE_NOTES.md`, `.github/workflows/*.yml`（必要時）。
- [ ] 既存テスト全体・全追加テスト・Releaseビルドを実行する。
- [ ] R2実体API、Linux検証ホスト、通常ゲーム（必要時）で機能・保存/ロード・途中削除を確認する。
- [ ] 270以上のMOD・大規模動物群・大規模備蓄でTPS/時間/GCをベースライン比較。高負荷なら処理上限とインターバルを調整。
- [ ] PR・レビュー・CI・ドキュメント更新。変更済みコードを確認してからマージ。バージョン番号・リリースは実装検証時に決定する。

**検証コマンド（予定）:** `dotnet run --project Tests/Tests.csproj -c Release`; `dotnet build Source/AnimalManagement.csproj -c Release`。実物DLLを使う場合は `-p:RimWorldManaged=/path/to/RimWorld`。各テストの失敗→修正→成功を観測する。
