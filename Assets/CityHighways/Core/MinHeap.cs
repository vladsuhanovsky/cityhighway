using System.Collections.Generic;

namespace CityHighways.Core
{
    /// <summary>
    /// Двоичная куча для алгоритма Дейкстры (в Unity нет PriorityQueue из .NET 6).
    /// При равных приоритетах первым выходит то, что положили раньше, — как heapq с счётчиком в Python-модели.
    /// Устаревшие записи не удаляются: вызывающий пропускает их сам.
    /// </summary>
    internal sealed class MinHeap<T>
    {
        readonly List<(double priority, long order, T item)> _items = new List<(double, long, T)>();
        long _counter;

        public int Count => _items.Count;

        public void Push(double priority, T item)
        {
            _items.Add((priority, _counter++, item));
            int i = _items.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (!Less(i, parent)) break;
                Swap(i, parent);
                i = parent;
            }
        }

        public bool TryPop(out double priority, out T item)
        {
            if (_items.Count == 0)
            {
                priority = 0;
                item = default(T);
                return false;
            }
            var top = _items[0];
            priority = top.priority;
            item = top.item;

            int last = _items.Count - 1;
            _items[0] = _items[last];
            _items.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int left = 2 * i + 1, right = left + 1, smallest = i;
                if (left < _items.Count && Less(left, smallest)) smallest = left;
                if (right < _items.Count && Less(right, smallest)) smallest = right;
                if (smallest == i) break;
                Swap(i, smallest);
                i = smallest;
            }
            return true;
        }

        bool Less(int a, int b) =>
            _items[a].priority < _items[b].priority
            || (_items[a].priority == _items[b].priority && _items[a].order < _items[b].order);

        void Swap(int a, int b)
        {
            var t = _items[a];
            _items[a] = _items[b];
            _items[b] = t;
        }
    }
}
