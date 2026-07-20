# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project kind

This is **flametail**, a Slay the Spire 2 mod built on the **STS2-RitsuLib** framework. It is **not** a standalone game: the build produces a mod (`flametail.dll` + `flametail.pck` + `flametail.json`) that is installed into the game's `mods/` folder.

- Engine: Godot 4.5.1 with .NET 9, C# 13.
- Project files: `flametail.csproj`, `flametail.sln`, `project.godot`.
- C# source root: `flametailCode/` (namespace `flametail`).
- Godot resources / localization: `flametail/` directory, mapped in-game to `res://flametail/`.
- Mod manifest: `flametail.json`.

## Local setup

Copy `local.props.template` to `local.props` (gitignored; never commit it) and set the paths for your machine:

| Property | Purpose |
|---|---|
| `Sts2Dir` | Slay the Spire 2 install directory. |
| `Sts2DataDir` | Game DLL directory, usually `$(Sts2Dir)/data_sts2_windows_x86_64`. |
| `GodotExe` | MegaDot/Godot executable used to export the PCK. |
| `RitsuLibDeployDir` | Optional; local RitsuLib deployment directory, default `$(Sts2Dir)/mods/STS2-RitsuLib`. |

The build validates that `$(Sts2DataDir)/sts2.dll` exists; if it fails, check `local.props`.

## Common commands

