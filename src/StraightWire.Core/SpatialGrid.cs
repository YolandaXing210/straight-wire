using System;
using System.Collections.Generic;

namespace StraightWire.Core
{
    /// <summary>
    /// A uniform grid of rectangles keyed by small non-negative int ids (node and job indices).
    /// It turns "which nodes or wires are near this?" from a scan of everything into a look at a
    /// few cells.
    ///
    /// Moving an item is the hot path (every accepted 45° diagonal moves its wire), so cells are
    /// never searched to take an id out: each cell entry carries the id's version at the time it
    /// was added, and entries whose version is out of date are skipped by queries.
    /// </summary>
    internal sealed class SpatialGrid
    {
        readonly double _cell;
        readonly Dictionary<long, List<long>> _cells = new Dictionary<long, List<long>>();
        Rect[] _rects = new Rect[64];
        int[] _version = new int[64];
        bool[] _present = new bool[64];

        // Query stamps each id it has looked at, instead of clearing a set per query.
        int[] _stamp = new int[64];
        int _query;

        public SpatialGrid(double cell)
        {
            _cell = cell;
        }

        /// <summary>Adds an item, or moves it if the id is already in the grid.</summary>
        public void Add(int id, Rect r)
        {
            Grow(id);
            int version = ++_version[id];
            _rects[id] = r;
            _present[id] = true;
            if (!Range(r, out long x0, out long x1, out long y0, out long y1)) return;
            long entry = ((long)version << 32) | (uint)id;
            for (long x = x0; x <= x1; x++)
                for (long y = y0; y <= y1; y++)
                {
                    long key = Key(x, y);
                    if (!_cells.TryGetValue(key, out var list)) _cells[key] = list = new List<long>();
                    list.Add(entry);
                }
        }

        public void Remove(int id)
        {
            if (id < 0 || id >= _present.Length || !_present[id]) return;
            _present[id] = false;
            _version[id]++;
        }

        /// <summary>Fills <paramref name="into"/> with the ids whose rectangle intersects r, ascending, once each.</summary>
        public void Query(Rect r, List<int> into)
        {
            into.Clear();
            if (++_query == int.MaxValue)
            {
                Array.Clear(_stamp, 0, _stamp.Length);
                _query = 1;
            }
            if (!Range(r, out long x0, out long x1, out long y0, out long y1)) return;
            for (long x = x0; x <= x1; x++)
                for (long y = y0; y <= y1; y++)
                {
                    if (!_cells.TryGetValue(Key(x, y), out var list)) continue;
                    foreach (long entry in list)
                    {
                        int id = (int)(uint)entry, version = (int)(entry >> 32);
                        if (!_present[id] || _version[id] != version || _stamp[id] == _query) continue;
                        _stamp[id] = _query;
                        if (_rects[id].Intersects(r)) into.Add(id);
                    }
                }
            into.Sort();
        }

        void Grow(int id)
        {
            if (id < _rects.Length) return;
            int size = Math.Max(id + 1, _rects.Length * 2);
            Array.Resize(ref _rects, size);
            Array.Resize(ref _version, size);
            Array.Resize(ref _present, size);
            Array.Resize(ref _stamp, size);
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
