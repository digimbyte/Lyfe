using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.Buildings;
using Syms.Simulation.People;
using Syms.Simulation.Objects;
using Syms.Simulation.Social;
using Syms.Simulation.World;

namespace Syms.Population
{
    [Serializable]
    public sealed class PopulationBootstrapResult
    {
        public PopulationState State = new PopulationState();
        public PopulationBootstrapReport Report = new PopulationBootstrapReport();
    }

    public sealed class PopulationBootstrapper
    {
        public PopulationBootstrapResult Build(
            PopulationBootstrapDefinition definition,
            AuthoredTownRuntime town)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (town == null) throw new ArgumentNullException(nameof(town));

            var result = new PopulationBootstrapResult();
            result.State.Seed = definition.Seed;
            result.State.BootstrapId = StableUid.Create("population", definition.Seed, "bootstrap");
            Validate(definition, town, result.Report);
            if (result.Report.HasErrors) return result;

            var random = new KeyedRandom(definition.Seed);
            List<HouseholdRecord> households = CreateHouseholds(definition, town, random, result);
            int plannedPopulation = 0;
            for (int i = 0; i < households.Count; i++) plannedPopulation += households[i].PlannedResidents;
            int target = definition.TargetPopulation == 0
                ? Math.Max(plannedPopulation, definition.NpcTemplates.Count)
                : definition.TargetPopulation;
            result.Report.RequestedPopulation = target;

            var simByStableKey = new Dictionary<string, Sim>(StringComparer.Ordinal);
            var householdByAuthoredKey = new Dictionary<string, HouseholdRecord>(StringComparer.Ordinal);
            var householdBySim = new Dictionary<string, HouseholdRecord>(StringComparer.Ordinal);
            var templateByStableKey = IndexTemplates(definition);
            var overridesByStableKey = IndexOverrides(definition, result.Report);

            CreateAuthoredNpcs(definition, households, random, result,
                templateByStableKey, overridesByStableKey, simByStableKey,
                householdByAuthoredKey, householdBySim);
            if (result.Report.HasErrors) return result;

            FillHouseholds(definition, households, random, target, result, overridesByStableKey,
                simByStableKey, householdBySim);
            if (definition.TargetPopulation > 0)
                FillRemainingHousing(definition, households, random, target, result,
                    overridesByStableKey, simByStableKey, householdBySim);
            FillHomeless(definition, random, target, result, overridesByStableKey,
                simByStableKey);
            ReportUnusedOverrides(overridesByStableKey, simByStableKey, result.Report);

            BuildFamilyRelationships(definition, households, result.State);
            AssignHouseholdBeds(town, result.State, result.Report);
            AssignEmployment(definition, town, simByStableKey, result);
            BuildImmediateRelationships(result.State, householdBySim);
            SeedRelationshipAffinities(random, result.State.Relationships);
            CaptureRelationshipActions(result);

            result.Report.CreatedPopulation = result.State.Sims.Count;
            result.Report.HousedPopulation = householdBySim.Count;
            result.Report.HomelessPopulation = result.State.Sims.Count - householdBySim.Count;
            result.Report.VacantRoles = Math.Max(0, town.RoleSlots.Count - result.Report.FilledRoles);
            return result;
        }

        private static void AssignHouseholdBeds(
            AuthoredTownRuntime town,
            PopulationState state,
            PopulationBootstrapReport report)
        {
            if (town.Objects == null || town.Objects.Objects.Count == 0) return;
            var objects = new List<InteractiveObjectState>(town.Objects.Objects);
            var simsById = new Dictionary<string, Sim>(StringComparer.Ordinal);
            for (int i = 0; i < state.Sims.Count; i++)
                if (state.Sims[i] != null) simsById[state.Sims[i].Id] = state.Sims[i];
            objects.Sort((left, right) => string.CompareOrdinal(
                left == null ? string.Empty : left.Id.Value,
                right == null ? string.Empty : right.Id.Value));

            for (int householdIndex = 0; householdIndex < state.Households.Count; householdIndex++)
            {
                HouseholdRecord household = state.Households[householdIndex];
                if (household == null) continue;
                var members = new List<string>(household.MemberSimIds);
                members.Sort(StringComparer.Ordinal);
                var assigned = new HashSet<string>(StringComparer.Ordinal);
                for (int objectIndex = 0; objectIndex < objects.Count; objectIndex++)
                {
                    InteractiveObjectState value = objects[objectIndex];
                    if (value == null || value.Building != household.Building) continue;
                    for (int assignedIndex = 0; assignedIndex < value.AssignedSimIds.Count; assignedIndex++)
                    {
                        string assignedSimId = value.AssignedSimIds[assignedIndex];
                        assigned.Add(assignedSimId);
                        AssignBedroom(simsById, assignedSimId, value);
                    }
                }

                for (int memberIndex = 0; memberIndex < members.Count; memberIndex++)
                {
                    string simId = members[memberIndex];
                    if (assigned.Contains(simId))
                    {
                        report.AssignedBeds++;
                        continue;
                    }
                    InteractiveObjectState selected = null;
                    int selectedScore = int.MaxValue;
                    for (int objectIndex = 0; objectIndex < objects.Count; objectIndex++)
                    {
                        InteractiveObjectState candidate = objects[objectIndex];
                        if (candidate == null || candidate.Building != household.Building) continue;
                        SleepingArrangement arrangement;
                        int capacity = GetAssignableSleepCapacity(town.Objects, candidate, out arrangement);
                        if (capacity > candidate.AssignedSimIds.Count)
                        {
                            int score = BedAssignmentScore(
                                state.Relationships, candidate, arrangement, simId, members);
                            if (score < selectedScore)
                            {
                                selected = candidate;
                                selectedScore = score;
                            }
                        }
                    }
                    if (selected == null)
                    {
                        report.HousedResidentsWithoutAssignedBed++;
                        continue;
                    }
                    selected.AssignedSimIds.Add(simId);
                    AssignBedroom(simsById, simId, selected);
                    assigned.Add(simId);
                    report.AssignedBeds++;
                    town.Objects.Touch();
                }
            }

            if (report.HousedResidentsWithoutAssignedBed > 0)
                Warning(report, "population.beds.insufficient", "population",
                    report.HousedResidentsWithoutAssignedBed
                    + " housed residents have no personally assigned authored bed.");
        }

