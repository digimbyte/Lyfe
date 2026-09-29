using System;
using System.Collections.Generic;
using Syms.Simulation.Activities;
using Syms.Simulation.Buildings;
using Syms.Simulation.People;
using Syms.Simulation.Social;

namespace Syms.Simulation.Objects
{
    public sealed class ObjectActivityCandidateSource
    {
        private readonly AuthoredTownRuntime _town;
        private readonly ObjectStateStore _objects;
        private readonly RelationshipGraph _relationships;
        private readonly IObjectUsePolicy _policy;

        public ObjectActivityCandidateSource(
            AuthoredTownRuntime town,
            RelationshipGraph relationships,
            IObjectUsePolicy policy = null)
        {
            _town = town ?? throw new ArgumentNullException(nameof(town));
            _objects = town.Objects ?? throw new ArgumentNullException(nameof(town));
            _relationships = relationships;
            _policy = policy ?? new ObjectUsePolicy();
        }

        public List<ActivityCandidate> GetCandidates(Sim sim, double cityTime)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            var result = new List<ActivityCandidate>();
            List<InteractiveObjectState> values = GatherCandidates(sim, cityTime);
            var context = new ObjectUseContext
            {
                Sim = sim,
                Relationships = _relationships,
                CityTime = cityTime
            };
            context.Claims.AddRange(sim.AccessClaims);
            for (int objectIndex = 0; objectIndex < values.Count; objectIndex++)
            {
                InteractiveObjectState value = values[objectIndex];
                InteractiveObjectDefinition definition = _objects.FindDefinition(value.Definition);
                if (definition == null) continue;
                for (int affordanceIndex = 0; affordanceIndex < definition.Affordances.Count; affordanceIndex++)
                {
                    ObjectAffordanceDefinition affordance = definition.Affordances[affordanceIndex];
                    if (affordance == null
                        || IsPortalAction(affordance.Action)
                        || !_objects.HasAvailableSlot(value.Id, affordance.LocalId, cityTime)) continue;
                    ObjectUseEvaluation evaluation = _policy.Evaluate(value, definition, affordance, context);
                    if (!evaluation.Allowed) continue;
                    var candidate = new ActivityCandidate
                    {
                        Kind = affordance.Activity,
                        Place = value.Place,
                        Room = value.Room,
                        TargetId = value.Id.Value,
                        Object = value.Id,
                        ObjectAffordance = affordance.LocalId,
                        DurationSeconds = TypicalDuration(affordance.Activity),
                        BaseUtility = evaluation.UtilityModifier,
                        RequiresFacility = affordance.Facility != FacilityKind.Generic,
                        Facility = affordance.Facility
                    };
                    candidate.AccessClaims.AddRange(context.Claims);
                    result.Add(candidate);
                }
            }
            return result;
        }

        private List<InteractiveObjectState> GatherCandidates(Sim sim, double cityTime)
        {
            var result = new List<InteractiveObjectState>();
            var seen = new HashSet<Syms.Content.ObjectId>();
            RoomRuntimeInstance current = _town.FindRoom(sim.Spatial.Room);
            if (current == null)
            {
                AddObjects(result, seen, _objects.FindByRoom(sim.Spatial.Room), sim, cityTime, true);
                return result;
            }
            BuildingRoomGraph graph;
            if (!_town.BuildingGraphs.TryGetGraph(current.Building, out graph))
            {
                AddObjects(result, seen, _objects.FindByRoom(sim.Spatial.Room), sim, cityTime, true);
                return result;
            }
            AddObjects(result, seen, _objects.FindByBuilding(graph.Building), sim, cityTime, false);
            for (int i = 0; i < graph.MemberBuildings.Count; i++)
                AddObjects(result, seen, _objects.FindByBuilding(graph.MemberBuildings[i]), sim, cityTime, false);
            return result;
        }

        private static void AddObjects(List<InteractiveObjectState> target,
            HashSet<Syms.Content.ObjectId> seen, IReadOnlyList<InteractiveObjectState> values,
            Sim sim, double cityTime, bool includeAll)
        {
            for (int i = 0; i < values.Count; i++)
            {
                InteractiveObjectState value = values[i];
                if (value == null || !seen.Add(value.Id)) continue;
                ObjectAccessPolicy access = value.Ownership == null
                    ? ObjectAccessPolicy.Public : value.Ownership.Access;
                bool resident = sim.Residence != null && sim.Residence.Occupies(value.Building, cityTime);
                bool worker = sim.Employment != null && sim.Employment.Contract != null
                    && sim.Employment.Contract.Active
                    && sim.Employment.Contract.Building == value.Building;
                if (includeAll || value.Room == sim.Spatial.Room || access == ObjectAccessPolicy.Public
                    || resident || worker) target.Add(value);
            }
        }

        private static bool IsPortalAction(ObjectActionKind action)
        {
            return action == ObjectActionKind.Open || action == ObjectActionKind.Close
                || action == ObjectActionKind.Lock || action == ObjectActionKind.Unlock;
        }

        private static double TypicalDuration(ActivityKind activity)
        {
            switch (activity)
            {
                case ActivityKind.Sleep: return 28800d;
                case ActivityKind.Toilet: return 600d;
                case ActivityKind.Hygiene: return 1200d;
                case ActivityKind.Eat: return 1200d;
                case ActivityKind.Drink: return 300d;
                case ActivityKind.WorkTask: return 1800d;
                default: return 1800d;
            }
        }
    }
}
