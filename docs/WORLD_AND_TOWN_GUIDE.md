# Authored Town, Baked World, and Population Guide

This document defines the source of truth for the Lyfe world model. These are
architectural constraints, not optional implementation suggestions.

## 1. Authored town is authoritative

The town is built by designers in the map. Runtime code must not generate its
layout, buildings, roads, rivers, parcels, or civic services from population
targets.

The authored map determines what can exist:

- authored usable beds determine residential capacity inside houses and managed
  residential child buildings;
- buildings determine facilities, services, ownership, and employment roles;
- world cells plus authored gateways, doors, stairs, bridges, and paths determine connectivity;
- room graphs and room-local anchors determine where interior activities occur;
- placed civic, commercial, religious, agricultural, and industrial buildings
  determine which occupations the town can support.

If the map contains no smithy, it has no blacksmith role unless a designer adds
another authored source for that role. Population initialization never invents a
building or job to meet a demographic quota.

## 2. Building-derived roles and facilities

Each authored building instance refers to a `BuildingDefinition` and supplies a
stable instance ID. Its definition or instance overrides provide:

- root management or direct-child occupancy policy;
- logical facilities such as beds, privies, desks, counters, forges, stables, and
  workbenches;
- activity and materialization anchors;
- authored room graphs and explicit exterior gateway sockets;
- operating hours and recurring schedule templates;
- ownership rules;
- employment role slots;
- services and production capabilities;
- optional quest and faction tags.

Building hierarchy has exactly one optional parent edge. Standalone houses are
residential root buildings. Apartment blocks, hotels, motels, inns, and jails are
root managers whose apartments, rentable rooms/suites, and detention cells are
direct child buildings. Grandchildren are invalid. The child `BuildingId` is the
residence, lease, or custody identity; there is no separate residence-unit type.
Each child may own any number of scene-authored rooms. An apartment or sublet can
therefore contain several bedrooms, a kitchen, halls, and living spaces; no room
is marked primary and Lyfe never infers that the child is a single-room unit.
A bedroom is just a private sleeping room for the person or people assigned to
its beds. It is not another residence, sublet, hierarchy level, or lease target.

Resident capacity is never copied into building metadata. It is derived from the
usable sleeping slots of authored beds in that building. Assigning a person to a
bed determines their bedroom or cell. This supports parents sharing one bedroom,
children occupying another, multi-bed lodging, and multiple inmates in one cell.

A role slot instantiates an employment opportunity. A job definition describes
how someone performs that role, but does not cause the role to exist.

```text
Authored building instance
  -> root or child building role slot
  -> employment contract
  -> assigned NPC
```

Recommended data separation:

- `JobDefinition`: stable string ID, title, category, qualifications, duties,
  behavior weights, tools, clothing, and compensation rules.
- `BuildingRoleSlot`: building instance, job ID, capacity, work anchors, schedule
  template, and local overrides.
- `EmploymentContract`: assigned Sim, role-slot ID, start time, performance,
  attendance, compensation, and Sim-specific overrides.
- `DutyDefinition`: the available work activities and required facilities.

Stable IDs such as `jobs.trade.blacksmith.master` are the saved and authored
identity. Closed enums may categorize jobs but must not be the extensibility
boundary.

## 3. Seeded population bootstrap

The seed initializes people within the fixed authored town. It may determine:

- given names, family names, titles, and epithets;
- households and family relationships;
- ages and other demographic attributes;
- appearances, personalities, traits, skills, moods, and starting needs;
- assignment among available homes and building-derived job slots;
- non-critical friendships, rivalries, patronage, and other social ties;
- starting inventories and economic variation within authored constraints.

The seed must not change map geometry, building inventory, spatial cells, job-slot
counts, or quest-authored facts.

Use deterministic substreams derived from stable keys rather than consuming one
global random sequence:

```text
world seed / population
world seed / building / inn.west / household.owner
world seed / household / house.17 / child.1
world seed / npc / npc.generated.house.17.2 / personality
```

Adding a name or unrelated building must not reshuffle every existing NPC.

Bootstrap order:

1. Load baked world data and authored building instances.
2. Register residences, facilities, anchors, and building-derived role slots.
3. Apply authored NPCs and quest-critical reservations.
4. Apply partial NPC overrides such as fixed name, household, job, or traits.
5. Form or complete households within available residential capacity.
6. Generate missing NPC identity and personality data from deterministic streams.
7. Assign eligible residents to remaining building-derived role slots.
8. Create explicit family, partner, quest, friendship, and rivalry relationships
   between concrete Sim IDs.
