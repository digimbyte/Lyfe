using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.Buildings;
using Syms.Simulation.People;

namespace Syms.Population
{
    public enum HouseholdKind
    {
        Single,
        Family,
        Roommates
    }

    [Serializable]
    public sealed class NamePool
    {
        public string Id = "default";
        public List<string> GivenNames = new List<string>();
        public List<string> FamilyNames = new List<string>();
    }

    [Serializable]
    public sealed class HouseholdTemplate
    {
        public string Id;
        public HouseholdKind Kind;
        public int MinimumResidents = 1;
        public int MaximumResidents = 4;
        public int Weight = 1;
    }

    /// <summary>A complete or partial authored NPC created before generated filling.</summary>
    [Serializable]
    public sealed class NpcTemplate
    {
        public string StableKey;
        public string HouseholdKey;
        public bool HasHouseholdKind;
        public HouseholdKind HouseholdKind;
        public BuildingId ReservedResidence;
        public RoleSlotId ReservedRole;
        public string GivenName;
        public string FamilyName;
        public bool HasAge;
        public int AgeYears;
        public bool HasGender;
        public Gender Gender;
        public bool HasPersonality;
        public Conduct Conduct;
        public Concern Concern;
        public List<Trait> Traits = new List<Trait>();
    }

    /// <summary>Patches a template or generated NPC by its stable bootstrap key.</summary>
    [Serializable]
    public sealed class NpcOverride
    {
        public string TargetStableKey;
        public string GivenName;
        public string FamilyName;
        public bool HasAge;
        public int AgeYears;
        public bool HasGender;
        public Gender Gender;
        public bool HasPersonality;
        public Conduct Conduct;
        public Concern Concern;
        public List<Trait> AddTraits = new List<Trait>();
    }

    [Serializable]
    public sealed class QuestRoleReservation
    {
        public string NpcStableKey;
        public RoleSlotId RoleSlot;
        public bool Required = true;
    }

    [Serializable]
    public sealed class PopulationBootstrapDefinition
    {
        public long Seed;
        /// <summary>Zero uses template-planned occupancy; larger values may create homelessness.</summary>
        public int TargetPopulation;
        public bool AllowHomeless = true;
        public int MinimumAdultAge = 18;
        public int MaximumAdultAge = 70;
        public int MaximumTraitsPerSim = 3;
        public NamePool Names = new NamePool();
        public List<HouseholdTemplate> HouseholdTemplates = new List<HouseholdTemplate>();
        public List<NpcTemplate> NpcTemplates = new List<NpcTemplate>();
        public List<NpcOverride> NpcOverrides = new List<NpcOverride>();
        public List<QuestRoleReservation> QuestRoleReservations = new List<QuestRoleReservation>();
    }

    [Serializable]
    public sealed class PopulationIssue
    {
        public ValidationSeverity Severity;
        public string Code;
        public string EntityId;
        public string Message;
    }

    [Serializable]
    public sealed class PopulationBootstrapReport
    {
        public int RequestedPopulation;
        public int CreatedPopulation;
        public int HousedPopulation;
        public int HomelessPopulation;
        public int FilledRoles;
        public int VacantRoles;
        public int AssignedBeds;
        public int HousedResidentsWithoutAssignedBed;
        public List<PopulationIssue> Issues = new List<PopulationIssue>();

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Issues.Count; i++)
                    if (Issues[i] != null && Issues[i].Severity == ValidationSeverity.Error) return true;
                return false;
            }
        }
    }
}
