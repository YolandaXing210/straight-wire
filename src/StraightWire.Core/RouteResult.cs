using System;
using System.Collections.Generic;

namespace StraightWire.Core
{
    /// <summary>A dot where wires from the same source part ways.</summary>
    public sealed class Junction
    {
        public Junction(string group, Pt at)
        {
            Group = group;
            At = at;
        }

        public string Group { get; }
        public Pt At { get; }
    }

    public sealed class RouteResult
    {
        public RouteResult(IReadOnlyDictionary<string, Pt[]> paths, IReadOnlyList<Junction> junctions,
                           IReadOnlyDictionary<string, double> laneFractions = null)
        {
            Paths = paths;
            Junctions = junctions;
            LaneFractions = laneFractions ?? new Dictionary<string, double>(StringComparer.Ordinal);
        }

        /// <summary>The polyline of every routed wire, by wire id. Curve wires have no entry.</summary>
        public IReadOnlyDictionary<string, Pt[]> Paths { get; }

        public IReadOnlyList<Junction> Junctions { get; }

        /// <summary>
        /// For every automatic lane wire, where its lane sits in its free range (0 = left end,
        /// 1 = right end). A finished lane drag stores this.
        /// </summary>
        public IReadOnlyDictionary<string, double> LaneFractions { get; }
    }

    /// <summary>
    /// What routing remembers between calls: each lane wire's last lane x. It keeps a lane in the
    /// free stretch it was in while a component is dragged, instead of jumping at every tie.
    /// </summary>
    public sealed class RouterState
    {
        readonly Dictionary<string, double> _lanes = new Dictionary<string, double>(StringComparer.Ordinal);

        public int Count => _lanes.Count;

        public bool TryGetLane(string wireId, out double x) => _lanes.TryGetValue(wireId, out x);

        internal void Replace(IEnumerable<KeyValuePair<string, double>> lanes)
        {
            _lanes.Clear();
            foreach (var kv in lanes) _lanes[kv.Key] = kv.Value;
        }
    }
}