9. Reconcile immediate social circles so every household/roommate, workplace,
   and same-block neighbouring pair has a sparse relationship record.
10. Produce schedules, initial locations, inventories, and activities.
11. Validate the result and report vacancies, overcrowding, missing anchors,
    impossible quest roles, and other unsatisfied constraints.

Vacant roles are valid. The bootstrapper must report them rather than inventing
buildings or silently assigning impossible work.

## 4. Authored overrides and RPG integration

Generated and authored NPCs use the same runtime model. Authored data may provide
a complete NPC or patch selected fields of a deterministically generated NPC.

Quest integration uses stable IDs and controlled interfaces:

- `NpcTemplate` defines a fully or partially authored person.
- `NpcOverride` patches a generated or authored NPC by stable ID or reserved role.
- `QuestRoleReservation` reserves a specific building role, household position,
  or NPC before procedural filling.
- `WorldEvent` reports simulation facts such as arrival, crime, death, work
  absence, ownership transfer, or witnessed behavior.
- `WorldFact` stores persistent RPG knowledge and world state.
- `SimulationDirective` requests or biases behavior without bypassing spatial,
  capacity, or safety validation.

Quests must not directly mutate internal dictionaries or teleport Sims through
invalid topology. They issue commands through simulation services and observe
events produced by those services.

## 5. Compile-time spatial bake

The full authored map may cover approximately 5 km by 5 km. Runtime startup must
not scan its geometry, voxelize it, flood-fill it, or build its persistent
octree. Those operations belong to a Unity Editor or standalone world-compiler
step.

```text
Authored map and building instances
  -> world compiler
  -> chunk and block partition
  -> persistent octree
  -> exterior traversal cells and portals
  -> room-local anchors and per-building room graphs
  -> validated, versioned topology blobs
```

The bake emits:

- a world manifest containing format version, content hash, bounds, and chunk
  index;
- a resident high-level chunk/block routing graph;
- independently loadable chunk topology blobs;
- flat baked-octree node arrays;
- stable exterior cells, room gateways, anchors, blockers, travel modes, and costs;
- building-instance references and connection sockets;
- validation diagnostics suitable for CI and the Unity Editor.

The baked octree is stored in compact linear arrays such as:

```text
BakedOctreeNode
  bounds
  firstChildIndex
  childCount
  firstCellIndex
  cellCount
```

Persistent IDs come from the bake and remain stable across runtime sessions.
Runtime octree allocation order is never used as save identity.

Startup is proportional to the manifest, coarse graph, and initially relevant
chunks. Detailed topology loads only for active or queried regions.

## 6. Runtime topology overlay

Runtime world changes are represented as localized overlays on the immutable
baked topology:

```text
effective topology = baked topology + saved runtime overlay
```

Overlay changes include destroyed or constructed buildings, collapsed floors,
removed walls, broken bridges, locked doors, rubble, hazards, and temporary road
closures.

Changes must:

- update only affected blocks and portals;
- invalidate facility reservations and activities made impossible by the change;
- wake and relocate affected Sims using explicit recovery rules;
- invalidate only routes whose dependency set intersects the changed region;
- remain serializable as part of the saved game;
- never trigger a full-map rebuild.

Constructible buildings should ship with pre-baked `BuildingTopologyStamp` data
containing room graphs, room-local anchors, gateways, facilities, and exterior
connection sockets. Placement transforms the stamp, connects valid sockets, and
adds a localized overlay. Freeform destruction may rebuild an affected leaf
region asynchronously, but never the entire world.

Use regional revisions rather than one global topology revision:

- bake-format and world-manifest version;
- chunk revision;
- block revision;
- connection or portal revision;
- runtime-overlay revision.

Routes record dependencies on the regions and connections they use. An altered
house must not invalidate unrelated routes on the opposite side of town.

## 7. Runtime spatial indexes

The persistent baked world octree and runtime indexes serve different purposes.

- `BakedWorldOctree` indexes immutable or overlay-adjusted map topology.
- `DynamicSpatialIndex` indexes NPCs, carts, dropped items, temporary hazards,
  projectiles, and other moving entities.

A loose runtime octree may serve the dynamic index. It must not rebuild or replace
the persistent world topology, and its nodes are never saved as world identity.

## 8. Performance and validation requirements

- No full-map geometry analysis during normal startup.
- No per-frame tick for every off-screen Sim.
- No global route invalidation for a localized topology change.
- No building or job invention during population bootstrap.
- Bake output must be deterministic for identical source content.
- Population output must be deterministic for identical baked content, seed, and
  overrides.
- Every building role, facility, anchor, route dependency, authored NPC, and
  quest reservation must be validated before play.
