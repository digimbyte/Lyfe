# Architecture

Architecture decisions will be recorded here as systems are introduced.

The default boundary is:

- deterministic simulation and game rules in plain C# types;
- Unity components and assets as adapters around those rules;
- edit-mode tests for domain behavior; and
- play-mode tests for Unity lifecycle and engine integration.

## Initial simulation model

`Syms.Simulation` contains engine-independent city state. A `CitySimulation` owns
the population, each `Sim` owns traits, mood, social tags, numeric stats, and
employment, and each employment record owns a weekly work schedule. Employment
uses `JobDefinition`, building-owned `BuildingRoleSlot` instances, and per-Sim
`EmploymentContract` state. Stable `JobDefinitionId` values such as
`jobs.police.clerk` are the only authored and saved job identity; there is no
enum-backed compatibility model.

## Authored buildings and employment

`AuthoredTownContent` contains definitions plus building instances exported from
the fixed authored map. Loading it registers residential capacity, aggregates
logical facilities, and instantiates only the employment role slots declared by
enabled building instances. A job definition describes duties and behavior but
never creates demand by itself.

`EmploymentService` assigns a vacant building role to a concrete Sim and creates
an employment contract containing the resolved workplace, cell, schedule, and
pay. Instance capacity overrides allow an authored or damaged building to expose
fewer roles or facilities without changing the shared definition.

Building instances form a strict two-level hierarchy: one root manager/site and
optional direct child buildings. Apartments, hotel or motel rooms/suites, and
jail cells are child buildings. The root owns shared halls, grounds, services,
and management roles. Child `BuildingId` values are the residence, lease, or
detention identity; no separate residence-unit layer exists.

Resident capacity is derived exclusively from usable authored bed affordance
slots. Bed assignments establish which bedroom or cell each person belongs to,
including shared beds, children's bedrooms, multi-guest rooms, and multi-inmate
cells. Lodging leases reserve a child building and assign its beds. Detention
assignments reserve jail bunks and produce meal-delivery obligations that must be
completed by on-duty staff with an authored `FeedPrisoners` duty.

## Demographics and relationships

Each Sim owns fixed demographic state: authored age in completed years plus
fractional progress, gender data, and a life stage derived from configurable
thresholds. Ordinary city-time advancement never changes age.

Relationships are authoritative in the city-level `RelationshipGraph`; Sims do
not store duplicated partner pointers. One relationship record can retain family,
partner, ex-partner, friendship, rivalry, and other kinds alongside a shared
affinity clamped to `-100..100`. Family roles are directional so parent/child and
sibling relationships remain unambiguous.

An explicit bootstrap or scripted new-partnership command converts previous active
partnerships to ex-partner history instead of deleting them. `SocialBiasProfile` supplies optional
data-authored, directional affinity modifiers based on demographics and existing
relationship kinds. Bias modifies perceived/effective affinity without rewriting
the underlying relationship history.

Every Sim has stable logical residence data containing household membership and
an authored home address. `ImmediateSocialCircleService` derives sparse links for
all pairs sharing a household or workplace and for residents of different
households on the same authored block. Unrelated townspeople receive no record.
Family and partner links remain explicit; unrelated household members also gain
the roommate kind. Derived context links are reconciled after housing or
employment changes, while explicit and non-neutral relationship history is
preserved.

Housing status is derived from the stable residence assignment: a Sim without an
active residence is homeless. `EconomicPressureModel` analytically applies
configurable daily hardship. Unemployment increases poverty; homelessness adds
further poverty, stress, anxiety, comfort, hygiene, and health pressure. Poverty
itself contributes additional stress, anxiety, and health loss. Employment does
not imply free income—money is still earned by completed paid work activities.

The population is structurally stable. Ordinary simulation never ages Sims,
changes residence, creates children, kills Sims, or forms/ends marriages.
`ApplyScriptedAgeChange`, relocation/homelessness, birth, death, marriage, and
divorce commands are the sole runtime boundary for those changes. Death does not
follow automatically from zero health. Physical travel and ordinary economic or
need fluctuations are not structural population changes.

## Deterministic population bootstrap

`Syms.Population` consumes `AuthoredTownRuntime` and a serializable
`PopulationBootstrapDefinition`. It never creates buildings, homes, role slots,
or map geometry. Authored NPC templates, partial overrides, reserved residences,
and quest role reservations are resolved before generated residents fill the
remaining planned household occupancy. Population above housing capacity may be
homeless when explicitly allowed; otherwise the report records an unfilled target.

Sim and household UIDs derive from the world seed plus stable content keys using
portable hashes. Keyed random substreams make identity, names, demographics,
personality, traits, affinity, and employment deterministic without depending on
one global call sequence. Name selection uses rendezvous-style hashing, so pool
ordering cannot reshuffle residents.

`PopulationState` is normalized, list-backed save data. Bootstrap mutations pass
through `PopulationAction` and `PopulationReducer`; the reducer advances a
revision without copying the entire 10,000-Sim state. `PopulationSelectors`
rebuilds nonserialized indexes lazily by revision and provides constant-time
identity, household, and employment lookup plus indexed workplace, block, and
relationship groups. Mutation and relationship indexes also rebuild after load.

## Hybrid world simulation

The authored city is compiled into stable chunks, blocks, a persistent spatial
octree, and sparse 3D cells before runtime. Startup loads baked data; it never
scans the entire map or constructs the world partition. A cell owns world bounds;
positions inside it use normalized X/Y/Z coordinates. Explicit face connections
form the only valid unloaded paths, so blockers such as walls, rivers, locks, and
closures cannot be bypassed by straight-line interpolation.

`CellRoute` instances are immutable shared path data. Each travelling Sim stores
only its route ID, current segment and cell-local progress. Travel advances from
elapsed city time rather than frame ticks. Separate runtime spatial indexes may
track moving entities, but they do not replace the baked world octree and their
node identities are never persistent.

Per-property routing uses a directed room graph spanning the root and its direct
children. Physical doors, windows, stairs, and fence gates are gateway edges.
Unfenced playgrounds, fields, parks, and similar outdoor rooms instead use baked
open-boundary or junction gateways with segment/area portal geometry. These
represent continuous crossings without fabricating a door-like gate. Graveyard
gates remain physical gateway objects when the authored fence actually has one.

Spatial authority has three states: at a logical place, in transit, or represented
by a materialized Unity agent. Agents load within one block and remain loaded
until beyond two blocks. An observed pose is reusable only while its activity,
topology, and route progress revisions still match.

## Activity decisions

Activity selection is deterministic utility scoring. Needs, work obligations,
the 4x4 conduct/concern personality grid, traits, travel cost, and seeded
variation contribute independently. Work is a soft obligation: personality and
traits influence lateness and slacking, while critical needs can override it.

Facilities use logical capacity reservations while unloaded. The city wakes Sims
for activity or route boundaries rather than updating the whole population every
frame. Long time jumps resolve each due Sim directly at the destination time.

Topology, routes, capacity definitions, and reservations use public list-backed
save data. Dictionaries and the event heap are runtime indexes rebuilt after
loading. City time is measured in seconds, with weekly schedules treating zero as
Sunday at midnight.

The complete authoring, bake, bootstrap, and runtime-overlay rules are defined in
`WORLD_AND_TOWN_GUIDE.md`.
