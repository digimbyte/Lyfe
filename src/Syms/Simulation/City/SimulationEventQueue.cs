using System;
using System.Collections.Generic;

namespace Syms.Simulation.City
{
    internal struct SimWakeEvent
    {
        public string SimId;
        public double Time;
        public long Sequence;
    }

    internal sealed class SimulationEventQueue
    {
        private readonly List<SimWakeEvent> _heap = new List<SimWakeEvent>();

        public int Count { get { return _heap.Count; } }

        public void Enqueue(SimWakeEvent item)
        {
            _heap.Add(item);
            int index = _heap.Count - 1;
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (_heap[parent].Time <= item.Time) break;
                _heap[index] = _heap[parent];
                index = parent;
            }
            _heap[index] = item;
        }

        public bool TryPeek(out SimWakeEvent item)
        {
            if (_heap.Count == 0) { item = default(SimWakeEvent); return false; }
            item = _heap[0];
            return true;
        }

        public SimWakeEvent Dequeue()
        {
            if (_heap.Count == 0) throw new InvalidOperationException("The queue is empty.");
            SimWakeEvent result = _heap[0];
            SimWakeEvent tail = _heap[_heap.Count - 1];
            _heap.RemoveAt(_heap.Count - 1);
            if (_heap.Count == 0) return result;

            int index = 0;
            while (true)
            {
                int left = (index * 2) + 1;
                if (left >= _heap.Count) break;
                int right = left + 1;
                int child = right < _heap.Count && _heap[right].Time < _heap[left].Time ? right : left;
                if (_heap[child].Time >= tail.Time) break;
                _heap[index] = _heap[child];
                index = child;
            }
            _heap[index] = tail;
            return result;
        }
    }
}