        private static void AssignBedroom(Dictionary<string, Sim> simsById, string simId,
            InteractiveObjectState bed)
        {
            Sim sim;
            if (!simsById.TryGetValue(simId, out sim) || sim.Residence == null) return;
            sim.Residence.AssignedBed = bed.Id;
            sim.Residence.Bedroom = bed.Room;
        }

        private static int GetAssignableSleepCapacity(
            ObjectStateStore store,
            InteractiveObjectState value,
            out SleepingArrangement arrangement)
        {
            arrangement = SleepingArrangement.None;
            if (!value.Enabled) return 0;
            InteractiveObjectDefinition definition = store.FindDefinition(value.Definition);
            if (definition == null) return 0;
            int capacity = 0;
            for (int i = 0; i < definition.Affordances.Count; i++)
            {
                ObjectAffordanceDefinition affordance = definition.Affordances[i];
                if (affordance == null) continue;
                switch (affordance.Sleeping)
                {
                    case SleepingArrangement.PrivateSingle:
                    case SleepingArrangement.SharedBed:
                    case SleepingArrangement.Bunk:
                    case SleepingArrangement.Institutional:
                        capacity += Math.Max(0, affordance.SlotCount);
                        if (arrangement == SleepingArrangement.None) arrangement = affordance.Sleeping;
                        break;
                }
            }
            return capacity;
        }

        private static int BedAssignmentScore(
            RelationshipGraph relationships,
            InteractiveObjectState bed,
            SleepingArrangement arrangement,
            string simId,
            List<string> householdMembers)
        {
            if (arrangement == SleepingArrangement.SharedBed)
            {
                for (int i = 0; i < bed.AssignedSimIds.Count; i++)
                {
                    SocialRelationship relationship = relationships.Find(simId, bed.AssignedSimIds[i]);
                    if (relationship != null && relationship.HasKind(RelationshipKind.Partner)) return -10;
                    if (relationship != null && relationship.HasKind(RelationshipKind.Family)) return 5;
                }
                for (int i = 0; i < householdMembers.Count; i++)
                {
                    SocialRelationship relationship = relationships.Find(simId, householdMembers[i]);
                    if (relationship != null && relationship.HasKind(RelationshipKind.Partner)
                        && !bed.AssignedSimIds.Contains(householdMembers[i])) return -5;
                }
                return 30;
            }
            if (arrangement == SleepingArrangement.PrivateSingle) return 0;
            if (arrangement == SleepingArrangement.Bunk) return 10;
            if (arrangement == SleepingArrangement.Institutional) return 20;
            return 100;
        }

        private static void Validate(
            PopulationBootstrapDefinition definition,
            AuthoredTownRuntime town,
            PopulationBootstrapReport report)
        {
            if (definition.TargetPopulation < 0)
                Error(report, "population.target.invalid", "population", "Target population cannot be negative.");
            if (definition.MinimumAdultAge < 0 || definition.MaximumAdultAge < definition.MinimumAdultAge)
                Error(report, "population.age_range.invalid", "population", "Adult age range is invalid.");
            if (definition.MaximumTraitsPerSim < 0)
                Error(report, "population.traits.invalid", "population", "Maximum traits cannot be negative.");
            var householdTemplateIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.HouseholdTemplates.Count; i++)
            {
                HouseholdTemplate template = definition.HouseholdTemplates[i];
                if (template == null || string.IsNullOrWhiteSpace(template.Id)
                    || (template != null && !householdTemplateIds.Add(template.Id))
                    || template.MinimumResidents < 1
                    || template.MaximumResidents < template.MinimumResidents
                    || template.Weight < 0)
                    Error(report, "population.household_template.invalid",
                        template == null ? string.Empty : template.Id,
                        "Household templates require an ID, valid resident bounds, and non-negative weight.");
            }

            int generatedNeeded = Math.Max(0, definition.TargetPopulation - definition.NpcTemplates.Count);
            int residentialCapacity = 0;
            for (int i = 0; i < town.Residences.Count; i++)
                if (town.Residences[i] != null) residentialCapacity += Math.Max(0, town.Residences[i].ResidentCapacity);
            bool mayGenerate = generatedNeeded > 0
                || (definition.TargetPopulation == 0 && residentialCapacity > definition.NpcTemplates.Count);
            bool hasNamePools = definition.Names != null
                && HasOnlyUsableNames(definition.Names.GivenNames)
                && HasOnlyUsableNames(definition.Names.FamilyNames);
            if (mayGenerate && !hasNamePools)
                Error(report, "population.names.missing", "names", "Generated NPCs require given and family name pools.");

