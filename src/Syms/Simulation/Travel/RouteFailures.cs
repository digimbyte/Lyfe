using System;
using System.Collections.Generic;
using Syms.Content;
using Syms.Simulation.Activities;
using Syms.Simulation.People;
using Syms.Simulation.World;

namespace Syms.Simulation.Travel
{
    public enum RouteFailureKind { Blocked, Confined, Delayed, Stranded }

    [Serializable]
    public sealed class RouteFailureState
    {
        public RouteFailureKind Kind;
        public PlaceId DestinationPlace;
        public RoomId DestinationRoom;
        public ActivityKind RequestedActivity;
        public GatewayId Gateway;
        public string GatewayArcId;
        public double BlockedAt;
        public bool HasSafeAlternative;
        public bool HasDangerousAlternative;
        public List<string> Dependencies = new List<string>();

        public static RouteFailureState Stranded(RoomId destination, double cityTime, string dependency)
        {
            var result = new RouteFailureState { Kind = RouteFailureKind.Stranded,
                DestinationRoom = destination, BlockedAt = cityTime };
            if (!string.IsNullOrEmpty(dependency)) result.Dependencies.Add(dependency);
            return result;
        }
    }

    public interface IRouteDependencyIndex
    {
        void Register(string simId, RouteFailureState failure);
        void RegisterRoute(string simId, RouteId route, IList<string> dependencies);
        void Clear(string simId);
        IReadOnlyList<string> GetWaitingSims(string dependency);
        IReadOnlyList<string> GetConfinedSims(BuildingId building);
        IReadOnlyList<RouteId> GetDependentRoutes(string dependency);
    }

    public sealed class RouteDependencyIndex : IRouteDependencyIndex
    {
        private readonly Dictionary<string, HashSet<string>> _byDependency
            = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<string>> _simDependencies
            = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, RouteId> _simRoutes
            = new Dictionary<string, RouteId>(StringComparer.Ordinal);

        public void Register(string simId, RouteFailureState failure)
        {
            if (string.IsNullOrWhiteSpace(simId)) throw new ArgumentException("A Sim ID is required.", nameof(simId));
            Clear(simId);
            if (failure == null) return;
            var keys = new List<string>();
            for (int i = 0; i < failure.Dependencies.Count; i++)
            {
                string dependency = failure.Dependencies[i];
                if (string.IsNullOrEmpty(dependency) || keys.Contains(dependency)) continue;
                keys.Add(dependency);
                HashSet<string> sims;
                if (!_byDependency.TryGetValue(dependency, out sims))
                { sims = new HashSet<string>(StringComparer.Ordinal); _byDependency.Add(dependency, sims); }
                sims.Add(simId);
            }
            _simDependencies[simId] = keys;
        }

        public void RegisterRoute(string simId, RouteId route, IList<string> dependencies)
        {
            var failure = new RouteFailureState();
            if (dependencies != null)
                for (int i = 0; i < dependencies.Count; i++) failure.Dependencies.Add(dependencies[i]);
            Register(simId, failure);
            _simRoutes[simId] = route;
        }

        public void Clear(string simId)
        {
            List<string> dependencies;
            if (!_simDependencies.TryGetValue(simId, out dependencies)) return;
            for (int i = 0; i < dependencies.Count; i++)
            {
                HashSet<string> sims;
                if (!_byDependency.TryGetValue(dependencies[i], out sims)) continue;
                sims.Remove(simId);
                if (sims.Count == 0) _byDependency.Remove(dependencies[i]);
            }
            _simDependencies.Remove(simId);
            _simRoutes.Remove(simId);
        }

        public IReadOnlyList<string> GetWaitingSims(string dependency)
        {
            HashSet<string> sims;
            if (!_byDependency.TryGetValue(dependency ?? string.Empty, out sims)) return Array.Empty<string>();
            var result = new List<string>(sims);
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        public IReadOnlyList<string> GetConfinedSims(BuildingId building)
        { return GetWaitingSims("building:" + building.Value); }

        public IReadOnlyList<RouteId> GetDependentRoutes(string dependency)
        {
            IReadOnlyList<string> sims = GetWaitingSims(dependency);
            var result = new List<RouteId>();
            for (int i = 0; i < sims.Count; i++)
            {
                RouteId route;
                if (_simRoutes.TryGetValue(sims[i], out route) && !result.Contains(route)) result.Add(route);
            }
            result.Sort((a, b) => string.CompareOrdinal(a.Value, b.Value));
            return result;
        }
    }
}
