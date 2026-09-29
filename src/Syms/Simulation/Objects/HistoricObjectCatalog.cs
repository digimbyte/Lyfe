using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.Activities;
using Syms.Simulation.Stats;

namespace Syms.Simulation.Objects
{
    /// <summary>Extensible 1800s-oriented object definitions; never placements.</summary>
    public static class HistoricObjectCatalog
    {
        public static List<InteractiveObjectDefinition> CreateDefault()
        {
            var result = new List<InteractiveObjectDefinition>();

            Add(result, "objects.seating.chair", "Chair", ObjectCategory.Seating, 50f, A("sit", ObjectActionKind.Sit, ActivityKind.Relax, FacilityKind.Seat, 1, 35f, 0f));
            Add(result, "objects.seating.armchair", "Armchair", ObjectCategory.Seating, 70f, A("sit", ObjectActionKind.Sit, ActivityKind.Relax, FacilityKind.Seat, 1, 70f, 5f));
            Add(result, "objects.seating.bench", "Bench", ObjectCategory.Seating, 120f, A("sit", ObjectActionKind.Sit, ActivityKind.Relax, FacilityKind.Seat, 3, 25f, -3f));
            Add(result, "objects.seating.pew", "Church Pew", ObjectCategory.Seating, 140f, A("sit", ObjectActionKind.Sit, ActivityKind.Socialize, FacilityKind.Seat, 5, 15f, 0f));
            Add(result, "objects.seating.couch", "Couch", ObjectCategory.Seating, 100f,
                A("sit", ObjectActionKind.Sit, ActivityKind.Relax, FacilityKind.Seat, 3, 60f, 6f),
                Sleep("sleep", FacilityKind.Bed, 1, SleepingArrangement.Couch, 45f, -5f));
            Add(result, "objects.seating.daybed", "Daybed", ObjectCategory.Sleeping, 90f,
                A("sit", ObjectActionKind.Lounge, ActivityKind.Relax, FacilityKind.Seat, 2, 55f, 5f),
                Sleep("sleep", FacilityKind.Bed, 1, SleepingArrangement.PrivateSingle, 65f, 0f));

            Add(result, "objects.sleeping.straw_pallet", "Straw Pallet", ObjectCategory.Sleeping, 25f, Sleep("sleep", FacilityKind.Bed, 1, SleepingArrangement.PrivateSingle, 10f, -8f));
            Add(result, "objects.sleeping.single_bed", "Single Bed", ObjectCategory.Sleeping, 90f, Sleep("sleep", FacilityKind.Bed, 1, SleepingArrangement.PrivateSingle, 70f, 8f));
            Add(result, "objects.sleeping.shared_bed", "Shared Double Bed", ObjectCategory.Sleeping, 120f, Sleep("sleep", FacilityKind.Bed, 2, SleepingArrangement.SharedBed, 75f, 8f));
            Add(result, "objects.sleeping.bunk_bed", "Bunk Bed", ObjectCategory.Sleeping, 110f, Sleep("sleep", FacilityKind.Bed, 2, SleepingArrangement.Bunk, 45f, 0f));
            Add(result, "objects.sleeping.crib", "Crib", ObjectCategory.Sleeping, 65f, Sleep("sleep", FacilityKind.Crib, 1, SleepingArrangement.PrivateSingle, 65f, 0f));
            Add(result, "objects.sleeping.hotel_bed", "Hotel Bed", ObjectCategory.Sleeping, 100f, Sleep("sleep", FacilityKind.Bed, 1, SleepingArrangement.Hotel, 65f, 5f), "lodging");
            Add(result, "objects.sleeping.institutional_bed", "Institutional Bed", ObjectCategory.Sleeping, 100f, Sleep("sleep", FacilityKind.Bed, 1, SleepingArrangement.Institutional, 35f, -2f), "orphanage", "almshouse");
            Add(result, "objects.sleeping.hospital_cot", "Hospital Cot", ObjectCategory.Medical, 90f, Sleep("sleep", FacilityKind.MedicalCot, 1, SleepingArrangement.Hospital, 30f, -2f), "hospital");
            Add(result, "objects.sleeping.jail_bunk", "Jail Bunk", ObjectCategory.Detention, 140f, Sleep("sleep", FacilityKind.PrisonCell, 1, SleepingArrangement.Jail, 5f, -15f), "jail");

            Add(result, "objects.plumbing.public_privy", "Public Privy", ObjectCategory.Plumbing, 80f, Toilet("toilet", 1, 35f, true), "public-restroom");
            Add(result, "objects.plumbing.private_privy", "Private Privy", ObjectCategory.Plumbing, 80f, Toilet("toilet", 1, 70f, true), "private-restroom");
            Add(result, "objects.plumbing.chamber_pot", "Chamber Pot", ObjectCategory.Plumbing, 25f, Toilet("toilet", 1, 20f, true), "private-restroom");
            Add(result, "objects.plumbing.washbasin", "Washbasin", ObjectCategory.Plumbing, 50f, A("wash", ObjectActionKind.Wash, ActivityKind.Hygiene, FacilityKind.WashBasin, 1, 25f, 3f));
            Add(result, "objects.plumbing.bathtub", "Bathtub", ObjectCategory.Plumbing, 100f, A("bathe", ObjectActionKind.Bathe, ActivityKind.Hygiene, FacilityKind.Bath, 1, 75f, 8f));

            Add(result, "objects.surface.dining_table", "Dining Table", ObjectCategory.Surface, 100f, A("eat", ObjectActionKind.Eat, ActivityKind.Eat, FacilityKind.DiningTable, 6, 20f, 0f));
            Add(result, "objects.surface.small_table", "Small Table", ObjectCategory.Surface, 60f, A("eat", ObjectActionKind.Eat, ActivityKind.Eat, FacilityKind.Table, 2, 10f, 0f));
            Add(result, "objects.work.desk", "Writing Desk", ObjectCategory.Workstation, 70f, A("work", ObjectActionKind.Work, ActivityKind.WorkTask, FacilityKind.Desk, 1, 35f, 0f));
            Add(result, "objects.work.workbench", "Workbench", ObjectCategory.Workstation, 120f, A("work", ObjectActionKind.Work, ActivityKind.WorkTask, FacilityKind.Workbench, 1, 25f, 0f));
            Add(result, "objects.work.forge", "Forge", ObjectCategory.Workstation, 180f, A("work", ObjectActionKind.Work, ActivityKind.WorkTask, FacilityKind.Forge, 1, 5f, -5f));
            Add(result, "objects.service.bar_counter", "Bar Counter", ObjectCategory.Service, 140f, A("serve", ObjectActionKind.Serve, ActivityKind.WorkTask, FacilityKind.BarCounter, 2, 15f, 0f));
            Add(result, "objects.service.shop_counter", "Shop Counter", ObjectCategory.Service, 120f, A("serve", ObjectActionKind.Serve, ActivityKind.WorkTask, FacilityKind.ServiceCounter, 2, 10f, 0f));
            Add(result, "objects.storage.wardrobe", "Wardrobe", ObjectCategory.Storage, 70f, A("store", ObjectActionKind.Store, ActivityKind.WorkTask, FacilityKind.Wardrobe, 1, 0f, 0f));
            Add(result, "objects.storage.chest", "Storage Chest", ObjectCategory.Storage, 80f, A("store", ObjectActionKind.Store, ActivityKind.WorkTask, FacilityKind.Storage, 1, 0f, 0f));
            Add(result, "objects.storage.bookshelf", "Bookshelf", ObjectCategory.Storage, 70f, A("read", ObjectActionKind.Lounge, ActivityKind.Relax, FacilityKind.LibraryShelf, 1, 25f, 2f));
            Add(result, "objects.heating.fireplace", "Fireplace", ObjectCategory.Heating, 130f, A("warm", ObjectActionKind.Warm, ActivityKind.Relax, FacilityKind.Hearth, 4, 35f, 4f));
            Add(result, "objects.recreation.piano", "Piano", ObjectCategory.Recreation, 100f, A("play", ObjectActionKind.Play, ActivityKind.Relax, FacilityKind.Piano, 1, 45f, 5f));
            Add(result, "objects.recreation.playground_swing", "Playground Swing", ObjectCategory.Recreation, 100f, A("play", ObjectActionKind.Play, ActivityKind.Play, FacilityKind.PlaygroundEquipment, 2, 20f, 8f), "outdoor", "playground");
            Add(result, "objects.recreation.seesaw", "Seesaw", ObjectCategory.Recreation, 100f, A("play", ObjectActionKind.Play, ActivityKind.Play, FacilityKind.PlaygroundEquipment, 2, 15f, 7f), "outdoor", "playground");
            Add(result, "objects.recreation.climbing_frame", "Climbing Frame", ObjectCategory.Recreation, 130f, A("climb", ObjectActionKind.Play, ActivityKind.Exercise, FacilityKind.PlaygroundEquipment, 4, 5f, 6f), "outdoor", "playground");
            Add(result, "objects.sports.cricket_pitch", "Cricket Pitch", ObjectCategory.Recreation, 200f, A("play", ObjectActionKind.Play, ActivityKind.Exercise, FacilityKind.SportsField, 22, 0f, 10f), "outdoor", "sports");
            Add(result, "objects.sports.lawn_game_field", "Lawn Game Field", ObjectCategory.Recreation, 160f, A("play", ObjectActionKind.Play, ActivityKind.Play, FacilityKind.SportsField, 12, 0f, 8f), "outdoor", "sports");
            Add(result, "objects.funerary.grave_plot", "Grave Plot", ObjectCategory.Decoration, 100f,
                A("mourn", ObjectActionKind.Mourn, ActivityKind.Mourn, FacilityKind.GravePlot, 2, 0f, 0f),
                A("tend", ObjectActionKind.Work, ActivityKind.WorkTask, FacilityKind.GravePlot, 1, 0f, 0f),
                "outdoor", "graveyard");
            Add(result, "objects.medical.surgery_table", "Surgery Table", ObjectCategory.Medical, 110f, A("care", ObjectActionKind.ReceiveCare, ActivityKind.Healthcare, FacilityKind.SurgeryTable, 1, -10f, -8f));
            Add(result, "objects.medical.morgue_slab", "Morgue Slab", ObjectCategory.Medical, 160f, A("work", ObjectActionKind.Work, ActivityKind.WorkTask, FacilityKind.MorgueSlab, 1, -20f, -10f));

            AddPortal(result, "objects.portal.interior_door", "Interior Door", ObjectCategory.Door, 80f);
            AddPortal(result, "objects.portal.lockable_door", "Lockable Exterior Door", ObjectCategory.Door, 130f, "lockable");
            AddPortal(result, "objects.portal.jail_cell_door", "Jail Cell Door", ObjectCategory.Detention, 220f, "secure", "jail");
            AddPortal(result, "objects.portal.wooden_gate", "Wooden Gate", ObjectCategory.Gate, 150f, "lockable");
            AddPortal(result, "objects.portal.iron_gate", "Iron Gate", ObjectCategory.Gate, 260f, "lockable", "secure");
            AddPortal(result, "objects.portal.cemetery_gate", "Cemetery Gate", ObjectCategory.Gate, 240f, "lockable", "graveyard", "cemetery");
            AddPortal(result, "objects.portal.sash_window", "Sash Window", ObjectCategory.Window, 55f, "window", "openable");
            AddPortal(result, "objects.portal.shuttered_window", "Shuttered Window", ObjectCategory.Window, 85f, "window", "shuttered");
            AddPortal(result, "objects.portal.barred_window", "Barred Window", ObjectCategory.Window, 180f, "window", "barred", "secure");
            AddPortal(result, "objects.portal.shopfront_window", "Shopfront Window", ObjectCategory.Window, 70f, "window", "breakable");

            return result;
        }

