using NUnit.Framework;
using Syms.Simulation.City;
using Syms.Content;
using Syms.Simulation.Jobs;
using Syms.Simulation.People;
using Syms.Simulation.Social;
using Syms.Simulation.World;

namespace Syms.Tests.EditMode
{
    public sealed class RelationshipTests
    {
        [Test]
        public void NewPartnerPreservesPreviousPartnerAsEx()
        {
            var graph = new RelationshipGraph();
            graph.SetPartners("a", "b", 1d);

            graph.SetPartners("a", "c", 2d);

            Assert.That(graph.Find("a", "b").HasKind(RelationshipKind.Partner), Is.False);
            Assert.That(graph.Find("a", "b").HasKind(RelationshipKind.ExPartner), Is.True);
            Assert.That(graph.Find("a", "c").HasKind(RelationshipKind.Partner), Is.True);
        }

        [Test]
        public void AffinityIsClampedToSimpleRelationshipRange()
        {
            var graph = new RelationshipGraph();

            SocialRelationship relationship = graph.SetAffinity("a", "b", 500, 0d);
            relationship.AdjustAffinity(-500);

            Assert.That(relationship.Affinity, Is.EqualTo(-100));
        }

        [Test]
        public void FamilyRolesRemainDirectional()
        {
            var graph = new RelationshipGraph();

            SocialRelationship relationship = graph.SetFamily(
                "parent", FamilyRole.Parent, "child", FamilyRole.Child, 0d);

            Assert.That(relationship.GetFamilyRole("parent"), Is.EqualTo(FamilyRole.Parent));
            Assert.That(relationship.GetFamilyRole("child"), Is.EqualTo(FamilyRole.Child));
        }

        [Test]
        public void BiasRulesModifyEffectiveAffinityWithoutChangingStoredRelationship()
        {
            var observer = new Sim("observer", "A", "A");
            observer.Demographics.Gender = Gender.Woman;
            var target = new Sim("target", "B", "B");
            target.Demographics.Gender = Gender.Man;
            var graph = new RelationshipGraph();
            SocialRelationship relationship = graph.SetAffinity(observer.Id, target.Id, 20, 0d);
            var biases = new SocialBiasProfile();
            biases.Rules.Add(new SocialBiasRule
            {
                HasObserverGender = true,
                ObserverGender = Gender.Woman,
                HasTargetGender = true,
                TargetGender = Gender.Man,
                AffinityModifier = -15
            });

            int effective = new FamilyDynamicsService(graph, biases).GetEffectiveAffinity(observer, target);

            Assert.That(effective, Is.EqualTo(5));
            Assert.That(relationship.Affinity, Is.EqualTo(20));
        }

        [Test]
        public void CityQueriesCurrentPartnerExPartnersAndFamily()
        {
            var city = new CitySimulation("Town");
            city.AddSim(new Sim("a", "A", "Family"));
            city.AddSim(new Sim("b", "B", "Family"));
            city.AddSim(new Sim("c", "C", "Family"));
            city.InitializePartners("a", "b");
            city.InitializePartners("a", "c");
            city.InitializeFamily("a", FamilyRole.Sibling, "b", FamilyRole.Sibling);

            Assert.That(city.GetPartner("a").Id, Is.EqualTo("c"));
            Assert.That(city.GetExPartners("a")[0].Id, Is.EqualTo("b"));
            Assert.That(city.GetFamily("a")[0].Id, Is.EqualTo("b"));
        }

        [Test]
        public void ImmediateCirclesConnectHouseholdsWorkplacesAndNeighboursOnly()
        {
            var city = new CitySimulation("Town");
            Sim alice = AddWorker(city, "alice", "smithy");
            Sim bob = AddWorker(city, "bob", "smithy");
            Sim child = AddWorker(city, "child", null);
            Sim neighbour = AddWorker(city, "neighbour", "smithy");
            Sim stranger = AddWorker(city, "stranger", "bakery");
            city.InitializeResidence(alice.Id, "household.a", new BuildingId("home.a"), Home("block.1", "home.a"));
            city.InitializeResidence(bob.Id, "household.a", new BuildingId("home.a"), Home("block.1", "home.a"));
            city.InitializeResidence(child.Id, "household.a", new BuildingId("home.a"), Home("block.1", "home.a"));
            city.InitializeResidence(neighbour.Id, "household.b", new BuildingId("home.b"), Home("block.1", "home.b"));
            city.InitializeResidence(stranger.Id, "household.c", new BuildingId("home.c"), Home("block.9", "home.c"));
            city.InitializeFamily(alice.Id, FamilyRole.Parent, child.Id, FamilyRole.Child);

            SocialCircleRefreshResult result = city.RefreshImmediateRelationships();

            Assert.That(city.Relationships.Find("alice", "bob").HasKind(RelationshipKind.Roommate), Is.True);
            Assert.That(city.Relationships.Find("alice", "child").HasKind(RelationshipKind.Household), Is.True);
            Assert.That(city.Relationships.Find("alice", "child").HasKind(RelationshipKind.Roommate), Is.False);
            Assert.That(city.Relationships.Find("alice", "neighbour").HasKind(RelationshipKind.Neighbour), Is.True);
            Assert.That(city.Relationships.Find("bob", "neighbour").HasKind(RelationshipKind.Colleague), Is.True);
            Assert.That(city.Relationships.Find("alice", "stranger"), Is.Null);
            Assert.That(result.RelationshipCount, Is.EqualTo(city.Relationships.Relationships.Count));
        }

        [Test]
        public void RefreshRemovesObsoleteNeutralContextRelationship()
        {
            var city = new CitySimulation("Town");
            Sim first = AddWorker(city, "first", "smithy");
            Sim second = AddWorker(city, "second", "smithy");
            city.RefreshImmediateRelationships();
            Assert.That(city.Relationships.Find(first.Id, second.Id), Is.Not.Null);

            second.Employment.Contract.Active = false;
            city.RefreshImmediateRelationships();

            Assert.That(city.Relationships.Find(first.Id, second.Id), Is.Null);
        }

        private static Sim AddWorker(CitySimulation city, string id, string workplace)
        {
            var sim = new Sim(id, id, "Test");
            if (!string.IsNullOrEmpty(workplace))
            {
                sim.Employment.Contract = new EmploymentContract
                {
                    Id = "employment." + id,
                    Job = new JobDefinitionId("jobs.retail.clerk"),
                    Workplace = new PlaceId(workplace),
                    Active = true
                };
            }
            city.AddSim(sim);
            return sim;
        }

        private static WorldAddress Home(string block, string place)
        {
            return new WorldAddress(
                new ChunkId("town"),
                new BlockId(block),
                new PlaceId(place),
                new CellId(place + ".cell"),
                new AnchorId(place + ".bed"));
        }
    }
}
