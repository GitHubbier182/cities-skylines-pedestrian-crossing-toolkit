# Pedestrian Crossing Toolkit

Standalone Cities: Skylines mod for connector-based pedestrian crossing tools.

## Development Status

Version 1.1.0 is the current Steam Workshop release. The current baseline has
moved away from destructive road segment splitting and builds generated
connector structures. Core road placement behaviour, signal visuals,
grade-separated links, validation, Auto Scan preview/confirm, and the unified
in-game UI are implemented.

## Version 1.1.0 Update

- Adds optional Auto Scan preview/confirm so suggested crossings can be reviewed, individually rejected, applied in one batch, or cancelled before anything is built.
- Improves Auto Scan suggestion ordering so long straight roads are de-duplicated, busy standard crossings stage toward signals, existing PCT signals are left alone, and 3+ junction throats keep to one subway/bridge family once chosen.
- Adds an Auto Scan preview reminder with proposal icon and action-button previews, plus a don't-show-again option.
- Validate Crossings markers should now remain visible while the Toolkit is open so multiple invalid crossings can be fixed from one scan. This should be fixed, but it still needs confirmation from affected players with saves that contain invalid or stale PCT crossings.

## Version 1.0.4 Bug Fixes

- Improves launcher compatibility with UnifiedUI/UUI when launcher buttons are moved or managed by external UI tools.
- Carries forward the 1.0.3 surface marking and cleanup hardening fixes.

## Version 1.0.3 Bug Fixes

- Improves PCT-created standard surface zebra stripes with UV-mapped procedural off-white, semi-transparent, worn/feathered road-marking paint instead of a flat colour.
- Hardens unload/reset cleanup for generated Toolkit assets so mod reset and unload paths leave fewer stale generated objects or state behind.
- Hardens explicit `Clear All` cleanup so saved crossing state and generated crossing structures are removed together.

## Version 1.0.2 Bug Fixes

- Keeps road-upgrade cleanup scoped to crossings on the road segment that was actually upgraded or replaced, so neighbouring PCT crossings are not removed by nearby geometry changes.
- Removes crossings affected by road upgrades through targeted registry and built-asset cleanup instead of rebuilding every saved crossing, reducing large-city placement, deletion, and upgrade pauses.
- Adds a passive vanilla road-tool warning overlay for hovered road segments that already contain PCT crossings.
- Fixes Manual Subway placement near existing entrances so sharing one endpoint no longer deletes an existing subway; true duplicate two-endpoint routes still replace/update.
- Removes the stale signal-placement world billboard while preserving the accepted screen-space signal placement guide.

## Current Features

- Provides a unified in-game UI for Pedestrian Crossing Toolkit controls.
- Places simple surface crossings on suitable road segments.
- Places signal-controlled pedestrian crossings at existing road segment joins.
- Places compact subway links and pedestrian bridge links without splitting road segments.
- Places grade-separated subway and pedestrian bridge links over supported non-road targets, including rail-style targets.
- Builds generated crossing markings, signal stop lines, connector strips, bridge decks, subway spans, and compact polished access markers from saved placements.
- Keeps built structures synced when crossings are added, replaced, removed, cleared, saved, or loaded.
- Suppresses built vanilla surface crossing flags at blocked subway and bridge junction approaches where supported.
- Blocks simple and signalled surface crossings on roads without pedestrian or sidewalk lanes, while still allowing grade-separated links across highway-style roads.
- Provides remove mode and `Clear All` for cleaning up placed toolkit crossings and built structures.
- Provides a one-shot `Validate Crossings` health check for placed toolkit crossings, with red X markers that stay visible until the Toolkit is closed so multiple marked crossings can be fixed from one scan.
- Provides Auto Scan staged upgrades: one surface-stage crossing per continuous long road, busy standard crossings can upgrade to signals, existing PCT signal crossings are left alone, and 3+ junction throats keep to one subway/bridge family once chosen; optional preview mode shows a reminder with proposal icon/button previews, then lets players reject suggested crossings before applying the remaining batch.
- Provides an `Info` support snapshot that copies user/debug state and writes it to the dedicated toolkit log.
- Shows selected-crossing query/debug state for path links, suppression, signal phase, and owned assets.
- Saves and restores pending crossing markers with the city.

## Which Tool Should I Use?

- Use `Standard Crossing` for a simple visible pedestrian crossing where traffic-light control is not needed.
- Use `Signalled Crossing` at an existing road join where pedestrians need vehicles to stop on demand.
- Use `Auto Subway` when pedestrians should avoid crossing the road surface, especially on busy or highway-style roads.
- Use `Manual Subway` when you want to choose both subway entrance points yourself.
- Use `Bridge` when a visible grade-separated route is clearer, or when crossing supported rail-style targets.

## Placement Rules

