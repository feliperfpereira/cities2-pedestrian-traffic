# Cities 2 Pedestrian Traffic

Small, focused traffic-light mod for **Cities: Skylines II**.

## What it does

Configure only the intersections you want. The mod adds two per-intersection options:

- **Pedestrian-only phase** — all pedestrian crossings share a dedicated signal phase. All vehicle lanes are red during this phase. The phase has a fixed minimum duration of about **10 simulation seconds** and is demand-driven, so it is not inserted just to stop traffic when no pedestrian requests it.
- **Free right turn** — right-turn lanes may proceed with **Yield** during other vehicle phases instead of waiting for their original green. They are **Stop** during the pedestrian-only phase.

Free right turn automatically enables the pedestrian-only phase. Disabling the pedestrian-only phase also disables free right turn.

The mod does **not** change left turns, lane arrows, signal timings for normal vehicle phases, roundabouts, movable bridges, or level crossings.

## In game

1. Click the small traffic-light button (`🚦`).
2. Click an existing ordinary road traffic light.
3. Toggle **Fase só para pedestres** and/or **Direita livre**.
4. Close the panel and keep playing.

Only the two selected flags are stored in the save. Lane signal groups are derived again from the vanilla intersection after road edits/load, instead of persisting fragile runtime group ids.

## Build and install for local testing

This is an official-toolchain code mod, so the machine building the DLL must have Cities: Skylines II and the official Modding Toolchain installed. The game/toolchain provides `Game.dll`, `Mod.props`, and `Mod.targets`; those proprietary game assemblies are intentionally not committed to this repository.

On Windows, from the repository root:

```powershell
.\build.ps1
```

The script:

1. reads `CSII_TOOLPATH` and `CSII_USERDATAPATH` from your user environment;
2. builds/deploys the C# mod with the official toolchain;
3. installs the UI dependencies when needed;
4. builds/deploys the UI module;
5. places the result under the local Cities: Skylines II `Mods\Cities2PedestrianTraffic` folder.

Then restart/open Cities: Skylines II and enable **Cities2PedestrianTraffic** in the active playset.

## Alpha test checklist

- Select an intersection with pedestrian crossings and enable only **Fase só para pedestres**.
- Wait for pedestrians: verify that a dedicated phase occurs where every vehicle movement is stopped.
- Verify the phase remains active for roughly 10 simulation seconds.
- Enable **Direita livre** and verify right-turn vehicles can proceed in Yield during other vehicle phases.
- During the pedestrian-only phase verify those right turns stop.
- Edit/rebuild the intersection and verify the options remain selected while signal groups are recalculated.
- Save, reload, and verify the selected options persist.
- Disable both options and verify the intersection returns to vanilla behavior after reinitialization.

## Architecture

The mod deliberately does not replace the full vanilla traffic-light simulation. It runs narrowly around the existing systems:

- before vanilla initialization: discard old derived runtime masks after a road edit;
- after vanilla initialization: add the pedestrian-only group and free-right memberships;
- before vanilla simulation: keep the pedestrian-only phase requested for its minimum duration;
- after vanilla simulation: downgrade mod-added right-turn greens to Yield.

This keeps vanilla responsible for normal phases, yellow/transition behavior, priority and the rest of the intersection simulation.
