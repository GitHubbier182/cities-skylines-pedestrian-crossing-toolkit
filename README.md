# Pedestrian Crossing Toolkit

Version 2.0.3 is released on Steam Workshop item `3735259302` and as matching
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

The v2.0.1 candidate preserves that complete Roads > Crossing workflow while
making its static tile layout event-driven, caching the UI-occlusion snapshot,
sampling the road-upgrade warning target at a bounded rate, and making the
selected Roads tab the sole owner of Crossing-page visibility.

Released v2.0.2 consolidates generated subway-entrance and roof-weather updates so
unchanged presentation creates no per-renderer frame work. Automatic supporting
network checks retain the same removal rules while advancing one saved crossing
per rendered update, avoiding a whole-registry main-thread burst as cities grow.

Version 2.0.3 makes the saved crossing count and every successful crossing
placement part of normal support logging. Detailed geometry and planning traces
remain behind the optional advanced-log setting. It also captures a recognised
crossing-removal click before reflected vanilla targets are cleared, retains
that capture through mouse-up even if the boundary then fails, contains overlay
failures once, and uses a world-space candidate index before exact screen-space
selection so Bulldoze hover work does not scan every saved crossing each frame.
PCT now requires the Roads surface to remain visible and both Roads and Crossing
to be the selected native pages, so opening UPG, FLM or another top-level
workspace closes PCT's tools, overlays and diagnostic measurement immediately
rather than leaving hidden Crossing work. Passive detail repaint also keeps its
grade-separated state query silent instead of producing geometry logs per frame.
PCT-owned targeted path removal now detaches immediately and queues exact native
release without waiting; one segment is released per simulation-frame callback.
Bulk Auto Scan upgrades and other targeted removals therefore neither mutate
vanilla manager update sets during iteration nor block the calling UI/scan path.
Large Auto Scan construction builds one immutable changed crossing per
cooperative frame slice and withholds completion until the batch finalizes, so
a complete replacement set is not constructed in one rendered frame.
Managed signals use a rotating bounded scheduler: no rendered update performs
more than two pedestrian-demand scans, active safety phases take priority, and
no simulation frame defensively reasserts more than four controllers. Nearby
road arms are resolved through the native segment grid, and newly built signal
controllers enter the scheduler without an immediate per-controller scan.
With advanced logs enabled, v2.0.3 also records a rolling closed-tab baseline,
Crossing-tab open and close transitions, five-second open-tab frame-rate windows
and the measured time spent in PCT's Roads-tab, overlay and main update callbacks.
Crossing markers retain their original far-zoom ceiling. Close passive detail
panels are prioritised nearest the pointer, decluttered and capped to a
screen-derived maximum. The complete graphical live signal display—vehicle and
pedestrian signal bodies, coloured lamp states, and its phase, demand, path and
ownership information—is retained in a bounded reusable Colossal UI panel pool;
panels move without per-repaint reconstruction, static facts refresh at a paced
interval, and live labels and lamp colours change only with their source state.
It remains required player-facing functionality. Inspect Crossing still exposes any
exact crossing. Signal demand trusts the native citizen grid whenever it is valid, so
an empty local result no longer triggers a periodic whole-citizen-buffer scan.
This diagnostic is observational only and emits no normal-log traffic.

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