Build (compile + copy mod to game's `mods/flametail/` + export PCK):

```powershell
dotnet build .\flametail.csproj
```

Skip PCK export (useful when `GodotExe` is not configured):

```powershell
dotnet build .\flametail.csproj /p:RunPckExport=false
```

Skip copying the mod to the game directory (output stays in `bin/`):

```powershell
dotnet build .\flametail.csproj /p:CopyModOnBuild=false
```

Compile validation only:

```powershell
dotnet build .\flametail.csproj /p:RunPckExport=false /p:CopyModOnBuild=false
```

Restore packages:

```powershell
dotnet restore .\flametail.csproj
```

Open the project in the Godot/MegaDot editor:

```powershell
& "$(GodotExe)" --path .
```

Run Slay the Spire 2 with the mod installed (after a successful build with `CopyModOnBuild=true`):

```powershell
& "$(Sts2Dir)\SlayTheSpire2.exe"
```

### Tests and lint

There are **no unit-test projects** in this repository, so `dotnet test` has nothing to run and there is no "run a single test" command.

There is **no standalone lint command**. The build runs the `Nothing.STS2RitsuLib.ModAnalyzers` analyzer package (e.g., `RITSU013` for missing resource paths), so build warnings are the primary lint signal.

### Content maintenance scripts

These helper scripts live at the repo root and are not part of the main build:

```powershell
python apply_dynamic_descriptions.py   # Refactor hardcoded card numbers into CanonicalVars and update localization.
python audit_numeric_descriptions.py   # Find cards with hardcoded numeric literals worth converting to CanonicalVars.
python add_diff_to_upgradeable.py      # Add `:diff()` to selected placeholder tokens in card descriptions.
python fix_loc_keys.py                 # Convert camel-case card IDs in localization keys to UPPER_SNAKE_CASE.
```

```powershell
godot --headless --path . --script convert_ctex_to_png.gd   # Re-export imported .ctex textures back to PNG.
```

## High-level architecture

### Entry point and registration

`flametailCode/Entry.cs` is the single initializer. `[ModInitializer(nameof(Initialize))]` causes the STS2 mod loader to call `Entry.Initialize()`, which does three things in order:

1. `RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger)` — registers Godot C# scripts so `.tscn` files in the PCK can find their script types.
2. `ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly)` — scans the assembly for RitsuLib registration attributes (`[RegisterCard]`, `[RegisterPower]`, `[RegisterRelic]`, `[RegisterCharacter]`, etc.) and auto-registers content.
3. `new Harmony(ModId).PatchAll(assembly)` — applies Harmony patches, notably `DodgeBlockPatch`.

Both Godot script registration and RitsuLib content registration are required and independent; do not remove either.

### Content conventions

- Cards inherit `ModCardTemplate` and use `[RegisterCard(typeof(flametailCardPool))]` plus `[RegisterCharacterStarterCard(typeof(flametailCharacter), count)]` where applicable.
- Powers inherit `PowerModel` and use `[RegisterPower]`.
- Relics inherit `ModRelicTemplate` and use `[RegisterRelic]`.
- Characters inherit `ModCharacterTemplate<...>` and use `[RegisterCharacter]`.

Card effects are async and use the STS2 command API, e.g. `DamageCmd.Attack(...)`, `PowerCmd.Apply<...>`, `CardPileCmd.Draw(...)`. Numeric values exposed to the player should be declared as `CanonicalVars` (e.g., `DamageVar`, `BlockVar`, `IntVar`) and bound to localization placeholders.

### Resource paths

`res://flametail/...` is the Godot/PCK resource path and maps to the repository directory `flametail/`. The C# namespace is also `flametail`, but these are separate concepts. Always use `Entry.ResPath` (`res://flametail`) when constructing resource paths in code.

### Custom combat mechanics

The mod introduces three tightly coupled mechanics:

1. **Footwork**: `flametailFootworkPower` is a counter buff. When its amount reaches 5, it converts into `flametailDodgePower`; it also decays by 1 at the start of each player turn unless `flametailSteadyStepPower` is present.
2. **Dodge**: `flametailDodgePower` absorbs one enemy attack before Block is consumed. Because the public command API does not expose this ordering, `DodgeBlockPatch.cs` uses Harmony to prefix `Creature.DamageBlockInternal`; `flametailDodgePower.BeforeDamageReceived` registers the target with the patch, which then skips the original block consumption for that attack. Enemy intent preview numbers are **not** changed by this patch because preview uses `Hook.ModifyDamage`, not `DamageBlockInternal`.
3. **Counter cards**: Cards implementing `ICounterCard` are retained at end-of-turn by `flametailCounterManagerPower` and automatically played when the player receives an enemy powered attack. `CounterSystem` stores the current counter context (`IsCounterPlay`, `CurrentAttacker`, `WasAttackDodged`, `LastAttackMitigated`, etc.) in `AsyncLocal<T>` so the async command chain can read it safely. Cards like `flametailBlisteringCounter` use `CounterSystem.PlayNextCounterCard` to chain into the next counter card in hand.

## Content and manifest conventions

### Localization

Translation files are in `flametail/localization/eng/` and `flametail/localization/zhs/` as JSON key-value files. Card keys follow:

```
FLAMETAIL_CARD_<UPPER_SNAKE_ID>.title
FLAMETAIL_CARD_<UPPER_SNAKE_ID>.description
FLAMETAIL_CARD_<UPPER_SNAKE_ID>.smartDescription
```

Use `CanonicalVars` with placeholders like `{Damage:diff()}` so upgraded values are highlighted. Dynamic helpers like `{IfUpgraded:show:a card|an Attack card}` are also supported via SmartFormat.

### Manifest alignment

`flametail.json` fields must stay consistent with the code and build:

- `id` must equal `Entry.ModId` (`"flametail"`).
- `pck_name` must equal the exported PCK file name (`flametail`).
- `dependencies[STS2-RitsuLib].version` must match the `STS2.RitsuLib` NuGet package version the project compiles against. The `SyncManifestDependencies` MSBuild target updates this automatically during build, but `min_game_version` still requires manual review.
- Only one RitsuLib package may be active in `flametail.csproj` at a time (mainline `STS2.RitsuLib` or a compat package).

### Asset profiles

Prefer `AssetProfile` properties (e.g., `CardAssetProfile`, `CharacterAssetProfile`) over legacy `Custom...Path` overrides. Unspecified character assets fall back through `PlaceholderCharacterId`.

## External documentation

For full setup, the version compatibility matrix, manifest field reference, and placeholder asset guidance, see `README.en.md` (English) and `README.md` (Chinese). RitsuLib tutorials and API examples are linked from those READMEs.