- CI should compile the runtime, run deterministic bootstrap tests, validate a
  representative baked town, and benchmark loading plus 10,000-Sim advancement.

## 9. Planned implementation boundaries

The intended subsystem split is:

- `Syms.World.Baking`: editor/CLI compiler and validation; never in player runtime.
- `Syms.World.Baked`: compact immutable manifest and topology data contracts.
- `Syms.World.Runtime`: chunk loading, effective topology view, and overlays.
- `Syms.Buildings`: authored building instances, facilities, roles, and stamps.
- `Syms.Population`: deterministic NPC and household bootstrap over the fixed map.
- `Syms.Jobs`: definitions, building role slots, contracts, duties, and schedules.
- `Syms.Rpg`: world events, facts, quest roles, NPC overrides, and directives.
- `Syms.Unity`: scene, NavMesh, animation, and materialization adapters.

The present code is an early runtime model. Future changes should move it toward
these boundaries rather than extending runtime topology construction or treating
the town itself as procedurally generated.

## 10. Current implementation status

### Room definitions and placed rooms

`RoomDefinition` is a reusable semantic archetype: category, environment,
default access, supported activities, suggested facilities, and tags. The
`HistoricRoomCatalog` provides an extensive 1800s-oriented starting vocabulary
covering domestic rooms, mansions, schools, orphanages, inns, hotels, period
roadside lodging, taverns, police, courts, hospitals, morgues, graveyards,
almshouses, retirement homes, churches, commerce, agriculture, industry,
transport, entertainment, and public points of interest. Projects may extend or
replace the catalog through stable string IDs.

Only a room volume defined in a Unity scene on a named culling layer makes a room
exist. The editor bake assigns its stable `RoomId`, emits a `RoomInstance`, and
places it as a node in its building's directed room graph. It may supply:

- a stable root building/site or one direct child building; parks, squares,
  playgrounds, sports fields, and graveyards use authored site identities even
  when they contain no enclosed structure;
- exact room-local anchor coordinates resolved by the loaded Unity scene;
- logical facility placements and capacity;
- access classification and quest/content tags; and
- optional building-owned employment positions, schedules, and work anchors.

Suggested facilities in a definition never create runtime capacity. A placed
facility does. Likewise, only placed room positions create job slots, and those
positions require an authored parent building.

Room instances, facilities, jobs, and furniture never store interior `CellId`
references. Only exterior gateway sockets bridge a building graph to the baked
world cell/octree topology. Room anchors are possible materialization and interaction points, not stored
off-screen NPC transforms. When no player observes an NPC, authoritative state
is its logical `RoomId`/place and activity, its interior room path/gateway progress,
or its exterior transit cell and route progress. Exact world position becomes tangible only while materialized.
`LastObservedPose` is a continuity cache for an unchanged activity, route, and
topology; abstract movement or state changes invalidate it.

The scene is also authoritative for gateway adjacency and physical costs. Editor
extraction identifies the rooms on each side of a door, window, stair, gate, or
opening; queries the baked NavMesh for portal-to-portal path lengths; and surveys
window drops and landing surfaces. `SceneBuildingGraphCompiler` converts those
facts into deterministic runtime blobs. Player startup only loads the bake and
never scans scene geometry, rebuilds NavMesh paths, or derives the room graph.

An unfenced outdoor adjacency is not authored as a fake gate. It bakes as an
`OpenBoundary` or `Junction` gateway whose portal geometry is a segment or area
covering the traversable edge. A real fence or wall entrance—such as a gated
graveyard—uses a physical `Gate` gateway and may be locked, blocked, damaged, or
closed through its bound object state.

### Interactive objects, furniture, and doors

`InteractiveObjectDefinition` describes a reusable object archetype. An object
may expose multiple independent affordances and slot counts. For example, the
default couch provides three sitting slots and one lower-quality emergency sleep
slot; a bunk bed provides two sleep slots; and a shared double bed provides two
sleep slots with a shared-bed social profile.

Only an authored `AuthoredObjectInstance` makes the object exist. A placement
binds its stable ID and definition to a room, building, room-local position or
anchor, ownership, assigned Sims, initial health and cleanliness, optional lock,
and optional gateway bindings. Runtime state is held by
`ObjectStateStore`, which supplies indexed room/object queries, deterministic
stable affordance-slot IDs, exact reservations, condition changes, damage, and
repair.

If damage makes an object unusable, its exact reservations are released, active
users are interrupted and woken for replanning, observed poses are invalidated,
and a door immediately resynchronizes its topology blockers.

Object access and preference are deliberately separate:

