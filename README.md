# Animal Management

[日本語](#日本語) | [English](#english)

## 日本語
RimWorld 1.7用の家畜飼料監視MOD。家畜の栄養消費量、備蓄飼料の栄養量、推定残日数、種別の不足リスクを表示します。初期版では放牧の将来予測・自動屠殺は行いません。

**状態: 開発版。コンパイル・ゲーム内動作は未検証です。** 備蓄量は消費可能な栄養量の保証ではありません。外部のMODが追加する餌、アクセス不能な餌、季節変化などで誤差が生じます。

### ビルド
.NET 10 SDKとRimWorld 1.7の同じビルドのゲームDLLが必要です。 `dotnet build Source/AnimalManagement.csproj -c Release /p:RimWorldManaged=/path/to/RimWorld` 。ゲーム本体のDLLは同梱しません。

## English
A RimWorld 1.7 animal feed monitoring utility. Displays nutrition consumed by colony animals, stored potentially edible nutrition, estimated coverage days and the species most at risk. The initial version does not model future pasture growth or automatically slaughter animals.

**Status: Development source, compilation and gameplay unverified.** Stored nutrition is not a guarantee of accessible or available feed; modded food, inaccessible stores, competing consumption and seasonal changes can affect results.

### Build
.NET 10 SDK and reference DLLs from the matching RimWorld 1.7 build are required. Run `dotnet build Source/AnimalManagement.csproj -c Release /p:RimWorldManaged=/path/to/RimWorld`. Game DLLs are not included.
