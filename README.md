# Pedestrian Crossing Toolkit

Version 2.0.1 is released on Steam Workshop item `3735259302` and as matching
clean public source.

## Scope

PCT provides managed pedestrian crossings for:

- surface road crossings;
- signal-controlled crossings;
- automatic and manual pedestrian subways;
- pedestrian bridges;
- observed citywide Auto Scan proposals.

Version 2.0.0 adds a native Roads > Crossing page with redesigned icon-only
placement controls, vanilla road-style image tooltips and Auto Scan. The six-tile
artwork uses a consistent road-and-structure language, including a Bridge icon
that clearly shows a raised roofed route crossing over the road. Each tile uses
the game's stock Roads tooltip layout with an embedded preview cropped from the
supplied in-game crossing images; Auto Scan uses a four-crossing overview made
from the same set. Auto Scan asks whether to preview its results:
Yes opens proposal review controls and No applies the plan directly after the
scan; closing the dialog or pressing Escape cancels before observation starts.
The floating PCT Tool and its standalone or UnifiedUI launcher are
removed. The confirmed Clear All
Crossings action now lives in PCT Options rather than the floating manager. A staggered timed
read-only scan now checks each registered crossing without rebuilding or
changing it, warns the player when action is needed, and marks affected
crossings in Roads > Crossing until that tab closes or the crossing is removed
and rebuilt. Vanilla Bulldoze can remove an individual PCT crossing without
demolishing its supporting road. While
Roads > Crossing is selected, every visible PCT crossing automatically shows a
type billboard from city scale or detailed crossing and live signal-phase
information below that scale; detailed summaries hide behind modal or overlapping UI and return when unobstructed. Support remains available through the public bug tracker and normal
game logs rather than the removed legacy Info snapshot. Detailed diagnostics are available through a default-off
`Enable advanced logs` option without changing crossing behavior; enabling it
in any registered ScratchyBald scan participant also enables the shared
manager's routine diagnostics. The exposed bridge deck roof, bridge
access/stair roofs and subway canopy roof follow the game's rain, retained
wetness and snow presentation while sheltered and road-integrated surfaces
retain their normal appearance.

Version 2.0.1 preserves that complete workflow while making the static Crossing
tile layout event-driven, caching UI-occlusion discovery, sampling the road
upgrade warning target at a bounded rate, and making the selected Roads tab the
sole owner of Crossing-page visibility. These changes keep Roads > Crossing
responsive in heavily modded cities without removing or changing its tools,
summaries, warnings, placement, or removal behavior.

## Placement And Ownership

- PCT validates every placement, owns only its generated structures and registry, and removes them when their supporting network disappears.
- Vanilla owns roads, pedestrian/vehicle simulation, paths, and signal lifecycle.
- Supported road-replacement integrations use PCT's versioned compatibility transaction.
- Vanilla Bulldoze selects a PCT crossing anywhere along its route or generated access footprint and removes only that crossing through an approved narrow Harmony boundary; the supporting road is neither highlighted nor removed while the crossing owns the pointer, and all ordinary targets remain vanilla-owned.
- Bridge routing uses one thin, straight hidden tunnel as its sole cross-road route; its visual deck and stairs do not create a surface crossing beneath the bridge or additional underground loops.
- Every legal bridge placement remains accepted and builds two complete exits plus its functional route; access planning must fall back to the bridge's pavement landing rather than suppressing an exit or rejecting/removing the bridge.

## Choosing A Tool

- Use Standard for an ordinary road crossing.
- Use Signalled when a controlled crossing is appropriate.
- Use Auto Subway for a simple generated underpass.
- Use Manual Subway to choose access points.
- Use Bridge where above-ground clearance and geometry are valid.
- Use Auto Scan from Roads > Crossing, then choose whether to preview its citywide suggestions or apply them directly.
- Close or press Escape on the Auto Scan choice dialog to cancel without starting observation or creating crossings.
- Auto Scan shows a centred percentage progress box from observation-area preparation through final analysis and temporarily disables the Crossing-tab controls so the operation cannot be duplicated or interrupted by another PCT action.
- Auto Scan samples locally legal observation areas across every eligible road corridor at roughly 125-unit intervals, places suggestions near their own measured pavement activity, permits multiple suggestions on one corridor only at the normal 250-unit spacing, and shows an accounted completion summary after preview or direct apply.
- Leave `Enable advanced logs` off during ordinary play; enable it in PCT's Diagnostics options only when detailed scan, planning, geometry, validation or lifecycle evidence is needed.

## Current Limits

- Placement can be rejected where terrain, networks, nearby crossings, protected nodes, or geometry make a safe result impossible.
- Generated structures are intentionally conservative around complex junctions and incompatible networks.
- Generated roof weather presentation is visual only and does not change crossing geometry, simulation or lifecycle.
- A city can persist up to 65,536 PCT crossings; all registered crossings participate in rebuild, cleanup, validation, suppression, overlays and citywide Auto Scan observation, while each Auto Scan intentionally proposes at most 100 changes.

## Required Dependency

- Harmony 2.2.2-0, Workshop item `2040656402`.

## Development

- Active work and UAT: `ISSUES_AND_PLANS.md`
- Integration rules: `DESIGN_PRINCIPLES.md`
- Road-replacement API: `PUBLIC_API.md`
- Historical evidence: `Archive/`
- Release gate: `STEAM_RELEASE_CHECKLIST.md`

Run `./build+deploy.sh` from this folder after code changes.

## Copyright and intellectual property

Copyright © 2026 ScratchyBald. All rights reserved.

This repository is published for source transparency and reference only. No
licence is granted to copy, modify, compile, distribute, repackage, republish,
or incorporate its code or documentation into another project without prior
written permission, except as permitted by applicable law and GitHub's Terms of
Service.

**Pedestrian Crossing Toolkit** and its associated original branding identify a
ScratchyBald release. They may not be used in a way that falsely suggests
authorship, endorsement, or affiliation. Original concepts and functionality
are claimed only to the extent protected by applicable law.

Cities: Skylines and related marks are the property of their respective owners.
This independent community modification is not affiliated with or endorsed by
Colossal Order or Paradox Interactive.
