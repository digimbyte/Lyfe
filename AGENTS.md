# Syms contributor instructions

## Project intent

Syms is a life-simulation and open-world role-playing game codebase inspired by
The Sims and The Elder Scrolls IV: Oblivion. The code in this repository targets
Unity 6.6 or newer, but this repository is not a complete Unity project.

Do not add Unity-generated or project-level directories such as `Assets/`,
`Library/`, `Logs/`, `Packages/`, `ProjectSettings/`, `Temp/`, or `UserSettings/`
unless the user explicitly asks to convert this repository into a Unity project.

## Hard platform rule

- Never install, launch, invoke, or attempt to validate with Unity on macOS for
  this repository. This includes Unity Hub, the Unity Editor, Unity batch mode,
  and the Unity Test Framework.
- Do not search for a local Unity installation or treat its absence as a blocker.
- Keep scene ingestion and editor baking behind engine-neutral contracts in this
  repository. Their Unity adapters are compiled and run only in the separate
  consuming Unity project on an approved development platform.
- On macOS, validation is limited to repository-native static checks and plain
  .NET compilation/tests when an already-installed .NET SDK is available. Never
  install an SDK merely to validate this repository.

## Hard development-stage rule

- This project is under active development and is not deployed in any live or
  production environment.
- Do not preserve legacy APIs, schemas, fields, adapters, compatibility aliases,
  fallback behavior, or migration shims unless the user explicitly requests a
  specific compatibility boundary.
- When the model changes, migrate all repository-owned callers, tests, fixtures,
  serialization models, and documentation to the new design in the same change.
- Prefer clean breaking refactors over parallel old/new implementations. Remove
  superseded code after its callers have been migrated.
- Migration and refactoring are expected. Do not describe compatibility with
  obsolete repository behavior as a benefit or treat breaking internal changes
  as a blocker.

## Non-negotiable world model

- The town and its map are authored. Never procedurally generate the town layout,
  buildings, roads, rivers, chunks, or employment demand at game startup.
- Buildings placed in the authored map define available homes, facilities, job
  roles, schedules, services, and activity anchors.
- Building hierarchy is exactly one level deep. A root building/site may own and
  manage direct child buildings; grandchildren are invalid. Apartments, hotel
  rooms/suites, motel rooms, and jail cells are child buildings managed by their
  parent property.
- Never introduce a parallel residence-unit abstraction. A standalone house or
  residential child `BuildingId` is the residence identity. Hotels and motels
  lease their residential child buildings; jails assign custody to detention-cell
  child buildings.
- A child building is the complete authored apartment, sublet, suite, or cell
  group and may own multiple rooms. There is no primary-room concept; scene room
  membership and assigned beds are authoritative.
- A bedroom is only an authored private sleeping room for its assigned person or
  people. Do not create another unit, hierarchy, or ownership abstraction for it.
- Authored usable bed slots are the sole resident/cell capacity authority. Bed
  assignments determine each person's bedroom or jail cell, including shared
  parental rooms, children's rooms, multi-guest lodging, and multi-inmate cells.
  Do not duplicate bed capacity in building or room metadata.
- An occupied detention cell creates scheduled meal-delivery obligations. Only
  an on-duty employee whose authored job contains the feed-prisoners duty may
  fulfill them; prisoners cannot satisfy hunger through an abstract local eat
  activity. Jail content must provide meal schedules and matching guard coverage.
- Room definitions are reusable semantics only. Placed room instances bind those
  semantics to room-local anchors, facilities, access, and optional building-owned
  job positions. Building interiors route through authored room graphs; rooms never
  store world cells or generate map geometry.
- Unity scene room volumes on named culling layers are authoritative. The editor
  bake generates `RoomInstance`, room adjacency, gateway arcs, NavMesh distances,
  and window fall/landing data; runtime must never rediscover them.
- Interactive object definitions expose one or more affordances, but only
  authored object instances create furniture, fixtures, doors, exact use slots,
  health, cleanliness, locks, ownership, or assigned users in a town.
- Keep hard access separate from preference. Ownership, keys, household/guest/
  staff/customer/prisoner claims, and locks decide whether use is allowed;
  comfort, privacy, cleanliness, sleeping arrangement, relationships, and need
  desperation adjust deterministic utility. Never select object use randomly.
- Personally assigned beds remain distinct from household ownership. Permission
  to use a bed does not remove the social/comfort penalty for using somebody
  else's bed, though critical need may reduce that penalty or permit explicitly
  authored emergency trespass.
- Population bootstrap may assign residents to remaining authored residential
  bed slots deterministically, after preserving quest/authored assignments. It
  must report shortages and never invent beds or other furniture.
- Door and window objects control only their owned flags on authored building
  gateways and exterior portal connections. Preserve unrelated blockers such as
  hazards, obstructions, walls, rivers, or destination closures.