- hard rules cover public, owner, household, guest, customer, staff, resident,
  prisoner, restricted, and keyed access;
- soft utility covers comfort, privacy, cleanliness, contamination, sleeping
  arrangement, and whether the bed is personally assigned;
- family and partners receive different sleeping penalties from strangers;
- critical energy or bladder need may reduce social aversion or allow an
  explicitly marked emergency trespass, with a consequence penalty; and
- jail, hospital, institutional, hotel, bunk, couch, shared, and private beds
  remain distinct authored arrangements.

Selection is deterministic. When a concrete object affordance exists in the
current room, Sims cannot bypass it through generic room capacity. The chosen
activity reserves both logical facility capacity and the exact object slot.
`Sim.AccessClaims` stores stable keys, guest/customer/staff/prisoner grants, and
similar permissions supplied by gameplay or quest systems.

During seeded population bootstrap, authored bed assignments are preserved and
remaining residential private, shared, bunk, and institutional sleep slots are
assigned deterministically to residents of the same authored building. The
bootstrap reports housed residents without a bed; it never creates furniture to
hide a shortage. Couches and hospital cots remain contextual facilities. Hotel
beds are assigned for the duration of a child-building lease, while jail bunks
are assigned for the duration of custody; neither is permanent bootstrap housing.

An occupied detention-cell child building creates deterministic meal-delivery
obligations from its parent jail's authored schedule. Only an on-duty employee
whose job contains the `FeedPrisoners` duty may claim one. Content validation
requires guard-shift coverage for every meal window. A confined prisoner cannot
bypass this system with a generic abstract eat activity; completed delivery
applies the meal while missed obligations remain explicit saved state.

A portal-controlling object owns persisted open/closed, lock, and condition state
and names the building gateways it controls. An exterior entrance may additionally
name its world portal connection. Synchronization toggles only object-owned flags,
preserving hazards, obstructions, and other independent blockers. Closed unlocked
doors remain routable with their authored opening delay; locked, barred, jammed,
or obstructed gateways require a valid action or remain impassable.

Room-level facility placements remain useful for built-in fixtures or deliberate
aggregate capacity. Authors should not also place equivalent object affordances
unless the capacities are intentionally additive.

The first building-derived employment layer now exists:

- `AuthoredTownContent` stores authored job and building definitions plus placed
  building instances.
- `BuildingDefinition` declares reusable facilities and role templates;
  `BuildingInstance` occupancy plus authored beds determine housing.
- `BuildingInstance` supplies the stable map identity, address, enabled state,
  and explicit capacity overrides.
- `AuthoredTownRuntime` resolves placed buildings into residences and stable role
  slots while registering facility capacity.
- `EmploymentService` assigns a Sim to a vacant role and produces an
  `EmploymentContract`.

The deterministic population bootstrap now exists:

- stable typed Sim and household UIDs derive from seed plus content keys;
- authored NPC templates, partial overrides, reserved homes, and quest role
  reservations run before generated filling;
- household occupancy never exceeds authored residential capacity;
- excess target population may become homeless but never creates housing;
- adults fill only authored building role slots, with vacancies reported;
- household, family, roommate, coworker, and same-block neighbour links remain
  sparse and receive deterministic starting affinity;
- `PopulationState`, reducer actions, and revision-indexed selectors provide a
  Redux-style mutation/query boundary without copying the population; and
- city initialization imports validated output and resolves employment contracts.

Jobs use stable `JobDefinitionId` values exclusively. Repository-owned enum job
identities and compatibility mappings do not exist.

The population model also includes fixed authored age, configurable life stages,
gender, centralized partner and ex-partner history, directional family roles, and
an affinity slider from `-100` to `+100`. Optional social-bias rules are authored
data and affect effective affinity without permanently changing stored affinity.
Stable residence membership—not a Sim's current scene position—determines their
household and neighbourhood. Immediate social links are generated only within
family, household, workplace, and same-block neighbour circles; the system must
not allocate a relationship for every possible pair in the town.

Homeless NPCs are valid population results when no residence is assigned. Do not
invent a house to eliminate homelessness. Housing status is derived from the
residence assignment, and tunable economic pressure makes unemployed homeless
NPCs accumulate poverty and related health/need penalties faster. A job remains
building-derived and only pays through simulated paid work.

The populated town is a stable ecosystem after bootstrap. City-time advancement
must not age Sims, relocate their residence, reproduce, kill them, marry them, or
divorce them. Only an explicit scripted event or external orchestrator command
may change those facts. Routine walking, commuting, shopping/spending, work,
needs, moods, and relationship affinity changes remain ordinary simulation.
