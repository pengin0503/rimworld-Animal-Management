# Build and verification / ビルド・検証

- Use a matching RimWorld 1.7 game root containing Assembly-CSharp.dll, Waylith.dll and UnityStubs.dll.
- Run: `dotnet build Source/AnimalManagement.csproj -c Release /p:RimWorldManaged=/path/to/RimWorld`.
- Generated: `1.7/Assemblies/AnimalManagement.dll` (after successful build).
- Test with animals that eat different diets; also test zero animals, zero stored food, forbidden food, mixed diets and a 12,000+ object map.
- Values are upper-bound-like, not strict guarantees, because accessibility, pasture plants and other consumers are not modelled.
- Do not publish a gameplay-ready release until verified on an actual RimWorld 1.7 instance.