            var templateKeys = new HashSet<string>(StringComparer.Ordinal);
            var householdKinds = new Dictionary<string, HouseholdKind>(StringComparer.Ordinal);
            for (int i = 0; i < definition.NpcTemplates.Count; i++)
            {
                NpcTemplate template = definition.NpcTemplates[i];
                if (template == null || string.IsNullOrWhiteSpace(template.StableKey)
                    || !templateKeys.Add(template.StableKey))
                    Error(report, "population.npc_template.id_invalid",
                        template == null ? string.Empty : template.StableKey,
                        "NPC template stable keys must be non-empty and unique.");
                else if (template.HasAge && template.AgeYears < 0)
                    Error(report, "population.npc_template.age_invalid", template.StableKey,
                        "Authored NPC age cannot be negative.");
                else if (!hasNamePools && (string.IsNullOrWhiteSpace(template.GivenName)
                    || string.IsNullOrWhiteSpace(template.FamilyName)))
                    Error(report, "population.npc_template.name_missing", template.StableKey,
                        "Authored NPCs require complete fixed names when no name pool exists.");
                if (template != null && template.HasHouseholdKind
                    && !string.IsNullOrWhiteSpace(template.HouseholdKey))
                {
                    HouseholdKind existingKind;
                    if (householdKinds.TryGetValue(template.HouseholdKey, out existingKind)
                        && existingKind != template.HouseholdKind)
                        Error(report, "population.npc_template.household_kind_conflict", template.HouseholdKey,
                            "Authored members of one household must agree on its kind.");
                    else householdKinds[template.HouseholdKey] = template.HouseholdKind;
                }
            }

            for (int i = 0; i < definition.NpcOverrides.Count; i++)
            {
                NpcOverride value = definition.NpcOverrides[i];
                if (value != null && value.HasAge && value.AgeYears < 0)
                    Error(report, "population.override.age_invalid", value.TargetStableKey,
                        "NPC override age cannot be negative.");
            }

            if (definition.TargetPopulation > 0 && definition.NpcTemplates.Count > definition.TargetPopulation)
                Error(report, "population.authored_over_target", "population",
                    "Target population cannot be smaller than the authored NPC count.");

