# Pedestrian Crossing Toolkit

Version 2.1.1 — matching public source for Steam Workshop item 3735259302.

PCT adds Standard, Signalled, Auto Subway, Manual Subway, Bridge and Auto Scan
controls to Roads > Crossing. Normal city-tool hover and selection show one
crossing's details; vanilla Bulldoze removes a crossing without its supporting
road. Clear All remains a confirmed action in PCT Options.

This update fixes the repeated UI lookup behind the reported FPS loss and reduces
crossing rendering and management overhead. It preserves complete crossing
visuals, improves road-join and long-route scanning, retains cleanup and bridge
orientation across saves, and strengthens signal and road-replacement recovery.
Signals wait for observed pedestrian clearance before releasing vehicles.

TM:PE is optional. Harmony 2.2.2-0 (Workshop item 2040656402) is required.
Road and pedestrian simulation and pathfinding remain game-owned. Original
crossing permissions already overwritten in older saves cannot be reconstructed;
the new saved ledger preserves originals captured by this version.

## Build reference

The project targets .NET Framework 3.5 and uses the installed game's managed
assemblies and CitiesHarmony.Harmony.dll. Supply GamePath/CitiesHarmonyPath as
needed for your installation. The six original tooltip images are encoded in
the project file and materialized into the intermediate directory during build.
Public version metadata uses 2.1.1.0 and a neutral update-notice build receipt.

## Support and source

[Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3735259302)

[Bug report](https://github.com/GitHubbier182/scratchys-cities-skylines-mod-support/issues/new?template=bug_report.yml)

When reporting a problem, include Player.log or output_log.txt and your mod list.

## Copyright and intellectual property

Copyright © 2026 ScratchyBald. All rights reserved.

This repository is published for source transparency and reference only. No
licence is granted to copy, modify, compile, distribute, repackage, republish,
or incorporate its code or documentation into another project without prior
written permission, except as permitted by applicable law and GitHub's Terms of
Service.

**Pedestrian Crossing Toolkit** and its associated original branding identify a ScratchyBald
release. They may not be used in a way that falsely suggests authorship,
endorsement, or affiliation. Original concepts and functionality are claimed
only to the extent protected by applicable law.

Cities: Skylines and related marks are the property of their respective owners.
This independent community modification is not affiliated with or endorsed by
Colossal Order or Paradox Interactive.
