# Syms

Standalone Unity C# source for a life-simulation and open-world RPG inspired by
The Sims and The Elder Scrolls IV: Oblivion.

This repository is intentionally **not** a Unity project. It targets Unity 6.6+
and is designed to be consumed from a Unity project's `Assets` or `Packages`
folder.

## Layout

```text
src/                     Production Unity C# source
  Syms/                  Runtime assembly root
tests/                   Unity Test Framework tests
  EditMode/              Fast deterministic/unit tests
  PlayMode/              Engine lifecycle and integration tests
docs/                    Architecture and design notes
```

The assembly definitions are included so these folders can be linked or copied
into a Unity project while preserving assembly boundaries.

The current simulation core includes variable-sized 3D traversal cells, blocked
portal routing, analytical off-screen travel, streaming authority handoff,
personality-driven activities, facility capacity, and an event-driven city clock.

The town itself is authored, and its persistent spatial topology is baked before
runtime. Buildings determine homes, facilities, and employment opportunities;
the seed initializes the population occupying that fixed town. See
[`docs/WORLD_AND_TOWN_GUIDE.md`](docs/WORLD_AND_TOWN_GUIDE.md).

Authored building definitions and placed instances drive facilities, employment
role slots, contracts, and work candidates. A root building/site may manage one
level of child buildings: whole apartments or sublets, hotel or motel rooms/suites,
and jail-cell groups. A child building is not a room: it owns every scene-authored
room in that leased or managed space and may contain multiple bedrooms and shared
rooms. Authored bed slots—not duplicated capacity fields—determine how many
residents, guests, or inmates fit and which bedroom/cell belongs to each person.

Scene-authored room instances bind reusable room definitions to room-local
anchors, access rules, facilities, and optional job positions. Per-property room
graphs use doors and gates for physical openings, while unfenced playgrounds,
sports fields, parks, and other outdoor spaces use continuous boundary/junction
portals. The 1800s catalog supplies indoor and outdoor archetypes without
generating town geometry.

Stateful authored objects add multi-affordance furniture, exact use-slot
reservations, health, cleanliness, conditions, ownership, assigned sleepers,
keys, locks, and topology-linked doors. Object-use utility distinguishes public,
private, hotel, institutional, hospital, jail, bunk, shared-bed, and emergency
sleeping contexts without random selection.

Hotel and motel child buildings are leased and assign their beds for the stay.
Occupied jail-cell children assign authored bunks and create timed meal duties;
only on-duty guards with the corresponding authored job duty can deliver them.

Off-screen NPCs retain only logical room/place or transit cell state, tasks, and
route progress. Exact Unity transforms exist only while materialized;
`LastObservedPose` is a disposable visual-continuity cache.

Sims also carry fixed authored age and gender data. A centralized relationship graph
tracks partners, ex-partners, directional family roles, and relationship affinity
from `-100` to `+100`. Data-driven bias rules can alter perceived affinity for
social decisions without overwriting the underlying relationship history.
Immediate household, roommate, workplace, and same-block neighbour circles are
reconciled as sparse records; unrelated townspeople are not allocated pair data.
NPCs without an active residence are homeless. Configurable analytical hardship
rates make homelessness and unemployment increase poverty and related needs.
Ordinary simulation never ages, relocates, births, kills, marries, or divorces
NPCs; only explicit scripted/orchestrator commands can change those stable facts.

`Syms.Population` provides deterministic UID generation, authored NPC and quest
overrides, household and homelessness allocation, building-role assignment,
sparse starting relationships, reducer-backed state, and lazy Redux-style
selectors for identity, household, workplace, block, and social queries.

## Validation

Run edit-mode and play-mode suites with the Unity Test Framework in the consuming
Unity project. This repository does not commit Unity-generated solution or project
files.

The engine-independent code and edit-mode tests can also be compiled outside
Unity with the authored standalone projects:

```sh
dotnet test tests/Syms.Tests.csproj
```