            var residenceIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < town.Residences.Count; i++)
            {
                ResidenceInstance residence = town.Residences[i];
                if (residence == null || string.IsNullOrWhiteSpace(residence.Building.Value)
                    || !residenceIds.Add(residence.Building.Value))
                {
                    Error(report, "population.residence.id_invalid",
                        residence == null ? string.Empty : residence.Building.Value,
                        "Runtime residences require unique building IDs.");
                    continue;
                }
                if (residence.ResidentCapacity < 0)
                    Error(report, "population.residence.capacity_invalid", residence.Building.Value,
                        "Residence capacity cannot be negative.");
            }

            var roleIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < town.RoleSlots.Count; i++)
            {
                BuildingRoleSlot role = town.RoleSlots[i];
                if (role == null || string.IsNullOrWhiteSpace(role.Id.Value)
                    || !roleIds.Add(role.Id.Value))
                    Error(report, "population.role.id_invalid", role == null ? string.Empty : role.Id.Value,
                        "Runtime role slots require unique stable IDs.");
            }

            var roleReservations = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < definition.QuestRoleReservations.Count; i++)
            {
                QuestRoleReservation reservation = definition.QuestRoleReservations[i];
                if (reservation == null || string.IsNullOrWhiteSpace(reservation.RoleSlot.Value)
                    || !roleReservations.Add(reservation.RoleSlot.Value))
                    Error(report, "population.role_reservation.invalid",
                        reservation == null ? string.Empty : reservation.RoleSlot.Value,
                        "Quest role reservations must name unique role slots.");
            }
        }

        private static List<HouseholdRecord> CreateHouseholds(
            PopulationBootstrapDefinition definition,
            AuthoredTownRuntime town,
            KeyedRandom random,
            PopulationBootstrapResult result)
        {
            var residences = new List<ResidenceInstance>();
            for (int i = 0; i < town.Residences.Count; i++)
                if (town.Residences[i] != null && town.Residences[i].PopulationEligible)
                    residences.Add(town.Residences[i]);
            residences.Sort((left, right) => string.CompareOrdinal(left.Building.Value, right.Building.Value));

            var households = new List<HouseholdRecord>();
            for (int residenceIndex = 0; residenceIndex < residences.Count; residenceIndex++)
            {
                ResidenceInstance residence = residences[residenceIndex];
                if (residence.ResidentCapacity > 0)
                {
                    int capacity = residence.ResidentCapacity;
                    string stableKey = "residence/" + residence.Building.Value + "/household";
                    HouseholdTemplate template = SelectHouseholdTemplate(definition, random, stableKey);
                    int plannedResidents = DeterminePlannedResidents(template, capacity, random, stableKey);
                    var household = new HouseholdRecord
                    {
                        Id = new HouseholdUid(StableUid.Create("household", definition.Seed, stableKey)),
                        StableKey = stableKey,
                        Kind = template == null ? HouseholdKind.Family : template.Kind,
                        Building = residence.Building,
                        ManagedBy = residence.ManagedBy,
                        Address = residence.Address,
                        Capacity = capacity,
                        PlannedResidents = plannedResidents
                    };
                    Dispatch(result, PopulationAction.Add(household));
                    households.Add(household);
                }
            }
            return households;
        }

        private static HouseholdTemplate SelectHouseholdTemplate(
            PopulationBootstrapDefinition definition,
            KeyedRandom random,
            string stableKey)
        {
            int totalWeight = 0;
            for (int i = 0; i < definition.HouseholdTemplates.Count; i++)
                if (definition.HouseholdTemplates[i] != null)
                    totalWeight += Math.Max(0, definition.HouseholdTemplates[i].Weight);
            if (totalWeight == 0) return null;
            int choice = random.Index(stableKey + "/kind", totalWeight);
            for (int i = 0; i < definition.HouseholdTemplates.Count; i++)
            {
                HouseholdTemplate template = definition.HouseholdTemplates[i];
                if (template == null || template.Weight <= 0) continue;
                if (choice < template.Weight) return template;
                choice -= template.Weight;
            }
            return null;
        }

        private static int DeterminePlannedResidents(
            HouseholdTemplate template,
            int capacity,
            KeyedRandom random,
            string stableKey)
        {
            if (template == null) return capacity;
            if (template.Kind == HouseholdKind.Single) return Math.Min(1, capacity);
            int minimum = Math.Max(1, Math.Min(capacity, template.MinimumResidents));
            int maximum = Math.Max(minimum, Math.Min(capacity, template.MaximumResidents));
            return random.RangeInclusive(stableKey + "/planned-residents", minimum, maximum);
        }

        private static Dictionary<string, NpcTemplate> IndexTemplates(PopulationBootstrapDefinition definition)
        {
            var result = new Dictionary<string, NpcTemplate>(StringComparer.Ordinal);
            for (int i = 0; i < definition.NpcTemplates.Count; i++)
                if (definition.NpcTemplates[i] != null) result[definition.NpcTemplates[i].StableKey] = definition.NpcTemplates[i];
            return result;
        }

        private static bool HasOnlyUsableNames(List<string> values)
        {
            if (values == null || values.Count == 0) return false;
            for (int i = 0; i < values.Count; i++)
                if (string.IsNullOrWhiteSpace(values[i])) return false;
            return true;
        }

        private static Dictionary<string, NpcOverride> IndexOverrides(
            PopulationBootstrapDefinition definition,
            PopulationBootstrapReport report)
        {
            var result = new Dictionary<string, NpcOverride>(StringComparer.Ordinal);
            for (int i = 0; i < definition.NpcOverrides.Count; i++)
            {
                NpcOverride value = definition.NpcOverrides[i];
                if (value == null || string.IsNullOrWhiteSpace(value.TargetStableKey)) continue;
                if (result.ContainsKey(value.TargetStableKey))
                    Error(report, "population.override.duplicate", value.TargetStableKey,
                        "Only one NPC override may target a stable key.");
                else result.Add(value.TargetStableKey, value);
            }
            return result;
        }

        private static void CreateAuthoredNpcs(
            PopulationBootstrapDefinition definition,
            List<HouseholdRecord> households,
            KeyedRandom random,
            PopulationBootstrapResult result,
            Dictionary<string, NpcTemplate> templates,
            Dictionary<string, NpcOverride> overrides,
            Dictionary<string, Sim> simByStableKey,
            Dictionary<string, HouseholdRecord> householdByAuthoredKey,
            Dictionary<string, HouseholdRecord> householdBySim)
        {
            var keys = new List<string>(templates.Keys);
            keys.Sort(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                string stableKey = keys[i];
                NpcTemplate template = templates[stableKey];
                HouseholdRecord authoredHousehold;
                if (!string.IsNullOrWhiteSpace(template.HouseholdKey)
                    && householdByAuthoredKey.TryGetValue(template.HouseholdKey, out authoredHousehold)
                    && authoredHousehold.MemberSimIds.Count >= authoredHousehold.Capacity)
                {
                    Error(result.Report, "population.authored_household.over_capacity", template.HouseholdKey,
                        "Authored household members exceed the reserved residence capacity.");
                    continue;
                }
                HouseholdRecord household = FindAuthoredHousehold(
                    template, households, householdByAuthoredKey);
                if (household == null && !string.IsNullOrWhiteSpace(template.ReservedResidence.Value))
                {
                    Error(result.Report, "population.authored_npc.residence_missing", stableKey,
                        "Authored NPC reserved a missing or full residence.");
                    continue;
                }
                if (household == null && !definition.AllowHomeless)
                {
                    Error(result.Report, "population.authored_npc.unhoused", stableKey,
                        "Authored NPC could not be placed and homelessness is disabled.");
                    continue;
                }
                if (household != null && template.HasHouseholdKind)
                    household.Kind = template.HouseholdKind;

                Sim sim = CreateSim(definition, random, stableKey, household, template, GetOverride(overrides, stableKey));
                AddSim(result, sim, stableKey, household, simByStableKey, householdBySim);
            }
        }

        private static HouseholdRecord FindAuthoredHousehold(
            NpcTemplate template,
            List<HouseholdRecord> households,
            Dictionary<string, HouseholdRecord> householdByAuthoredKey)
        {
            HouseholdRecord existing;
            if (!string.IsNullOrWhiteSpace(template.HouseholdKey)
                && householdByAuthoredKey.TryGetValue(template.HouseholdKey, out existing))
                return existing.MemberSimIds.Count < existing.Capacity ? existing : null;

            for (int i = 0; i < households.Count; i++)
            {
                HouseholdRecord household = households[i];
                if (!string.IsNullOrWhiteSpace(template.ReservedResidence.Value)
                    && household.Building != template.ReservedResidence) continue;
                if (household.MemberSimIds.Count >= household.Capacity) continue;
                if (!string.IsNullOrWhiteSpace(template.HouseholdKey))
                    householdByAuthoredKey[template.HouseholdKey] = household;
                return household;
            }
            return null;
        }

        private static void FillHouseholds(
            PopulationBootstrapDefinition definition,
            List<HouseholdRecord> households,
            KeyedRandom random,
            int target,
            PopulationBootstrapResult result,
            Dictionary<string, NpcOverride> overrides,
            Dictionary<string, Sim> simByStableKey,
            Dictionary<string, HouseholdRecord> householdBySim)
        {
            for (int householdIndex = 0; householdIndex < households.Count && result.State.Sims.Count < target; householdIndex++)
            {
                HouseholdRecord household = households[householdIndex];
                int desiredResidents = Math.Max(household.MemberSimIds.Count, household.PlannedResidents);
                while (household.MemberSimIds.Count < desiredResidents && result.State.Sims.Count < target)
                {
                    int memberIndex = household.MemberSimIds.Count;
                    string stableKey = household.StableKey + "/member/" + memberIndex;
                    Sim sim = CreateSim(definition, random, stableKey, household, null, GetOverride(overrides, stableKey));
                    AddSim(result, sim, stableKey, household, simByStableKey, householdBySim);
                }
            }
        }

        private static void FillHomeless(
            PopulationBootstrapDefinition definition,
            KeyedRandom random,
            int target,
            PopulationBootstrapResult result,
            Dictionary<string, NpcOverride> overrides,
            Dictionary<string, Sim> simByStableKey)
        {
            int index = 0;
            while (result.State.Sims.Count < target && definition.AllowHomeless)
            {
                string stableKey = "homeless/" + index++;
                Sim sim = CreateSim(definition, random, stableKey, null, null, GetOverride(overrides, stableKey));
                AddSim(result, sim, stableKey, null, simByStableKey, null);
            }
            if (result.State.Sims.Count < target)
                Warning(result.Report, "population.target.unfilled", "population",
                    "Population target exceeds residence capacity and homelessness is disabled.");
        }

        private static void FillRemainingHousing(
            PopulationBootstrapDefinition definition,
            List<HouseholdRecord> households,
            KeyedRandom random,
            int target,
            PopulationBootstrapResult result,
            Dictionary<string, NpcOverride> overrides,
            Dictionary<string, Sim> simByStableKey,
            Dictionary<string, HouseholdRecord> householdBySim)
        {
            for (int householdIndex = 0; householdIndex < households.Count && result.State.Sims.Count < target; householdIndex++)
            {
                HouseholdRecord household = households[householdIndex];
                while (household.MemberSimIds.Count < household.Capacity && result.State.Sims.Count < target)
                {
                    if (household.Kind == HouseholdKind.Single) household.Kind = HouseholdKind.Roommates;
                    int memberIndex = household.MemberSimIds.Count;
                    string stableKey = household.StableKey + "/member/" + memberIndex;
                    Sim sim = CreateSim(definition, random, stableKey, household, null, GetOverride(overrides, stableKey));
                    AddSim(result, sim, stableKey, household, simByStableKey, householdBySim);
                }
            }
        }

        private static Sim CreateSim(
            PopulationBootstrapDefinition definition,
            KeyedRandom random,
            string stableKey,
            HouseholdRecord household,
            NpcTemplate template,
            NpcOverride npcOverride)
        {
            List<string> familyNames = definition.Names == null ? null : definition.Names.FamilyNames;
            List<string> givenNames = definition.Names == null ? null : definition.Names.GivenNames;
            string authoredFamilyName = template == null ? null : template.FamilyName;
            string familyName = ChooseName(familyNames, random, stableKey + "/family-name");
            if (household != null && household.Kind == HouseholdKind.Family)
            {
                if (string.IsNullOrWhiteSpace(household.FamilyName))
                    household.FamilyName = !string.IsNullOrWhiteSpace(authoredFamilyName)
                        ? authoredFamilyName
                        : ChooseName(familyNames, random, household.StableKey + "/family-name");
                familyName = household.FamilyName;
            }
            string givenName = ChooseName(givenNames, random, stableKey + "/given-name");
            if (template != null && !string.IsNullOrWhiteSpace(template.GivenName)) givenName = template.GivenName;
            if (!string.IsNullOrWhiteSpace(authoredFamilyName)) familyName = authoredFamilyName;

            var sim = new Sim(StableUid.Create("sim", definition.Seed, stableKey), givenName, familyName);
            sim.Demographics.Gender = template != null && template.HasGender
                ? template.Gender : (Gender)(1 + random.Index(stableKey + "/gender", 3));
            int memberIndex = household == null ? 0 : household.MemberSimIds.Count;
            int age = DetermineAge(definition, random, stableKey, household, memberIndex);
            if (template != null && template.HasAge) age = template.AgeYears;
            sim.Demographics.SetAge(age, 0d);
            sim.Personality.Conduct = template != null && template.HasPersonality
                ? template.Conduct : (Conduct)random.Index(stableKey + "/conduct", 4);
            sim.Personality.Concern = template != null && template.HasPersonality
                ? template.Concern : (Concern)random.Index(stableKey + "/concern", 4);
            AddTraits(sim, definition.MaximumTraitsPerSim, random, stableKey);
            if (template != null) AddTraits(sim, template.Traits);

            if (household != null)
            {
                sim.Residence = new Residence
                {
                    HouseholdId = household.Id.Value,
                    Building = household.Building,
                    ManagedBy = household.ManagedBy,
                    HomeAddress = household.Address,
                    Active = true
                };
                sim.Spatial.Address = household.Address;
                sim.Spatial.Address.Anchor = new AnchorId();
            }
            ApplyOverride(sim, npcOverride);
            return sim;
        }

        private static int DetermineAge(
            PopulationBootstrapDefinition definition,
            KeyedRandom random,
            string stableKey,
            HouseholdRecord household,
            int memberIndex)
        {
            if (household != null && household.Kind == HouseholdKind.Family && memberIndex >= 2)
                return random.RangeInclusive(stableKey + "/age", 1, Math.Max(1, definition.MinimumAdultAge - 1));
            return random.RangeInclusive(stableKey + "/age", definition.MinimumAdultAge, definition.MaximumAdultAge);
        }

        private static void AddTraits(Sim sim, int maximum, KeyedRandom random, string stableKey)
        {
            int available = Enum.GetValues(typeof(Trait)).Length - 1;
            int count = maximum == 0 ? 0 : random.RangeInclusive(stableKey + "/trait-count", 0, Math.Min(maximum, available));
            int cursor = random.Index(stableKey + "/trait-start", available);
            for (int i = 0; i < count; i++) sim.Traits.Add((Trait)(1 + ((cursor + i) % available)));
        }

        private static void AddTraits(Sim sim, List<Trait> traits)
        {
            if (traits == null) return;
            for (int i = 0; i < traits.Count; i++)
                if (traits[i] != Trait.None && !sim.Traits.Contains(traits[i])) sim.Traits.Add(traits[i]);
        }

        private static void ApplyOverride(Sim sim, NpcOverride value)
        {
            if (value == null) return;
            if (!string.IsNullOrWhiteSpace(value.GivenName)) sim.GivenName = value.GivenName;
            if (!string.IsNullOrWhiteSpace(value.FamilyName)) sim.FamilyName = value.FamilyName;
            if (value.HasAge) sim.Demographics.SetAge(value.AgeYears, 0d);
            if (value.HasGender) sim.Demographics.Gender = value.Gender;
            if (value.HasPersonality)
            {
                sim.Personality.Conduct = value.Conduct;
                sim.Personality.Concern = value.Concern;
            }
            AddTraits(sim, value.AddTraits);
        }

        private static void AddSim(
            PopulationBootstrapResult result,
            Sim sim,
            string stableKey,
            HouseholdRecord household,
            Dictionary<string, Sim> simByStableKey,
            Dictionary<string, HouseholdRecord> householdBySim)
        {
            Dispatch(result, PopulationAction.Add(sim));
            simByStableKey.Add(stableKey, sim);
            if (household == null) return;
            Dispatch(result, PopulationAction.Assign(household.Id, sim.Id));
            if (householdBySim != null) householdBySim.Add(sim.Id, household);
        }

        private static void BuildFamilyRelationships(
            PopulationBootstrapDefinition definition,
            List<HouseholdRecord> households,
            PopulationState state)
        {
            var simById = new Dictionary<string, Sim>(StringComparer.Ordinal);
            for (int i = 0; i < state.Sims.Count; i++) simById[state.Sims[i].Id] = state.Sims[i];
            for (int householdIndex = 0; householdIndex < households.Count; householdIndex++)
            {
                HouseholdRecord household = households[householdIndex];
                if (household.Kind != HouseholdKind.Family || household.MemberSimIds.Count < 2) continue;
                var adults = new List<string>();
                var children = new List<string>();
                for (int member = 0; member < household.MemberSimIds.Count; member++)
                {
                    string simId = household.MemberSimIds[member];
                    if (simById[simId].Demographics.AgeYears >= definition.MinimumAdultAge) adults.Add(simId);
                    else children.Add(simId);
                }
                if (adults.Count >= 2)
                {
                    state.Relationships.SetPartners(adults[0], adults[1], 0d);
                    state.Relationships.SetFamily(adults[0], FamilyRole.Spouse, adults[1], FamilyRole.Spouse, 0d);
                }
                for (int firstAdult = 0; firstAdult < adults.Count; firstAdult++)
                for (int secondAdult = firstAdult + 1; secondAdult < adults.Count; secondAdult++)
                    if (!(firstAdult == 0 && secondAdult == 1))
                        state.Relationships.SetFamily(adults[firstAdult], FamilyRole.Relative,
                            adults[secondAdult], FamilyRole.Relative, 0d);
                for (int child = 0; child < children.Count; child++)
                {
                    if (adults.Count >= 1)
                        state.Relationships.SetFamily(adults[0], FamilyRole.Parent,
                            children[child], FamilyRole.Child, 0d);
                    if (adults.Count >= 2)
                        state.Relationships.SetFamily(adults[1], FamilyRole.Parent,
                            children[child], FamilyRole.Child, 0d);
                    for (int sibling = 0; sibling < child; sibling++)
                        state.Relationships.SetFamily(children[sibling], FamilyRole.Sibling,
                            children[child], FamilyRole.Sibling, 0d);
                }
            }
        }

        private static void AssignEmployment(
            PopulationBootstrapDefinition definition,
            AuthoredTownRuntime town,
            Dictionary<string, Sim> simByStableKey,
            PopulationBootstrapResult result)
        {
            var slots = new Dictionary<string, BuildingRoleSlot>(StringComparer.Ordinal);
            for (int i = 0; i < town.RoleSlots.Count; i++)
                if (town.RoleSlots[i] != null) slots[town.RoleSlots[i].Id.Value] = town.RoleSlots[i];
            var assignedSims = new HashSet<string>(StringComparer.Ordinal);
            var assignedSlots = new HashSet<string>(StringComparer.Ordinal);

            var reservationByRole = new Dictionary<string, QuestRoleReservation>(StringComparer.Ordinal);
            for (int i = 0; i < definition.QuestRoleReservations.Count; i++)
            {
                QuestRoleReservation reservation = definition.QuestRoleReservations[i];
                if (reservation != null) reservationByRole[reservation.RoleSlot.Value ?? string.Empty] = reservation;
            }
            for (int i = 0; i < definition.NpcTemplates.Count; i++)
            {
                NpcTemplate template = definition.NpcTemplates[i];
                if (template != null && !string.IsNullOrWhiteSpace(template.ReservedRole.Value))
                {
                    QuestRoleReservation existing;
                    if (!reservationByRole.TryGetValue(template.ReservedRole.Value, out existing))
                        reservationByRole.Add(template.ReservedRole.Value, new QuestRoleReservation
                    {
                        NpcStableKey = template.StableKey,
                        RoleSlot = template.ReservedRole,
                        Required = true
                    });
                    else if (!string.Equals(existing.NpcStableKey, template.StableKey, StringComparison.Ordinal))
                        Error(result.Report, "population.role_reservation.conflict", template.ReservedRole.Value,
                            "Template and quest data reserve the same role for different NPCs.");
                }
            }
            var reservations = new List<QuestRoleReservation>(reservationByRole.Values);
            reservations.Sort((left, right) => string.CompareOrdinal(left.RoleSlot.Value, right.RoleSlot.Value));
            for (int i = 0; i < reservations.Count; i++)
            {
                QuestRoleReservation reservation = reservations[i];
                Sim sim;
                BuildingRoleSlot slot;
                bool valid = reservation != null
                    && simByStableKey.TryGetValue(reservation.NpcStableKey ?? string.Empty, out sim)
                    && slots.TryGetValue(reservation.RoleSlot.Value ?? string.Empty, out slot)
                    && sim.Demographics.AgeYears >= definition.MinimumAdultAge
                    && !assignedSims.Contains(sim.Id)
                    && !assignedSlots.Contains(slot.Id.Value);
                if (!valid)
                {
                    AddReservationIssue(result.Report, reservation);
                    continue;
                }
                AddEmployment(result, sim, slot, assignedSims, assignedSlots);
            }

            var adults = new List<Sim>();
            for (int i = 0; i < result.State.Sims.Count; i++)
            {
                Sim sim = result.State.Sims[i];
                if (sim.Demographics.AgeYears >= definition.MinimumAdultAge && !assignedSims.Contains(sim.Id)) adults.Add(sim);
            }
            adults.Sort((left, right) =>
            {
                ulong leftHash = StableUid.Hash64(definition.Seed, "employment/" + left.Id);
                ulong rightHash = StableUid.Hash64(definition.Seed, "employment/" + right.Id);
                int compare = leftHash.CompareTo(rightHash);
                return compare != 0 ? compare : string.CompareOrdinal(left.Id, right.Id);
            });
            var openSlots = new List<BuildingRoleSlot>();
            for (int i = 0; i < town.RoleSlots.Count; i++)
                if (town.RoleSlots[i] != null && !assignedSlots.Contains(town.RoleSlots[i].Id.Value)) openSlots.Add(town.RoleSlots[i]);
            openSlots.Sort((left, right) => string.CompareOrdinal(left.Id.Value, right.Id.Value));
            int count = Math.Min(adults.Count, openSlots.Count);
            for (int i = 0; i < count; i++) AddEmployment(result, adults[i], openSlots[i], assignedSims, assignedSlots);
        }

        private static void AddEmployment(
            PopulationBootstrapResult result,
            Sim sim,
            BuildingRoleSlot slot,
            HashSet<string> assignedSims,
            HashSet<string> assignedSlots)
        {
            Dispatch(result, PopulationAction.Assign(new EmploymentAssignmentRecord
            {
                SimId = sim.Id,
                RoleSlot = slot.Id,
                Building = slot.Building,
                Workplace = slot.Workplace
            }));
            assignedSims.Add(sim.Id);
            assignedSlots.Add(slot.Id.Value);
            result.Report.FilledRoles++;
        }

        private static void BuildImmediateRelationships(
            PopulationState state,
            Dictionary<string, HouseholdRecord> householdBySim)
        {
            for (int householdIndex = 0; householdIndex < state.Households.Count; householdIndex++)
            {
                HouseholdRecord household = state.Households[householdIndex];
                for (int first = 0; first < household.MemberSimIds.Count; first++)
                for (int second = first + 1; second < household.MemberSimIds.Count; second++)
                {
                    string firstId = household.MemberSimIds[first];
                    string secondId = household.MemberSimIds[second];
                    SocialRelationship relationship = state.Relationships.AddKind(
                        firstId, secondId, RelationshipKind.Household, 0d);
                    if (!relationship.HasKind(RelationshipKind.Family)
                        && !relationship.HasKind(RelationshipKind.Partner))
                        state.Relationships.AddKind(firstId, secondId, RelationshipKind.Roommate, 0d);
                }
            }

            var byWorkplace = new Dictionary<PlaceId, List<string>>();
            for (int i = 0; i < state.EmploymentAssignments.Count; i++)
                Add(byWorkplace, state.EmploymentAssignments[i].Workplace, state.EmploymentAssignments[i].SimId);
            foreach (List<string> group in byWorkplace.Values)
                AddRelationshipPairs(state.Relationships, group, RelationshipKind.Colleague, null, householdBySim);

            var byBlock = new Dictionary<BlockId, List<string>>();
            for (int i = 0; i < state.Households.Count; i++)
                for (int member = 0; member < state.Households[i].MemberSimIds.Count; member++)
                    Add(byBlock, state.Households[i].Address.Block, state.Households[i].MemberSimIds[member]);
            foreach (List<string> group in byBlock.Values)
                AddRelationshipPairs(state.Relationships, group, RelationshipKind.Neighbour,
                    DifferentHouseholds, householdBySim);
        }

        private static void CaptureRelationshipActions(PopulationBootstrapResult result)
        {
            var relationships = new List<SocialRelationship>(result.State.Relationships.Relationships);
            result.State.Relationships.Clear();
            for (int i = 0; i < relationships.Count; i++)
                Dispatch(result, PopulationAction.Upsert(relationships[i]));
        }

        private static void SeedRelationshipAffinities(KeyedRandom random, RelationshipGraph graph)
        {
            for (int i = 0; i < graph.Relationships.Count; i++)
            {
                SocialRelationship relationship = graph.Relationships[i];
                if (relationship == null) continue;
                int minimum = -20;
                int maximum = 30;
                if (relationship.HasKind(RelationshipKind.Partner)) { minimum = 30; maximum = 90; }
                else if (relationship.HasKind(RelationshipKind.Family)) { minimum = 10; maximum = 80; }
                else if (relationship.HasKind(RelationshipKind.Roommate)) { minimum = -20; maximum = 50; }
                else if (relationship.HasKind(RelationshipKind.Colleague)) { minimum = -30; maximum = 40; }
                string lower = string.CompareOrdinal(relationship.FirstSimId, relationship.SecondSimId) <= 0
                    ? relationship.FirstSimId : relationship.SecondSimId;
                string upper = string.CompareOrdinal(relationship.FirstSimId, relationship.SecondSimId) <= 0
                    ? relationship.SecondSimId : relationship.FirstSimId;
                relationship.SetAffinity(random.RangeInclusive(
                    "relationship/" + lower + "/" + upper + "/affinity", minimum, maximum));
            }
        }

        private static void AddRelationshipPairs(
            RelationshipGraph graph,
            List<string> simIds,
            RelationshipKind kind,
            Func<string, string, Dictionary<string, HouseholdRecord>, bool> predicate,
            Dictionary<string, HouseholdRecord> householdBySim)
        {
            for (int first = 0; first < simIds.Count; first++)
            for (int second = first + 1; second < simIds.Count; second++)
                if (predicate == null || predicate(simIds[first], simIds[second], householdBySim))
                    graph.AddKind(simIds[first], simIds[second], kind, 0d);
        }

        private static bool DifferentHouseholds(
            string first,
            string second,
            Dictionary<string, HouseholdRecord> householdBySim)
        {
            HouseholdRecord firstHousehold;
            HouseholdRecord secondHousehold;
            return !householdBySim.TryGetValue(first, out firstHousehold)
                || !householdBySim.TryGetValue(second, out secondHousehold)
                || firstHousehold.Id != secondHousehold.Id;
        }

        private static void Add<TKey>(Dictionary<TKey, List<string>> groups, TKey key, string value)
        {
            List<string> group;
            if (!groups.TryGetValue(key, out group))
            {
                group = new List<string>();
                groups.Add(key, group);
            }
            group.Add(value);
        }

        private static string ChooseName(List<string> values, KeyedRandom random, string key)
        {
            return values == null || values.Count == 0 ? string.Empty : random.ChooseStable(key, values);
        }

        private static NpcOverride GetOverride(Dictionary<string, NpcOverride> overrides, string stableKey)
        {
            NpcOverride value;
            return overrides.TryGetValue(stableKey, out value) ? value : null;
        }

        private static void ReportUnusedOverrides(
            Dictionary<string, NpcOverride> overrides,
            Dictionary<string, Sim> simsByStableKey,
            PopulationBootstrapReport report)
        {
            foreach (KeyValuePair<string, NpcOverride> pair in overrides)
                if (!simsByStableKey.ContainsKey(pair.Key))
                    Warning(report, "population.override.target_missing", pair.Key,
                        "NPC override did not match an authored or generated stable key.");
        }

        private static void Dispatch(PopulationBootstrapResult result, PopulationAction action)
        {
            PopulationReducer.Reduce(result.State, action);
        }

        private static void AddReservationIssue(PopulationBootstrapReport report, QuestRoleReservation reservation)
        {
            ValidationSeverity severity = reservation != null && reservation.Required
                ? ValidationSeverity.Error : ValidationSeverity.Warning;
            report.Issues.Add(new PopulationIssue
            {
                Severity = severity,
                Code = "population.role_reservation.unsatisfied",
                EntityId = reservation == null ? string.Empty : reservation.RoleSlot.Value,
                Message = "Reserved NPC or building-derived role slot is missing, unavailable, or ineligible."
            });
        }

        private static void Error(PopulationBootstrapReport report, string code, string entityId, string message)
        {
            report.Issues.Add(new PopulationIssue
                { Severity = ValidationSeverity.Error, Code = code, EntityId = entityId, Message = message });
        }

        private static void Warning(PopulationBootstrapReport report, string code, string entityId, string message)
        {
            report.Issues.Add(new PopulationIssue
                { Severity = ValidationSeverity.Warning, Code = code, EntityId = entityId, Message = message });
        }
    }
}
