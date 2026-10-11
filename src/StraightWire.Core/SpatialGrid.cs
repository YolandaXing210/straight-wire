using System;
using System.Collections.Generic;

namespace StraightWire.Core
{
    /// <summary>
    /// A uniform grid of rectangles keyed by int id. It turns "which nodes or wires are near
    /// this?" from a scan of everything into a look at a few cells.
    /// </summary>
    internal sealed class SpatialGrid
    {
        readonly double _cell;
        readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
        readonly Dictionary<int, Rect> _items = new Dictionary<int, Rect>();
        readonly HashSet<int> _seen = new HashSet<int>();

        public SpatialGrid(double cell)
        {
            _cell = cell;
        }

        /// <summary>Adds an item, or moves it if the id is already in the grid.</summary>
        public void Add(int id, Rect r)
        {
            Remove(id);
            _items[id] = r;
            if (!Range(r, out long x0, out long x1, out long y0, out long y1)) return;
            for (long x = x0; x <= x1; x++)
                for (long y = y0; y <= y1; y++)
                {
                    long key = Key(x, y);
                    if (!_cells.TryGetValue(key, out var list)) _cells[key] = list = new List<int>();
                    list.Add(id);
                }
        }

        public void Remove(int id)
        {
            if (!_items.TryGetValue(id, out var r)) return;
            _items.Remove(id);
            if (!Range(r, out long x0, out long x1, out long y0, out long y1)) return;
            for (long x = x0; x <= x1; x++)
                for (long y = y0; y <= y1; y++)
                    if (_cells.TryGetValue(Key(x, y), out var list)) list.Remove(id);
        }

        /// <summary>Fills <paramref name="into"/> with the ids whose rectangle intersects r, ascending, once each.</summary>
        public void Query(Rect r, List<int> into)
        {
            into.Clear();
            _seen.Clear();
            if (!Range(r, out long x0, out long x1, out long y0, out long y1)) return;
            for (long x = x0; x <= x1; x++)
                for (long y = y0; y <= y1; y++)
                {
                    if (!_cells.TryGetValue(Key(x, y), out var list)) continue;
                    foreach (int id in list)
                        if (_seen.Add(id) && _items[id].Intersects(r)) into.Add(id);
                }
            into.Sort();
        }

        bool Range(Rect r, out long x0, out long x1, out long y0, out long y1)
        {
            x0 = x1 = y0 = y1 = 0;
            if (!IsFinite(r.X0) || !IsFinite(r.Y0) || !IsFinite(r.X1) || !IsFinite(r.Y1)) return false;
            x0 = (long)Math.Floor(r.X0 / _cell);
            x1 = (long)Math.Floor(r.X1 / _cell);
            y0 = (long)Math.Floor(r.Y0 / _cell);
            y1 = (long)Math.Floor(r.Y1 / _cell);
            return true;
        }

        static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        static long Key(long x, long y) => (x << 32) ^ (y & 0xffffffffL);
    }
}
