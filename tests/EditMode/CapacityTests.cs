using NUnit.Framework;
using Syms.Simulation.Activities;
using Syms.Simulation.World;

namespace Syms.Tests.EditMode
{
    public sealed class CapacityTests
    {
        [Test]
        public void CapacityRejectsExcessReservationAndReopensAfterExpiry()
        {
            var service = new CapacityService();
            var key = new CapacityKey(new PlaceId("home"), FacilityKind.Toilet);
            service.SetCapacity(key, 1);
            CapacityReservation first;
            CapacityReservation second;

            Assert.That(service.TryReserve(key, "one", 10d, out first), Is.True);
            Assert.That(service.TryReserve(key, "two", 10d, out second), Is.False);
            service.ReleaseExpired(10d);
            Assert.That(service.TryReserve(key, "two", 20d, out second), Is.True);
        }

        [Test]
        public void ListBackedStateRebuildsRuntimeIndexesAfterLoad()
        {
            var key = new CapacityKey(new PlaceId("home"), FacilityKind.Bed);
            var restored = new CapacityService();
            restored.Definitions.Add(new CapacityDefinition { Key = key, Capacity = 1 });
            restored.Reservations.Add(new CapacityReservation
            {
                Id = "saved",
                Key = key,
                SimId = "sim",
                ExpiresAt = 100d
            });

            Assert.That(restored.HasAvailability(key), Is.False);
            Assert.That(restored.Release("saved"), Is.True);
            Assert.That(restored.HasAvailability(key), Is.True);
        }
    }
}