- Surface crossings use separate connector geometry and do not split road segments.
- Signal crossings only manage traffic where the placement can snap to an existing road node.
- Mid-block signal traffic stopping is not enabled until a non-destructive node strategy is available.
- Subway and bridge placements can target junction approaches and highway-style roads, using road-edge landing points when sidewalk lanes are unavailable.
- Subway and bridge placements can target supported non-road linear networks without enabling surface or signal placement on those targets.
- Subway and bridge placements only suppress vanilla surface crossings when the
  placement resolves to a valid road node with a suppressible surface crossing.
- Toolkit UI interaction is shielded so clicks on the UI do not create crossings behind it.

## Tested / Resolved

- Unified in-game UI replacing the old separate launcher/panel direction.
- Grade-separated bridge and subway targeting for supported non-road networks.
- Non-road target rejection rules so unsupported networks fail cleanly.
- Mid-block surface crossing placement is working in UAT.
- Grade-separated road-position snapping now follows the raw hovered road point instead of jumping to distant candidates.
- Signal placement uses the lightweight 1:1 segment-join guide and click buffer.
- Signal stop lines now render as two full-road lines, one on each side of the vanilla zebra.

## Known Limits

- Pedestrian pathfinding through the generated connector network is not yet
  release-validated.
- Subway and bridge links remain subject to vanilla pathfinding choices. With
  TM:PE installed, the toolkit uses detected public pedestrian-crossing APIs to
  improve suppression of nearby vanilla surface crossings where available.
  Without TM:PE or similar traffic/path management installed, cims may not
  always prefer a nearby subway or bridge over an available surface route.
- Bridge and subway access pieces are compact generated structures, not custom art assets.
- Vanilla crossing markings may still be visible in some cases; the toolkit suppresses crossing flags where possible but does not repaint road materials.
- PCT surface markings can sometimes visually overlap or be obscured by vanilla parked vehicles, queued vehicles, road props, or road materials. This is a vanilla object/rendering limitation rather than something the toolkit can fix cleanly. The toolkit cannot stop vanilla parked cars from occupying those spots, so please consider those cims inconsiderate neighbours.
- Signal crossings currently use vanilla signal state plus Pedestrian Crossing Toolkit stop lines only.
  Some vanilla road joins can still produce the known VIS-03 blown-out visual
  state; this is parked for now rather than being patched further.
- TM:PE support uses detected public pedestrian-crossing APIs when available.
  Non-TM:PE behaviour is accepted as a vanilla pathfinding limitation rather
  than an active recode target.
- Concrete padding over central reservations and side margins can still be missing or incorrectly sized.
- Harmony-based traffic AI hooks were tested for signal crossings and binned
  after crashes / broken building selection in CS1 `1.21.1-f9`. Do not
  reintroduce `CarAI` or `HumanAI` Harmony patches unless a future game update
  clearly fixes the underlying mod compatibility problem.

## Build And Deploy

Build and deploy from this folder:

```bash
bash ./build+deploy.sh
```

## Steam / Workshop Release Prep

- Use `STEAM_RELEASE_CHECKLIST.md` as the release gate before any Workshop upload.
- Use `WORKSHOP_COPY.md` as the source for Workshop-facing description, known limits, mode guidance, screenshot planning, and tags.
- Run `bash ./release-privacy-scan.sh` before creating a clean export, and run it again against the final package folder.
- Release from a fresh scrubbed repository/export, not from this working repository history.

## Development Notes

- Validate pedestrian pathing and lane connectivity before preparing a Steam
  Workshop release.
- The private grade-separated suppression recode plan is closed. The accepted
  direction is TM:PE integration where available plus clear non-TM:PE
  pathfinding limits for players.
- Continue UAT on subway entrance depth, edge caps, and bridge access finishes across varied road widths and slopes.
- No further vanilla-only subway usefulness tuning is currently planned; keep
  the pathfinding limitation visible for players before Workshop release.
- Signal crossing traffic control should avoid Harmony AI hooks for now.
  Possible future routes are TM:PE vehicle restriction/timed-light integration
  if exposed, or a non-destructive invisible control node/gate that can hold
  vehicles without dropping the 1:1 segment-join signal-crossing plan.
- Revisit the signal crossing model only after an explicit redesign decision,
  or if a future CS1/Harmony compatibility change makes `CarAI` / `HumanAI`
  hooks safe enough to consider again.
- Continue expanding connector prefabs and compact access assets for subway and bridge links.
- Bridge/subway visuals now have more material separation and trim detail, but
  continue validating generated walls, roofs, thresholds, and end caps against
  varied road widths and slopes.
- Future aesthetics idea: detect pedestrians using enclosed bridge crossings
  and render visual-only fake cim silhouettes walking behind the bridge glass,
  timed to match the invisible crossing traversal.
- Review TM:PE attribution and licensing before reusing anything beyond observed public API behaviour.