        public static InteractiveObjectDefinition Find(
            IList<InteractiveObjectDefinition> definitions,
            ObjectDefinitionId id)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            for (int i = 0; i < definitions.Count; i++)
                if (definitions[i] != null && definitions[i].Id == id) return definitions[i];
            return null;
        }

        private static ObjectAffordanceDefinition A(
            string id,
            ObjectActionKind action,
            ActivityKind activity,
            FacilityKind facility,
            int slots,
            float comfort,
            float utility)
        {
            return new ObjectAffordanceDefinition
            {
                LocalId = id,
                Action = action,
                Activity = activity,
                Facility = facility,
                SlotCount = slots,
                Comfort = comfort,
                BaseUtility = utility
            };
        }

        private static ObjectAffordanceDefinition Sleep(
            string id,
            FacilityKind facility,
            int slots,
            SleepingArrangement arrangement,
            float comfort,
            float utility)
        {
            ObjectAffordanceDefinition value = A(id, ObjectActionKind.Sleep, ActivityKind.Sleep, facility, slots, comfort, utility);
            value.Sleeping = arrangement;
            value.AllowsEmergencyTrespass = arrangement != SleepingArrangement.Jail
                && arrangement != SleepingArrangement.Hospital;
            value.EmergencyNeed = SimStat.Energy;
            value.EmergencyThreshold = 0.92f;
            return value;
        }

