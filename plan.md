# Immersive Quicklime for VS 1.22.2

## Summary
Implement this as a Vintage Story code mod for VS 1.22.2 using the Anego `VintageStoryMod` template structure and its CakeBuild / Cake Frosting packaging flow as the project baseline.

The repo currently only contains `modinfo.json`, so the work starts by turning it into a proper code-mod scaffold modeled after the `VintageStoryMod` template:
- SDK-style mod project
- `CakeBuild` project
- `build.ps1` / `build.sh`
- release packaging into the project `Release` folder
- compiled mod DLL included in the packaged mod zip

Gameplay-wise, the mod adds an immersive limestone-burning system:
1. Players fill a connected pit with loose limestone items.
2. They cap it with a domed grate block.
3. They add one valid pit-kiln fuel type to the grate.
4. They ignite it.
5. On successful completion, limestone converts into vanilla quicklime at 2 limestone -> 3 quicklime.

Vanilla quicklime production remains available and unchanged.

## Implementation Changes
### Project and build setup
- Reorganize the repo to match the Anego `VintageStoryMod` template layout as closely as practical for a single-mod repository.
- Add the code project, solution, CakeBuild project, and build scripts required to produce the mod DLL and packaged release zip.
- Use the template’s CakeBuild approach as the authoritative packaging flow rather than ad hoc scripts.
- Keep `modinfo.json` as the mod metadata source and ensure the Cake build includes it in the final package.
- Target the current game baseline the user specified: VS 1.22.2 and the corresponding current modding/runtime setup in use for that version.

### New gameplay objects
- Add a limestone pit controller block/entity pair responsible for connected-volume discovery, storage, validation, burn state, and output conversion.
- Add a domed grate block with its own block/entity behavior for placement, fuel loading, ignition, and break chance handling.
- Reuse vanilla quicklime as output; do not introduce a custom quicklime item.
- Add any required block/item JSON, lang entries, shapes, and textures for the new pit/grate system.

### Pit and fuel behavior
- Accept only loose limestone items as kiln input.
- Support a connected limestone pit up to 7x7x7.
- Implement custom stacked-storage behavior for limestone inside the pit so the structure can hold more than a simple ground pile.
- Require a valid top-capped structure with the grate present before ignition is allowed.
- Fuel behavior mirrors pit kiln allowed fuels, but each burn may use only one fuel type.
- Fuel choice determines burn duration using configured per-fuel durations.
- Mixed fuel types in the same batch are rejected with a clear interaction error.

### Burn lifecycle
- Burn sequence:
  1. validate pit geometry and cap/grate
  2. validate enclosure and required air/opening rules
  3. validate limestone contents
  4. validate single fuel type and quantity
  5. ignite
  6. run warmup/burn timer
  7. convert output on success
- Successful conversion uses whole recipe units only:
  - every 2 limestone yields 3 quicklime
  - unmatched leftover limestone remains unchanged
- If the structure becomes invalid during the burn:
  - the batch fails
  - fuel is treated as spent
  - limestone is preserved unless later balancing intentionally changes that
- Add smoke/light/state feedback consistent with vanilla pit kiln and charcoal pit expectations.

### Grate durability behavior
- The domed grate is reusable, but after each successful completed burn it has a 10% chance to break.
- Break chance is rolled once per completed batch.
- If it does not break, the grate remains or is recoverable for reuse.
- If it breaks, remove it cleanly and optionally support a broken/scrap drop only if that content is intentionally added.

### Configuration and data-driven balancing
- Keep balancing values data-driven where practical:
  - max pit size
  - accepted limestone codes
  - accepted fuel codes
  - burn duration per fuel
  - burn requirement per batch or unit
  - conversion ratio
  - grate break chance
- Follow the vanilla pit kiln JSON-driven pattern where feasible so later tuning is mostly content/config work instead of code refactoring.

## Public Interfaces / Types
Planned mod-local types:
- `ImmersiveQuicklimeModSystem` or equivalent entry system
- `BlockLimestonePit`
- `BlockEntityLimestonePit`
- `BlockDomedGrate`
- `BlockEntityDomedGrate`
- `LimestonePitFuelConfig`
- `LimestonePitProcessConfig`
- `EnumLimestonePitState`

Responsibility split:
- `BlockEntityLimestonePit` owns connected-pit discovery, limestone totals, structure validation hooks, batch state, and final conversion.
- `BlockEntityDomedGrate` owns grate interactions, fuel acceptance, selected fuel type, ignition, and grate break resolution.
- The mod system or config layer loads fuel/process settings from assets and exposes them to both entities.

## Test Plan
- The project builds through the CakeBuild flow and produces the expected packaged mod output.
- The packaged release includes the compiled DLL, `modinfo.json`, and required assets.
- Valid small and large pits up to 7x7x7 ignite and complete successfully.
- Pits exceeding the cap are rejected.
- Only loose limestone items are accepted.
- Only one valid pit-kiln fuel type may be used per batch.
- Mixed fuel types are rejected with a clear message.
- Fuel duration changes correctly by selected fuel.
- Successful burns convert at 2 -> 3 using whole recipe units only.
- Leftover unmatched limestone remains unconverted.
- Invalid top/grate/enclosure states prevent ignition.
- Mid-burn invalidation causes failure without quicklime output.
- Repeated successful burns show grate breakage at approximately the expected 10% rate.
- Vanilla quicklime production still works unchanged.

## Assumptions
- The requested Anego template reference is the baseline project structure and packaging model, not a requirement to copy every file verbatim.
- Vanilla quicklime remains the output and vanilla quicklime crafting remains untouched.
- The user wants a bulk immersive alternative, not a hard replacement for existing gameplay.
- The current repository will eventually contain a `plan.md` with this exact plan content once Plan Mode is exited and file writes are allowed.
