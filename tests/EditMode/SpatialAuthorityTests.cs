using NUnit.Framework;
using Syms.Simulation.World;
using Syms.Simulation.Activities;
using Syms.Simulation.People;

namespace Syms.Tests.EditMode
{
    public sealed class SpatialAuthorityTests
    {
        [Test]
        public void StreamingUsesOneBlockLoadAndTwoBlockUnloadHysteresis()
        {
            var policy = new StreamingAuthorityPolicy();

            Assert.That(policy.Evaluate(1, false), Is.EqualTo(StreamingAction.Materialize));
            Assert.That(policy.Evaluate(2, true), Is.EqualTo(StreamingAction.None));
            Assert.That(policy.Evaluate(3, true), Is.EqualTo(StreamingAction.Demote));
        }

        [Test]
        public void PoseRequiresMatchingActivityTopologyAndRouteProgress()
        {
            var pose = new LastObservedPose { ActivityRevision = 2, TopologyVersion = 3, RouteProgress = 0.5f };

            Assert.That(pose.IsValid(2, 3, 0.5f), Is.True);
            Assert.That(pose.IsValid(3, 3, 0.5f), Is.False);
            Assert.That(pose.IsValid(2, 4, 0.5f), Is.False);
            Assert.That(pose.IsValid(2, 3, 0.6f), Is.False);
        }

        [Test]
        public void ActivityChangeInvalidatesObservedPose()
        {
            var sim = new Sim("sim", "A", "B");
            sim.Spatial.LastObservedPose = new LastObservedPose();

            sim.StartActivity(new ActivitySegment { Kind = ActivityKind.Toilet, EndTime = 10d }, 0d);

            Assert.That(sim.Spatial.LastObservedPose, Is.Null);
        }
    }
}