        private static ObjectAffordanceDefinition Toilet(string id, int slots, float privacy, bool emergency)
        {
            ObjectAffordanceDefinition value = A(id, ObjectActionKind.Toilet, ActivityKind.Toilet, FacilityKind.Toilet, slots, 0f, 0f);
            value.RequiresPrivacy = true;
            value.Privacy = privacy;
            value.AllowsEmergencyTrespass = emergency;
            value.EmergencyNeed = SimStat.Bladder;
            value.EmergencyThreshold = 0.94f;
            return value;
        }

        private static void Add(
            List<InteractiveObjectDefinition> result,
            string id,
            string name,
            ObjectCategory category,
            float health,
            ObjectAffordanceDefinition first,
            params object[] rest)
        {
            var value = new InteractiveObjectDefinition
            {
                Id = new ObjectDefinitionId(id),
                DisplayName = name,
                Category = category,
                MaximumHealth = health
            };
            value.Affordances.Add(first);
            for (int i = 0; i < rest.Length; i++)
            {
                ObjectAffordanceDefinition affordance = rest[i] as ObjectAffordanceDefinition;
                if (affordance != null) value.Affordances.Add(affordance);
                else if (rest[i] is string) value.Tags.Add((string)rest[i]);
            }
            result.Add(value);
        }

        private static void AddPortal(
            List<InteractiveObjectDefinition> result,
            string id,
            string name,
            ObjectCategory category,
            float health,
            params string[] tags)
        {
            var value = new InteractiveObjectDefinition
            {
                Id = new ObjectDefinitionId(id),
                DisplayName = name,
                Category = category,
                MaximumHealth = health,
                IsPortalController = true
            };
            value.Affordances.Add(A("open", ObjectActionKind.Open, ActivityKind.Idle, FacilityKind.Generic, 1, 0f, 0f));
            value.Affordances.Add(A("close", ObjectActionKind.Close, ActivityKind.Idle, FacilityKind.Generic, 1, 0f, 0f));
            value.Tags.AddRange(tags);
            result.Add(value);
        }
    }
}
