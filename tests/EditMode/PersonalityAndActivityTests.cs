using System.Collections.Generic;
using NUnit.Framework;
using Syms.Simulation.Activities;
using Syms.Simulation.People;
using Syms.Simulation.Stats;

namespace Syms.Tests.EditMode
{
    public sealed class PersonalityAndActivityTests
    {
        [Test]
        public void GridProducesSixteenStablePersonalityTypes()
        {
            var types = new HashSet<PersonalityType>();
            foreach (Conduct conduct in System.Enum.GetValues(typeof(Conduct)))
            foreach (Concern concern in System.Enum.GetValues(typeof(Concern)))
            {
                var personality = new Personality { Conduct = conduct, Concern = concern };
                types.Add(personality.Type);
            }

            Assert.That(types.Count, Is.EqualTo(16));
        }

        [Test]
        public void LazyRebelliousSimValuesWorkLessThanDisciplinedWorkaholic()
        {
            var work = new ActivityCandidate { Kind = ActivityKind.WorkTask, WorkObligation = 60f };
            var candidates = new[] { work };
            var planner = new DeterministicActivityPlanner();
            var diligent = new Sim("diligent", "A", "A");
            diligent.Personality.Conduct = Conduct.Disciplined;
            diligent.Traits.Add(Trait.Workaholic);
            var slacker = new Sim("slacker", "B", "B");
            slacker.Personality.Conduct = Conduct.Rebellious;
            slacker.Traits.Add(Trait.Lazy);
            var diligentScores = new List<ActivityScore>();
            var slackerScores = new List<ActivityScore>();

            planner.Choose(diligent, candidates, 0, diligentScores);
            planner.Choose(slacker, candidates, 0, slackerScores);

            Assert.That(diligentScores[0].Total, Is.GreaterThan(slackerScores[0].Total));
        }

        [Test]
        public void CriticalBladderNeedOverridesOrdinaryRelaxation()
        {
            var sim = new Sim("sim", "A", "B");
            sim.Stats.Set(SimStat.Bladder, 100f);
            var candidates = new[]
            {
                new ActivityCandidate { Kind = ActivityKind.Relax, BaseUtility = 20f },
                new ActivityCandidate { Kind = ActivityKind.Toilet }
            };

            ActivityCandidate choice = new DeterministicActivityPlanner().Choose(sim, candidates, 0, null);

            Assert.That(choice.Kind, Is.EqualTo(ActivityKind.Toilet));
        }

        [Test]
        public void SameStateAndTimeBucketProduceSameDecision()
        {
            var sim = new Sim("stable", "A", "B");
            var candidates = new[]
            {
                new ActivityCandidate { Kind = ActivityKind.Idle, BaseUtility = 1f },
                new ActivityCandidate { Kind = ActivityKind.Relax, BaseUtility = 1f }
            };
            var planner = new DeterministicActivityPlanner();

            ActivityCandidate first = planner.Choose(sim, candidates, 42, null);
            ActivityCandidate second = planner.Choose(sim, candidates, 42, null);

            Assert.That(second.Kind, Is.EqualTo(first.Kind));
        }
    }
}
