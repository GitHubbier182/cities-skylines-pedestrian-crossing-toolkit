# Pedestrian Crossing Toolkit

Version 2.1.0 was released on Steam Workshop item `3735259302` on
2026-09-03 and is published here as matching clean public source. It combines
selectable crossings and path-ranked Auto Scan with complete vanilla-crossing
exclusion and bounded saved-crossing preparation.

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
demolishing its supporting road. While Roads > Crossing is selected, visible
PCT crossings retain their type billboards at city scale. Version 2.1.0 adds
normal no-overlay hover and selection: a crossing receives its exact cyan
footprint before click, and selecting it shows the detailed crossing or live
signal-phase summary at any camera height. That selection is independent of
Roads > Crossing and clears like an ordinary native selection.
The selected summary hides behind modal or overlapping UI and returns when
unobstructed. Support remains available through the public bug tracker and normal
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

Version 2.1.0 removes the automatic close-up summary for every visible crossing
in favour of one explicitly selected crossing. Building-like no-overlay
selection enters through the normal `DefaultTool.OnToolGUI` gesture owner.
Because a PCT crossing
has no native `InstanceID`, the approved minimum Harmony boundary retains a
separate PCT asset selection and its existing summary while every non-crossing
gesture remains vanilla-owned. Its closed targets, compatibility boundary and
rollback are recorded in `DESIGN_PRINCIPLES.md`.

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
Crossing markers retain their original far-zoom ceiling. The complete graphical
live signal display—vehicle and pedestrian signal bodies, coloured lamp states,
and its phase, demand, path and ownership information—is retained in a reusable
Colossal UI panel; it moves without per-repaint reconstruction, static facts
refresh at a paced interval, and live labels and lamp colours change only with
their source state.
It remains required player-facing functionality. Inspect Crossing still exposes any
exact crossing. Signal demand trusts the native citizen grid whenever it is valid, so
an empty local result no longer triggers a periodic whole-citizen-buffer scan.
This diagnostic is observational only and emits no normal-log traffic.

## Placement And Ownership

- PCT validates every placement, owns only its generated structures and registry, and removes them when their supporting network disappears.
- Vanilla owns roads, pedestrian/vehicle simulation, paths, and signal lifecycle.
- Road replacement integrations use the versioned transaction in `PUBLIC_API.md`.
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
- Auto Scan first snapshots the remaining ready pedestrian path of every active on-foot citizen. Every eligible road arm at a 3+ junction contributes its exact calculated crossing line even when its segment end does not carry a vanilla crossing flag. Auto Scan counts each citizen at most once for the junction arm whose span the planned route traverses, ranks arms by distinct routed citizens and reserves up to 10 percent of all prepared observation areas for accepted junction bridge/subway proposals before any non-junction work. Different entry-road arms at one junction may each qualify; the same arm cannot be duplicated, and later signal-crossing upgrades do not count toward this junction budget.
- Auto Scan then samples existing crossings and locally legal straight-road observation areas at roughly 125-unit intervals, but every actual vanilla road crossing—including one at an ordinary two-segment join—excludes new standard proposals within 250 units. Saved-crossing candidate preparation is sliced into bounded 512-record updates. Junction proposals are bridge/subway only and start at that arm's exact calculated crossing line, so a short section between nearby vanilla crossings cannot receive a standard infill crossing. Genuinely long corridor interiors may still receive multiple measured-demand suggestions at the normal 250-unit spacing.
- Auto Scan has no fixed 60-second wait. Candidate discovery, native path capture and one current-demand sample advance in cooperative frame batches, followed immediately by analysis and the chosen preview or direct-apply workflow.
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
