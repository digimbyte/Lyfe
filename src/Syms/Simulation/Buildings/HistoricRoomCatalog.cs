using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.Activities;

namespace Syms.Simulation.Buildings
{
    /// <summary>
    /// Broad 1800s room vocabulary. Definitions are suggestions only: they do
    /// not create geometry, facilities, jobs, buildings, or population.
    /// </summary>
    public static class HistoricRoomCatalog
    {
        public static List<RoomDefinition> CreateDefault()
        {
            var result = new List<RoomDefinition>();

            // Domestic rooms and estates.
            Add(result, "rooms.residential.bedroom", "Bedroom", RoomCategory.Residential, RoomEnvironment.Indoor, RoomAccess.Private, A(ActivityKind.Sleep, ActivityKind.Relax), F(FacilityKind.Bed, FacilityKind.Wardrobe), "home");
            Add(result, "rooms.residential.nursery", "Nursery", RoomCategory.Childcare, RoomEnvironment.Indoor, RoomAccess.Private, A(ActivityKind.Sleep, ActivityKind.Relax), F(FacilityKind.Crib, FacilityKind.Bed), "children");
            Add(result, "rooms.residential.servants_quarters", "Servants' Quarters", RoomCategory.Residential, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.Sleep, ActivityKind.Relax), F(FacilityKind.Bed, FacilityKind.Storage), "domestic-service");
            Add(result, "rooms.residential.parlour", "Parlour", RoomCategory.Residential, RoomEnvironment.Indoor, RoomAccess.ResidentsAndGuests, A(ActivityKind.Relax, ActivityKind.Socialize), F(FacilityKind.Seat, FacilityKind.Hearth), "home");
            Add(result, "rooms.residential.drawing_room", "Drawing Room", RoomCategory.Residential, RoomEnvironment.Indoor, RoomAccess.ResidentsAndGuests, A(ActivityKind.Relax, ActivityKind.Socialize), F(FacilityKind.Seat, FacilityKind.Piano), "estate");
            Add(result, "rooms.residential.dining_room", "Private Dining Room", RoomCategory.Residential, RoomEnvironment.Indoor, RoomAccess.ResidentsAndGuests, A(ActivityKind.Eat, ActivityKind.Socialize), F(FacilityKind.DiningTable), "home");
            Add(result, "rooms.residential.kitchen", "Domestic Kitchen", RoomCategory.Residential, RoomEnvironment.Indoor, RoomAccess.ResidentsAndGuests, A(ActivityKind.Eat, ActivityKind.WorkTask), F(FacilityKind.Hearth, FacilityKind.Oven, FacilityKind.KitchenRange), "home");
            Add(result, "rooms.residential.pantry", "Pantry", RoomCategory.Storage, RoomEnvironment.Indoor, RoomAccess.ResidentsAndGuests, A(ActivityKind.WorkTask), F(FacilityKind.Storage, FacilityKind.Shelf), "food-storage");
            Add(result, "rooms.residential.scullery", "Scullery", RoomCategory.Utility, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.Hygiene, ActivityKind.WorkTask), F(FacilityKind.WashBasin), "domestic-service");
            Add(result, "rooms.residential.laundry", "Laundry", RoomCategory.Utility, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.Hygiene, ActivityKind.WorkTask), F(FacilityKind.LaundryTub), "domestic-service");
            Add(result, "rooms.residential.bathing_room", "Bathing Room", RoomCategory.Utility, RoomEnvironment.Indoor, RoomAccess.Private, A(ActivityKind.Hygiene), F(FacilityKind.Bath, FacilityKind.WashBasin), "home");
            Add(result, "rooms.residential.privy", "Privy", RoomCategory.Utility, RoomEnvironment.Indoor, RoomAccess.Private, A(ActivityKind.Toilet), F(FacilityKind.Toilet), "sanitation");
            Add(result, "rooms.residential.study", "Study", RoomCategory.Residential, RoomEnvironment.Indoor, RoomAccess.Private, A(ActivityKind.Relax, ActivityKind.WorkTask), F(FacilityKind.Desk, FacilityKind.LibraryShelf), "home");
            Add(result, "rooms.residential.library", "Private Library", RoomCategory.Residential, RoomEnvironment.Indoor, RoomAccess.Private, A(ActivityKind.Relax), F(FacilityKind.LibraryShelf, FacilityKind.Seat), "estate");
            Add(result, "rooms.residential.ballroom", "Ballroom", RoomCategory.Entertainment, RoomEnvironment.Indoor, RoomAccess.ResidentsAndGuests, A(ActivityKind.Relax, ActivityKind.Socialize), F(FacilityKind.Piano, FacilityKind.Seat), "estate");
            Add(result, "rooms.residential.conservatory", "Conservatory", RoomCategory.Residential, RoomEnvironment.Indoor, RoomAccess.ResidentsAndGuests, A(ActivityKind.Relax), F(FacilityKind.GardenPlot, FacilityKind.Seat), "estate");
            Add(result, "rooms.residential.mansion_great_hall", "Mansion Great Hall", RoomCategory.Residential, RoomEnvironment.Indoor, RoomAccess.ResidentsAndGuests, A(ActivityKind.Idle, ActivityKind.Socialize), F(FacilityKind.Seat, FacilityKind.Hearth), "mansion", "estate");
            Add(result, "rooms.residential.attic", "Attic", RoomCategory.Storage, RoomEnvironment.Indoor, RoomAccess.Private, A(ActivityKind.WorkTask), F(FacilityKind.Storage), "home");
            Add(result, "rooms.residential.cellar", "Cellar", RoomCategory.Storage, RoomEnvironment.Underground, RoomAccess.Private, A(ActivityKind.WorkTask), F(FacilityKind.Storage), "home");
            Add(result, "rooms.residential.carriage_house", "Carriage House", RoomCategory.Transport, RoomEnvironment.CoveredOutdoor, RoomAccess.Private, A(ActivityKind.WorkTask), F(FacilityKind.CarriageBay, FacilityKind.Workbench), "estate");

            // Lodging, food, and drink. A motel is represented by its period equivalent.
            Add(result, "rooms.lodging.inn_common_room", "Inn Common Room", RoomCategory.Lodging, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.Eat, ActivityKind.Drink, ActivityKind.Relax, ActivityKind.Socialize), F(FacilityKind.DiningTable, FacilityKind.Hearth), "inn");
            Add(result, "rooms.lodging.inn_guest_room", "Inn Guest Room", RoomCategory.Lodging, RoomEnvironment.Indoor, RoomAccess.ResidentsAndGuests, A(ActivityKind.Sleep, ActivityKind.Relax), F(FacilityKind.Bed, FacilityKind.WashBasin), "inn");
            Add(result, "rooms.lodging.hotel_lobby", "Hotel Lobby", RoomCategory.Lodging, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.Idle, ActivityKind.Socialize), F(FacilityKind.ReceptionDesk, FacilityKind.Seat), "hotel");
            Add(result, "rooms.lodging.hotel_room", "Hotel Room", RoomCategory.Lodging, RoomEnvironment.Indoor, RoomAccess.ResidentsAndGuests, A(ActivityKind.Sleep, ActivityKind.Relax), F(FacilityKind.Bed, FacilityKind.WashBasin), "hotel");
            Add(result, "rooms.lodging.boarding_house_room", "Boarding House Room", RoomCategory.Lodging, RoomEnvironment.Indoor, RoomAccess.ResidentsAndGuests, A(ActivityKind.Sleep), F(FacilityKind.Bed, FacilityKind.Storage), "boarding-house");
            Add(result, "rooms.lodging.motel_room", "Roadside Lodging Room (Motel Equivalent)", RoomCategory.Lodging, RoomEnvironment.Indoor, RoomAccess.ResidentsAndGuests, A(ActivityKind.Sleep, ActivityKind.Relax), F(FacilityKind.Bed, FacilityKind.WashBasin), "coaching-inn", "road-house", "motel-equivalent");
            Add(result, "rooms.food.tavern_bar", "Tavern Bar", RoomCategory.FoodAndDrink, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.Drink, ActivityKind.Eat, ActivityKind.Relax, ActivityKind.Socialize), F(FacilityKind.BarCounter, FacilityKind.Seat), "bar", "tavern");
            Add(result, "rooms.food.saloon", "Saloon", RoomCategory.FoodAndDrink, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.Drink, ActivityKind.Relax, ActivityKind.Socialize), F(FacilityKind.BarCounter, FacilityKind.Piano), "bar", "setting-dependent");
            Add(result, "rooms.food.restaurant_dining", "Restaurant Dining Room", RoomCategory.FoodAndDrink, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.Eat, ActivityKind.Socialize), F(FacilityKind.DiningTable, FacilityKind.ServiceCounter), "restaurant");
            Add(result, "rooms.food.tea_room", "Tea Room", RoomCategory.FoodAndDrink, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.Drink, ActivityKind.Eat, ActivityKind.Socialize), F(FacilityKind.DiningTable), "cafe");
            Add(result, "rooms.food.commercial_kitchen", "Commercial Kitchen", RoomCategory.FoodAndDrink, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.Oven, FacilityKind.KitchenRange, FacilityKind.Workbench), "kitchen");
            Add(result, "rooms.food.brewery", "Brewery", RoomCategory.Industry, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.BreweryVat, FacilityKind.Storage), "brewery");
            Add(result, "rooms.food.distillery", "Distillery", RoomCategory.Industry, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.Distillery, FacilityKind.Storage), "distillery");

            // Education and child institutions.
            Add(result, "rooms.education.school_classroom", "School Classroom", RoomCategory.Education, RoomEnvironment.Indoor, RoomAccess.Restricted, A(ActivityKind.WorkTask, ActivityKind.Socialize), F(FacilityKind.Classroom, FacilityKind.Desk), "school");
            Add(result, "rooms.education.school_office", "School Office", RoomCategory.Education, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.Desk, FacilityKind.Storage), "school");
            Add(result, "rooms.education.schoolyard", "Schoolyard", RoomCategory.Education, RoomEnvironment.Outdoor, RoomAccess.Restricted, A(ActivityKind.Play, ActivityKind.Exercise, ActivityKind.Relax, ActivityKind.Socialize), F(FacilityKind.PlaygroundEquipment, FacilityKind.SportsField), "school", "children", "open-boundary-capable");
            Add(result, "rooms.public.playground", "Public Playground", RoomCategory.PublicSpace, RoomEnvironment.Outdoor, RoomAccess.Public, A(ActivityKind.Play, ActivityKind.Exercise, ActivityKind.Relax, ActivityKind.Socialize), F(FacilityKind.PlaygroundEquipment, FacilityKind.Seat), "children", "public-poi", "open-boundary-capable");
            Add(result, "rooms.public.sports_field", "Public Sports Field", RoomCategory.PublicSpace, RoomEnvironment.Outdoor, RoomAccess.Public, A(ActivityKind.Play, ActivityKind.Exercise, ActivityKind.Socialize), F(FacilityKind.SportsField), "sports", "public-poi", "open-boundary-capable");
            Add(result, "rooms.orphanage.dormitory", "Orphanage Dormitory", RoomCategory.Childcare, RoomEnvironment.Indoor, RoomAccess.Restricted, A(ActivityKind.Sleep, ActivityKind.Relax), F(FacilityKind.Bed, FacilityKind.Storage), "orphanage");
            Add(result, "rooms.orphanage.dining_hall", "Orphanage Dining Hall", RoomCategory.Childcare, RoomEnvironment.Indoor, RoomAccess.Restricted, A(ActivityKind.Eat, ActivityKind.Socialize), F(FacilityKind.DiningTable), "orphanage");
            Add(result, "rooms.orphanage.classroom", "Orphanage Classroom", RoomCategory.Education, RoomEnvironment.Indoor, RoomAccess.Restricted, A(ActivityKind.WorkTask), F(FacilityKind.Classroom), "orphanage");
            Add(result, "rooms.orphanage.infirmary", "Orphanage Infirmary", RoomCategory.Medical, RoomEnvironment.Indoor, RoomAccess.Restricted, A(ActivityKind.Healthcare, ActivityKind.Sleep), F(FacilityKind.MedicalCot), "orphanage");

            // Civic, police, court, and detention rooms.
            Add(result, "rooms.civic.town_hall", "Town Hall", RoomCategory.Civic, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.WorkTask, ActivityKind.Socialize), F(FacilityKind.Seat, FacilityKind.Desk), "government", "public-poi");
            Add(result, "rooms.civic.council_chamber", "Council Chamber", RoomCategory.Civic, RoomEnvironment.Indoor, RoomAccess.Restricted, A(ActivityKind.WorkTask), F(FacilityKind.Table, FacilityKind.Seat), "government");
            Add(result, "rooms.civic.records_office", "Records Office", RoomCategory.Civic, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.Desk, FacilityKind.Storage), "government");
            Add(result, "rooms.civic.post_office", "Post Office", RoomCategory.Civic, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.WorkTask, ActivityKind.Idle), F(FacilityKind.ServiceCounter, FacilityKind.Storage), "postal-service");
            Add(result, "rooms.civic.telegraph_office", "Telegraph Office", RoomCategory.Civic, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.WorkTask), F(FacilityKind.Desk, FacilityKind.ServiceCounter), "telegraph", "setting-dependent");
            Add(result, "rooms.police.front_desk", "Police Front Desk", RoomCategory.LawAndOrder, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.WorkTask, ActivityKind.Idle), F(FacilityKind.ServiceCounter, FacilityKind.WaitingBench), "police");
            Add(result, "rooms.police.office", "Police Office", RoomCategory.LawAndOrder, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.Desk, FacilityKind.Storage), "police");
            Add(result, "rooms.police.detective_office", "Detective Office", RoomCategory.LawAndOrder, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.Desk), "police");
            Add(result, "rooms.police.evidence_room", "Evidence Room", RoomCategory.LawAndOrder, RoomEnvironment.Indoor, RoomAccess.Secure, A(ActivityKind.WorkTask), F(FacilityKind.Storage), "police");
            Add(result, "rooms.police.armoury", "Police Armoury", RoomCategory.LawAndOrder, RoomEnvironment.Indoor, RoomAccess.Secure, A(ActivityKind.WorkTask), F(FacilityKind.Storage), "police");
            Add(result, "rooms.police.holding_cell", "Police Holding Cell", RoomCategory.LawAndOrder, RoomEnvironment.Indoor, RoomAccess.Secure, A(ActivityKind.Idle, ActivityKind.Sleep), F(FacilityKind.PrisonCell), "police", "detention");
            Add(result, "rooms.prison.cell_block", "Prison Cell Block", RoomCategory.LawAndOrder, RoomEnvironment.Indoor, RoomAccess.Secure, A(ActivityKind.Idle, ActivityKind.Sleep), F(FacilityKind.PrisonCell), "prison");
            Add(result, "rooms.prison.yard", "Prison Yard", RoomCategory.LawAndOrder, RoomEnvironment.Outdoor, RoomAccess.Secure, A(ActivityKind.Relax, ActivityKind.Socialize), F(FacilityKind.Seat), "prison");
            Add(result, "rooms.court.courtroom", "Courtroom", RoomCategory.LawAndOrder, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.WorkTask, ActivityKind.Idle), F(FacilityKind.Seat, FacilityKind.Desk), "court");
            Add(result, "rooms.court.judges_chambers", "Judge's Chambers", RoomCategory.LawAndOrder, RoomEnvironment.Indoor, RoomAccess.Private, A(ActivityKind.WorkTask), F(FacilityKind.Desk, FacilityKind.LibraryShelf), "court");
            Add(result, "rooms.court.gallows_yard", "Gallows Yard", RoomCategory.LawAndOrder, RoomEnvironment.Outdoor, RoomAccess.Restricted, A(ActivityKind.Idle), F(FacilityKind.Gallows), "execution", "setting-dependent");

            // Medicine, elder care, poverty relief, and funerary rooms.
            Add(result, "rooms.medical.hospital_ward", "Hospital Ward", RoomCategory.Medical, RoomEnvironment.Indoor, RoomAccess.Restricted, A(ActivityKind.Healthcare, ActivityKind.Sleep), F(FacilityKind.MedicalCot, FacilityKind.WashBasin), "hospital");
            Add(result, "rooms.medical.examination_room", "Examination Room", RoomCategory.Medical, RoomEnvironment.Indoor, RoomAccess.Restricted, A(ActivityKind.Healthcare, ActivityKind.WorkTask), F(FacilityKind.MedicalCot), "hospital");
            Add(result, "rooms.medical.surgery", "Surgery", RoomCategory.Medical, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.Healthcare, ActivityKind.WorkTask), F(FacilityKind.SurgeryTable, FacilityKind.WashBasin), "hospital");
            Add(result, "rooms.medical.dispensary", "Dispensary", RoomCategory.Medical, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.Healthcare, ActivityKind.Shop, ActivityKind.WorkTask), F(FacilityKind.ServiceCounter, FacilityKind.Shelf), "pharmacy");
            Add(result, "rooms.medical.isolation_ward", "Isolation Ward", RoomCategory.Medical, RoomEnvironment.Indoor, RoomAccess.Secure, A(ActivityKind.Healthcare, ActivityKind.Sleep), F(FacilityKind.MedicalCot), "hospital", "quarantine");
            Add(result, "rooms.medical.sanatorium_ward", "Sanatorium Ward", RoomCategory.Medical, RoomEnvironment.Indoor, RoomAccess.Restricted, A(ActivityKind.Healthcare, ActivityKind.Sleep, ActivityKind.Relax), F(FacilityKind.MedicalCot, FacilityKind.Seat), "sanatorium");
            Add(result, "rooms.medical.morgue", "Morgue", RoomCategory.Funerary, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.MorgueSlab, FacilityKind.Storage), "hospital", "morgue");
            Add(result, "rooms.medical.autopsy_room", "Autopsy Room", RoomCategory.Funerary, RoomEnvironment.Indoor, RoomAccess.Secure, A(ActivityKind.WorkTask), F(FacilityKind.MorgueSlab, FacilityKind.WashBasin), "morgue");
            Add(result, "rooms.care.almshouse_dormitory", "Almshouse Dormitory", RoomCategory.ElderCare, RoomEnvironment.Indoor, RoomAccess.ResidentsAndGuests, A(ActivityKind.Sleep, ActivityKind.Relax), F(FacilityKind.Bed), "almshouse", "poverty-relief");
            Add(result, "rooms.care.retirement_dormitory", "Retirement Home Dormitory", RoomCategory.ElderCare, RoomEnvironment.Indoor, RoomAccess.ResidentsAndGuests, A(ActivityKind.Sleep, ActivityKind.Relax), F(FacilityKind.Bed, FacilityKind.WashBasin), "retirement-home");
            Add(result, "rooms.care.retirement_common_room", "Retirement Home Common Room", RoomCategory.ElderCare, RoomEnvironment.Indoor, RoomAccess.ResidentsAndGuests, A(ActivityKind.Relax, ActivityKind.Socialize, ActivityKind.Eat), F(FacilityKind.Seat, FacilityKind.DiningTable), "retirement-home");
            Add(result, "rooms.care.workhouse_dormitory", "Workhouse Dormitory", RoomCategory.Civic, RoomEnvironment.Indoor, RoomAccess.Restricted, A(ActivityKind.Sleep, ActivityKind.WorkTask), F(FacilityKind.Bed), "workhouse", "poverty-relief");
            Add(result, "rooms.funerary.undertaker_parlour", "Undertaker's Parlour", RoomCategory.Funerary, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.WorkTask, ActivityKind.Socialize), F(FacilityKind.Seat, FacilityKind.DisplayCase), "undertaker");
            Add(result, "rooms.funerary.graveyard", "Graveyard", RoomCategory.Funerary, RoomEnvironment.Outdoor, RoomAccess.Public, A(ActivityKind.Idle, ActivityKind.Mourn, ActivityKind.WorkTask), F(FacilityKind.GravePlot), "graveyard", "public-poi");
            Add(result, "rooms.funerary.cemetery", "Municipal Cemetery", RoomCategory.Funerary, RoomEnvironment.Outdoor, RoomAccess.Public, A(ActivityKind.Idle, ActivityKind.Mourn, ActivityKind.WorkTask), F(FacilityKind.GravePlot), "cemetery", "public-poi");
            Add(result, "rooms.funerary.mausoleum", "Mausoleum", RoomCategory.Funerary, RoomEnvironment.Indoor, RoomAccess.Restricted, A(ActivityKind.Idle), F(FacilityKind.GravePlot), "cemetery");
            Add(result, "rooms.funerary.crypt", "Crypt", RoomCategory.Funerary, RoomEnvironment.Underground, RoomAccess.Restricted, A(ActivityKind.Idle), F(FacilityKind.GravePlot), "church", "cemetery");

            // Religion, commerce, trades, farms, and industry.
            Add(result, "rooms.religious.church_nave", "Church Nave", RoomCategory.Religious, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.Socialize, ActivityKind.Relax), F(FacilityKind.Seat, FacilityKind.ChapelAltar), "church", "public-poi");
            Add(result, "rooms.religious.chapel", "Chapel", RoomCategory.Religious, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.Relax, ActivityKind.Socialize), F(FacilityKind.ChapelAltar, FacilityKind.Seat), "chapel");
            Add(result, "rooms.religious.vestry", "Vestry", RoomCategory.Religious, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.Storage, FacilityKind.Desk), "church");
            Add(result, "rooms.religious.cloister", "Cloister", RoomCategory.Religious, RoomEnvironment.CoveredOutdoor, RoomAccess.Restricted, A(ActivityKind.Relax), F(FacilityKind.Seat), "monastery");
            Add(result, "rooms.commerce.general_store", "General Store", RoomCategory.Commerce, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.Shop, ActivityKind.WorkTask), F(FacilityKind.ServiceCounter, FacilityKind.Shelf), "shop");
            Add(result, "rooms.commerce.market_hall", "Market Hall", RoomCategory.Commerce, RoomEnvironment.CoveredOutdoor, RoomAccess.Public, A(ActivityKind.Shop, ActivityKind.Socialize), F(FacilityKind.MarketStall), "market", "public-poi");
            Add(result, "rooms.commerce.market_stall", "Market Stall", RoomCategory.Commerce, RoomEnvironment.Outdoor, RoomAccess.Public, A(ActivityKind.Shop, ActivityKind.WorkTask), F(FacilityKind.MarketStall), "market");
            Add(result, "rooms.commerce.apothecary", "Apothecary", RoomCategory.Commerce, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.Shop, ActivityKind.Healthcare), F(FacilityKind.ServiceCounter, FacilityKind.Shelf), "shop", "medical");
            Add(result, "rooms.commerce.bank_lobby", "Banking Hall", RoomCategory.Commerce, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.WorkTask, ActivityKind.Idle), F(FacilityKind.ServiceCounter, FacilityKind.Desk), "bank");
            Add(result, "rooms.commerce.bank_vault", "Bank Vault", RoomCategory.Storage, RoomEnvironment.Indoor, RoomAccess.Secure, A(ActivityKind.WorkTask), F(FacilityKind.Storage), "bank");
            Add(result, "rooms.trade.smithy", "Smithy", RoomCategory.Trade, RoomEnvironment.CoveredOutdoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.Forge, FacilityKind.Workbench), "blacksmith");
            Add(result, "rooms.trade.bakery", "Bakery", RoomCategory.Trade, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.Oven, FacilityKind.Workbench), "baker");
            Add(result, "rooms.trade.tailor_workroom", "Tailor Workroom", RoomCategory.Trade, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.Workbench, FacilityKind.Storage), "tailor");
            Add(result, "rooms.trade.print_shop", "Print Shop", RoomCategory.Trade, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.Workbench, FacilityKind.Storage), "printer");
            Add(result, "rooms.agriculture.barn", "Barn", RoomCategory.Agriculture, RoomEnvironment.CoveredOutdoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.Storage, FacilityKind.Stable), "farm");
            Add(result, "rooms.agriculture.stable", "Stable", RoomCategory.Agriculture, RoomEnvironment.CoveredOutdoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.Stable), "horses");
            Add(result, "rooms.agriculture.field", "Cultivated Field", RoomCategory.Agriculture, RoomEnvironment.Outdoor, RoomAccess.Restricted, A(ActivityKind.WorkTask), F(FacilityKind.Field), "farm");
            Add(result, "rooms.agriculture.orchard", "Orchard", RoomCategory.Agriculture, RoomEnvironment.Outdoor, RoomAccess.Restricted, A(ActivityKind.WorkTask), F(FacilityKind.GardenPlot), "farm");
            Add(result, "rooms.agriculture.greenhouse", "Greenhouse", RoomCategory.Agriculture, RoomEnvironment.Indoor, RoomAccess.Restricted, A(ActivityKind.WorkTask), F(FacilityKind.GardenPlot), "estate", "farm");
            Add(result, "rooms.industry.factory_floor", "Factory Floor", RoomCategory.Industry, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.Workbench), "factory");
            Add(result, "rooms.industry.textile_floor", "Textile Mill Floor", RoomCategory.Industry, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.Loom), "factory", "textile");
            Add(result, "rooms.industry.foundry", "Foundry", RoomCategory.Industry, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.Forge), "factory");
            Add(result, "rooms.industry.gristmill", "Gristmill", RoomCategory.Industry, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.Mill, FacilityKind.Storage), "mill");
            Add(result, "rooms.industry.warehouse", "Warehouse", RoomCategory.Storage, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.Storage), "warehouse");
            Add(result, "rooms.industry.mine_entrance", "Mine Entrance", RoomCategory.Industry, RoomEnvironment.CoveredOutdoor, RoomAccess.Restricted, A(ActivityKind.WorkTask), F(FacilityKind.Storage), "mine");
            Add(result, "rooms.industry.mine_working", "Mine Working", RoomCategory.Industry, RoomEnvironment.Underground, RoomAccess.Restricted, A(ActivityKind.WorkTask), F(FacilityKind.Workbench), "mine");

            // Entertainment, transport, landmarks, and other public POIs.
            Add(result, "rooms.entertainment.theatre_auditorium", "Theatre Auditorium", RoomCategory.Entertainment, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.Relax, ActivityKind.Socialize), F(FacilityKind.Seat), "theatre", "public-poi");
            Add(result, "rooms.entertainment.theatre_stage", "Theatre Stage", RoomCategory.Entertainment, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask), F(FacilityKind.Stage), "theatre");
            Add(result, "rooms.entertainment.backstage", "Theatre Backstage", RoomCategory.Entertainment, RoomEnvironment.Indoor, RoomAccess.StaffOnly, A(ActivityKind.WorkTask, ActivityKind.Relax), F(FacilityKind.Storage, FacilityKind.Wardrobe), "theatre");
            Add(result, "rooms.entertainment.music_hall", "Music Hall", RoomCategory.Entertainment, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.Relax, ActivityKind.Socialize, ActivityKind.Drink), F(FacilityKind.Stage, FacilityKind.Piano), "music-hall");
            Add(result, "rooms.entertainment.dance_hall", "Dance Hall", RoomCategory.Entertainment, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.Relax, ActivityKind.Socialize), F(FacilityKind.Piano, FacilityKind.Seat), "dance-hall");
            Add(result, "rooms.transport.railway_platform", "Railway Platform", RoomCategory.Transport, RoomEnvironment.CoveredOutdoor, RoomAccess.Public, A(ActivityKind.Commute, ActivityKind.Idle), F(FacilityKind.WaitingBench), "railway", "setting-dependent", "public-poi");
            Add(result, "rooms.transport.station_hall", "Railway Station Hall", RoomCategory.Transport, RoomEnvironment.Indoor, RoomAccess.Public, A(ActivityKind.Commute, ActivityKind.Idle), F(FacilityKind.ServiceCounter, FacilityKind.WaitingBench), "railway", "setting-dependent");
            Add(result, "rooms.transport.wharf", "Wharf", RoomCategory.Transport, RoomEnvironment.Outdoor, RoomAccess.Public, A(ActivityKind.Commute, ActivityKind.WorkTask), F(FacilityKind.Dock), "waterfront", "public-poi");
            Add(result, "rooms.transport.canal_landing", "Canal Landing", RoomCategory.Transport, RoomEnvironment.Outdoor, RoomAccess.Public, A(ActivityKind.Commute, ActivityKind.WorkTask), F(FacilityKind.Dock), "canal");
            Add(result, "rooms.public.town_square", "Town Square", RoomCategory.PublicSpace, RoomEnvironment.Outdoor, RoomAccess.Public, A(ActivityKind.Idle, ActivityKind.Relax, ActivityKind.Socialize, ActivityKind.Shop), F(FacilityKind.Seat, FacilityKind.MarketStall), "public-poi");
            Add(result, "rooms.public.park", "Public Park", RoomCategory.PublicSpace, RoomEnvironment.Outdoor, RoomAccess.Public, A(ActivityKind.Relax, ActivityKind.Socialize), F(FacilityKind.Seat, FacilityKind.GardenPlot), "public-poi");
            Add(result, "rooms.public.formal_garden", "Formal Garden", RoomCategory.PublicSpace, RoomEnvironment.Outdoor, RoomAccess.Public, A(ActivityKind.Relax, ActivityKind.Socialize), F(FacilityKind.GardenPlot, FacilityKind.Seat), "public-poi");
            Add(result, "rooms.public.bandstand", "Bandstand", RoomCategory.Entertainment, RoomEnvironment.CoveredOutdoor, RoomAccess.Public, A(ActivityKind.Relax, ActivityKind.Socialize, ActivityKind.WorkTask), F(FacilityKind.Stage, FacilityKind.Seat), "public-poi");
            Add(result, "rooms.public.fountain", "Public Fountain", RoomCategory.PublicSpace, RoomEnvironment.Outdoor, RoomAccess.Public, A(ActivityKind.Idle, ActivityKind.Relax), F(FacilityKind.Seat), "landmark", "public-poi");
            Add(result, "rooms.public.monument", "Civic Monument", RoomCategory.PublicSpace, RoomEnvironment.Outdoor, RoomAccess.Public, A(ActivityKind.Idle, ActivityKind.Socialize), F(FacilityKind.Seat), "landmark", "public-poi");
            Add(result, "rooms.public.promenade", "Promenade", RoomCategory.PublicSpace, RoomEnvironment.Outdoor, RoomAccess.Public, A(ActivityKind.Relax, ActivityKind.Socialize, ActivityKind.Commute), F(FacilityKind.Seat), "public-poi");
            Add(result, "rooms.public.fairground", "Fairground", RoomCategory.Entertainment, RoomEnvironment.Outdoor, RoomAccess.Public, A(ActivityKind.Relax, ActivityKind.Socialize, ActivityKind.Shop), F(FacilityKind.MarketStall, FacilityKind.Stage), "public-poi", "seasonal");

            return result;
        }

        public static RoomDefinition Find(IList<RoomDefinition> definitions, RoomDefinitionId id)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));
            for (int i = 0; i < definitions.Count; i++)
                if (definitions[i] != null && definitions[i].Id == id) return definitions[i];
            return null;
        }

        private static ActivityKind[] A(params ActivityKind[] values) { return values; }
        private static FacilityKind[] F(params FacilityKind[] values) { return values; }

        private static void Add(
            List<RoomDefinition> rooms,
            string id,
            string name,
            RoomCategory category,
            RoomEnvironment environment,
            RoomAccess access,
            ActivityKind[] activities,
            FacilityKind[] facilities,
            params string[] tags)
        {
            var definition = new RoomDefinition
            {
                Id = new RoomDefinitionId(id),
                DisplayName = name,
                Category = category,
                Environment = environment,
                DefaultAccess = access
            };
            definition.SupportedActivities.AddRange(activities);
            definition.Tags.AddRange(tags);
            for (int i = 0; i < facilities.Length; i++)
                definition.SuggestedFacilities.Add(new RoomFacilityDefinition
                {
                    LocalId = facilities[i].ToString().ToLowerInvariant() + "." + i,
                    Kind = facilities[i],
                    SuggestedCapacity = 1,
                    Optional = true
                });
            rooms.Add(definition);
        }
    }
}
