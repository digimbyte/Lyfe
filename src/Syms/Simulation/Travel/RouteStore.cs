using System;
using System.Collections.Generic;
using Syms.Simulation.World;

namespace Syms.Simulation.Travel
{
    public interface IRouteStore
    {
        void Store(CellRoute route);
        bool TryGet(RouteId id, out CellRoute route);
        bool Remove(RouteId id);
    }

    [Serializable]
    public sealed class RouteStore : IRouteStore
    {
        public List<CellRoute> Items = new List<CellRoute>();
        [NonSerialized] private Dictionary<RouteId, CellRoute> _routes;

        public void Store(CellRoute route)
        {
            if (route == null) throw new ArgumentNullException(nameof(route));
            EnsureIndex();
            CellRoute previous;
            if (_routes.TryGetValue(route.Id, out previous)) Items.Remove(previous);
            Items.Add(route);
            _routes[route.Id] = route;
        }

        public bool TryGet(RouteId id, out CellRoute route) { EnsureIndex(); return _routes.TryGetValue(id, out route); }

        public bool Remove(RouteId id)
        {
            EnsureIndex();
            CellRoute route;
            if (!_routes.TryGetValue(id, out route)) return false;
            _routes.Remove(id);
            Items.Remove(route);
            return true;
        }

        private void EnsureIndex()
        {
            if (_routes != null && _routes.Count == Items.Count) return;
            _routes = new Dictionary<RouteId, CellRoute>();
            for (int i = 0; i < Items.Count; i++)
                if (Items[i] != null) _routes[Items[i].Id] = Items[i];
        }
    }
}