- Unfenced outdoor rooms such as playgrounds, sports fields, parks, and open
  grounds connect through baked `OpenBoundary` or `Junction` gateways with a
  segment/area extent. Do not invent physical gates or reduce a continuous edge
  to a point portal. Use `Gate` only for an authored fence/wall gate, such as a
  gated graveyard entrance.
- A seed may procedurally initialize the population that occupies the authored
  town: names, households, traits, relationships, appearances, and assignment to
  available building-derived roles.
- Apply authored and quest-critical NPC overrides before filling remaining roles.
- Spatial chunks, blocks, cells, portals, and the persistent world octree are
  compiled/baked before runtime. Do not scan the full map or build its spatial
  partition at startup.
- Runtime world changes are localized overlays on immutable baked topology.
  Destruction or construction must invalidate and rebuild only affected regions.
- Runtime spatial indexes for moving objects are separate from the baked world
  topology and are never persistent world identity.
- Outside player observation, an NPC has no authoritative Unity transform. Its
  room/place, interior path or exterior transit cell, activity, and route progress
  are definitive. `LastObservedPose` exists only for visual continuity
  and must be invalidated by abstract movement, activity, room, route, or
  topology changes.
- Store partner, ex-partner, and family state in the city relationship graph;
  never add unsynchronized partner pointers to individual Sims.
- Keep social bias data-driven. Do not hardcode demographic prejudice into
  activity logic or mutate stored affinity merely because a bias rule applies.
- Keep relationship storage sparse. Reconcile records for family, partners,
  household/roommates, coworkers, neighbours, and authored social ties; never
  create an all-population relationship matrix.
- Determine household and neighbour circles from stable residence assignments,
  never from a Sim's temporary current position.
- Treat a missing active residence as authoritative homelessness. Do not create
  housing or employment to normalize the population; apply hardship through
  configurable simulation rules.
- Preserve a stable population during ordinary simulation: no automatic aging,
  residential relocation, reproduction, death, marriage, or divorce. Those
  changes require an explicit scripted/orchestrator command. Zero health alone
  must never kill a Sim.
- Derive generated Sim and household UIDs from stable content keys and the world
  seed. Never use list position, runtime object identity, or randomized platform
  hash codes as persistent identity.
- Route population bootstrap mutations through population actions/reducers and
  keep selector indexes nonserialized and revision-rebuildable. Do not copy the
  whole population for each action.

Read `docs/WORLD_AND_TOWN_GUIDE.md` before changing world topology, buildings,
population bootstrapping, employment, persistence, or quest integration.

## Repository layout

- `src/` contains all production C# source intended for Unity.
- `tests/` contains automated tests, test fixtures, and compile-validation code.
- `docs/` contains architecture notes and design documentation.

Keep production code out of `tests/`. Keep test-only helpers out of `src/`.
Within `src/`, prefer feature-oriented namespaces and folders beneath `Syms`.

## C# and Unity conventions

- Use the `Syms` root namespace.
- Use nullable reference annotations in code that is compiled outside Unity when
  the surrounding project supports them.
- Prefer plain C# domain objects for simulation rules. Depend on `MonoBehaviour`,
  `ScriptableObject`, and other Unity APIs only at Unity integration boundaries.
- Do not use editor-only APIs in runtime assemblies. Put editor code in an
  `Editor/` folder and a separate editor assembly definition.
- Avoid per-frame allocations and avoid doing expensive work in `Update`.
- Prefer serialized private fields over public mutable fields in Unity components.
- Use assembly definition files (`.asmdef`) to keep runtime and test assemblies
  explicit and isolated.
- Do not hand-edit Unity-generated `.csproj` or `.sln` files.
- `src/Syms/Syms.Runtime.csproj` and `tests/Syms.Tests.csproj` are authored
  standalone build files, not Unity-generated projects, and may be maintained.

## Testing and validation

- Add edit-mode tests for deterministic simulation/domain behavior.
- Add play-mode tests only when behavior requires Unity scenes, frames, physics,
  coroutines, or engine lifecycle callbacks.
- Every bug fix should include a regression test when practical.
- Tests must not depend on execution order or shared mutable global state.
- Run the relevant Unity Test Framework suite only in the consuming Unity
  project on an approved non-macOS development platform.
- Also run `dotnet test tests/Syms.Tests.csproj` when a .NET SDK is available.

## Change discipline

- Keep changes scoped to the requested feature.
- Do not commit generated binaries, caches, test results, or IDE state.
- Update documentation when introducing a new subsystem or repository convention.

## Code navigation

- Prefer the installed Graft tools for repository maps, API inspection, and
  caller tracing when they are available in the current task.
- Fall back to `rg` and direct source inspection when the Graft MCP tools are not
  loaded; do not install duplicate Graft plugins or run unsupported runtimes.
