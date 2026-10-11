using System;
using System.Collections.Generic;

namespace StraightWire.Core
{
    /// <summary>
    /// Dots where wires from the same source part ways [diverge, junctions]. Without them a
    /// shared trunk reads as one wire.
    /// </summary>
    internal static class Junctions
    {
        const double SplitDistance = 0.6, PortGap = 3, MergeDistance = 1.5;
        const int BisectSteps = 14;

        /// <summary>Where <paramref name="a"/> first leaves <paramref name="b"/>, refined by bisection; null if it never does.</summary>
        public static Pt? Diverge(IReadOnlyList<Pt> a, IReadOnlyList<Pt> b)
        {
            for (int i = 1; i < a.Count; i++)
            {
                if (Geo.PolyDist(a[i], b) <= SplitDistance) continue;
                Pt p = a[i - 1], q = a[i];
                double lo = 0, hi = 1;
                for (int k = 0; k < BisectSteps; k++)
                {
                    double m = (lo + hi) / 2;
                    if (Geo.PolyDist(Lerp(p, q, m), b) <= SplitDistance) lo = m;
                    else hi = m;
                }
                return Lerp(p, q, lo);
            }
            return null;
        }

        static Pt Lerp(Pt p, Pt q, double t) => new Pt(p.X + (q.X - p.X) * t, p.Y + (q.Y - p.Y) * t);

        /// <summary>
        /// Dots for every same-source group, groups in order of first appearance. A group with any
        /// unrouted (Curve) wire gets none: a dot where a curve leaves would float in mid-air.
        /// </summary>
        public static List<Junction> Find(IReadOnlyList<(string Group, bool Routed, Pt[] Path)> wires)
        {
            var order = new List<string>();
            var groups = new Dictionary<string, List<(bool Routed, Pt[] Path)>>(StringComparer.Ordinal);
            foreach (var (group, routed, path) in wires)
            {
                if (!groups.TryGetValue(group, out var list))
                {
                    groups[group] = list = new List<(bool Routed, Pt[] Path)>();
                    order.Add(group);
                }
                list.Add((routed, path));
            }
            var dots = new List<Junction>();
            foreach (string group in order)
            {
                var list = groups[group];
                if (list.Count < 2 || list.Exists(e => !e.Routed)) continue;
                for (int i = 0; i < list.Count; i++)
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        var split = Diverge(list[i].Path, list[j].Path);
                        if (split == null) continue;
                        var p = split.Value;
                        if (Pt.Dist(p, list[i].Path[0]) < PortGap) continue;
                        if (dots.Exists(d => Pt.Dist(d.At, p) < MergeDistance)) continue;
                        dots.Add(new Junction(group, p));
                    }
            }
            return dots;
        }
    }
}
