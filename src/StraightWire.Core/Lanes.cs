using System;
using System.Collections.Generic;
using System.Linq;

namespace StraightWire.Core
{
    /// <summary>
    /// Vertical lanes for forward wires that bend: where each may go (Feasible), and how they
    /// spread out to keep the spacing and cross as little as possible (Relax). Ported from the
    /// prototype's feasible(), crossCount() and relax(), without dragging or animation.
    /// </summary>
    internal static class Lanes
    {
        /// <summary>A pass that moves no lane further than this ends the push-and-shove early.</summary>
        const double Settled = 1e-6;

        /// <summary>Finds each routed lane wire's range and preferred x. Returns them in index order.</summary>
        public static List<WireJob> Prepare(RouteContext ctx)
        {
            var lanes = new List<WireJob>();
            var scratch = new List<int>();
            foreach (var w in ctx.Jobs)
            {
                if (!w.Routed || w.Kind != WireKind.Lane) continue;
                Feasible(ctx, w, scratch);
                w.YLo = Math.Min(w.Ay, w.By);
                w.YHi = Math.Max(w.Ay, w.By);
                w.Pref = w.Lo + (w.Hi - w.Lo) / 2;
                lanes.Add(w);
            }
            return lanes;
        }

        /// <summary>
        /// The x range a wire's vertical run may use: between the two stubs, minus every node
        /// (with clearance) that overlaps the run vertically, and short of any node sitting in
        /// front of either port. The free stretch holding the previous lane wins (hysteresis);
        /// otherwise the widest, and the rightmost on a tie.
        /// </summary>
        public static void Feasible(RouteContext ctx, WireJob w, List<int> scratch)
        {
            const double C = Routing.Clearance;
            double sx = w.Ax, sy = w.Ay, tx = w.Bx, ty = w.By;
            double lo = sx + Routing.Stub, hi = tx - Routing.Stub;
            if (lo > hi)
            {
                w.Lo = w.Hi = (sx + tx) / 2;
                return;
            }
            double lo0 = lo, hi0 = hi;
            double y0 = Math.Min(sy, ty), y1 = Math.Max(sy, ty);
            var forbidden = new List<(double Lo, double Hi)>();
            ctx.NodeGrid.Query(new Rect(lo0 - C, y0 - C, hi0 + C, y1 + C), scratch);
            foreach (int k in scratch)
            {
                if (k == w.ANode || k == w.BNode) continue;
                var n = ctx.NodeRects[k];
                double x0 = n.X0 - C, x1 = n.X1 + C, ny0 = n.Y0 - C, ny1 = n.Y1 + C;
                if (x1 <= lo0 || x0 >= hi0) continue;
                if (sy > ny0 && sy < ny1 && x0 > sx) hi = Math.Min(hi, x0);
                if (ty > ny0 && ty < ny1 && x1 < tx) lo = Math.Max(lo, x1);
                if (ny1 > y0 && ny0 < y1) forbidden.Add((x0, x1));
            }
            if (lo > hi)
            {
                w.Lo = lo0;
                w.Hi = hi0;
                return;
            }
            var spans = new List<(double Lo, double Hi)> { (lo, hi) };
            foreach (var (p, q) in forbidden) spans = Intervals.Cut(spans, p, q);
            if (spans.Count == 0)
            {
                w.Lo = lo;
                w.Hi = hi;
                return;
            }
            if (w.PrevLane is double probe)
            {
                bool found = false;
                (double Lo, double Hi) pick = default;
                double pd = double.PositiveInfinity;
                foreach (var s in spans)
                {
                    if (s.Hi - s.Lo < 4) continue;
                    double d = probe < s.Lo ? s.Lo - probe : probe > s.Hi ? probe - s.Hi : 0;
                    if (d < pd)
                    {
                        pd = d;
                        pick = s;
                        found = true;
                    }
                }
                if (found && pd <= 1)
                {
                    w.Lo = pick.Lo;
                    w.Hi = pick.Hi;
                    return;
                }
            }
            var best = spans[0];
            foreach (var s in spans)
            {
                double ds = (s.Hi - s.Lo) - (best.Hi - best.Lo);
                if (ds > 1 || (ds > -1 && s.Lo > best.Lo)) best = s;
            }
            w.Lo = best.Lo;
            w.Hi = best.Hi;
        }

        static int Hv(double hy, double hx0, double hx1, double vx, double vy0, double vy1)
        {
            double x0 = Math.Min(hx0, hx1), x1 = Math.Max(hx0, hx1), y0 = Math.Min(vy0, vy1), y1 = Math.Max(vy0, vy1);
            return vx > x0 + 0.01 && vx < x1 - 0.01 && hy > y0 + 0.01 && hy < y1 - 0.01 ? 1 : 0;
        }

        /// <summary>How many times two right-angle wires cross with lanes at xa and xb.</summary>
        public static int CrossCount(WireJob a, double xa, WireJob b, double xb) =>
            Hv(a.Ay, a.Ax, xa, xb, b.Ay, b.By) + Hv(a.By, xa, a.Bx, xb, b.Ay, b.By) +
            Hv(b.Ay, b.Ax, xb, xa, a.Ay, a.By) + Hv(b.By, xb, b.Bx, xa, a.Ay, a.By);

        /// <summary>
        /// One-dimensional push-and-shove. Lanes whose runs overlap vertically are pushed at least
        /// <paramref name="sp"/> apart, in the left-right order that crosses least; lanes from the
        /// same source are pulled together so they share a trunk. Every lane stays in its range.
        /// </summary>
        public static void Relax(IReadOnlyList<WireJob> lanes, double sp)
        {
            foreach (var w in lanes) w.P = Geo.Clamp(w.Pref, w.Lo, w.Hi);
            var pairs = Pairs(lanes, sp);
            var before = new double[lanes.Count];
            for (int it = 0; it < Routing.RelaxIterations; it++)
            {
                for (int i = 0; i < lanes.Count; i++) before[i] = lanes[i].P;
                foreach (var pr in pairs)
                {
                    if (pr.Same)
                    {
                        double d = pr.B.P - pr.A.P;
                        pr.A.P += d * 0.25;
                        pr.B.P -= d * 0.25;
                    }
                    else
                    {
                        double gap = pr.Right.P - pr.Left.P;
                        if (gap >= sp) continue;
                        double d = sp - gap;
                        pr.Left.P -= d / 2;
                        pr.Right.P += d / 2;
                    }
                }
                double moved = 0;
                for (int i = 0; i < lanes.Count; i++)
                {
                    var w = lanes[i];
                    w.P = Geo.Clamp(w.P, w.Lo, w.Hi);
                    moved = Math.Max(moved, Math.Abs(w.P - before[i]));
                }
                // Settled: further passes would move nothing visible. The prototype always ran 32.
                if (moved < Settled) break;
            }
            foreach (var w in lanes)
            {
                double span = w.Hi - w.Lo;
                w.Lane = span > 0.5 ? w.P : w.Lo + span / 2;
            }
        }

        sealed class Pair
        {
            public WireJob A, B, Left, Right;
            public bool Same;
        }

        /// <summary>
        /// The pairs that interact, found by a sweep over runs sorted by top edge instead of every
        /// pair of lanes. They are processed in (lower index, higher index) order, the prototype's
        /// double-loop order, so results do not depend on the sweep.
        /// </summary>
        static List<Pair> Pairs(IReadOnlyList<WireJob> lanes, double sp)
        {
            var keys = new List<(int I, int J)>();
            int[] byY = Enumerable.Range(0, lanes.Count).OrderBy(i => lanes[i].YLo).ThenBy(i => i).ToArray();
            for (int s = 0; s < byY.Length; s++)
            {
                var a = lanes[byY[s]];
                for (int t = s + 1; t < byY.Length; t++)
                {
                    var b = lanes[byY[t]];
                    if (b.YLo >= a.YHi + sp) break;
                    if (a.Group == b.Group) continue;   // paired below, whatever their heights
                    if (a.Hi + sp <= b.Lo || b.Hi + sp <= a.Lo) continue;
                    keys.Add(byY[s] < byY[t] ? (byY[s], byY[t]) : (byY[t], byY[s]));
                }
            }
            var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (int i = 0; i < lanes.Count; i++)
            {
                if (!groups.TryGetValue(lanes[i].Group, out var g)) groups[lanes[i].Group] = g = new List<int>();
                g.Add(i);
            }
            foreach (var g in groups.Values)
                for (int i = 0; i < g.Count; i++)
                    for (int j = i + 1; j < g.Count; j++)
                        keys.Add((g[i], g[j]));
            keys.Sort();

            var pairs = new List<Pair>(keys.Count);
            foreach (var (i, j) in keys)
            {
                WireJob a = lanes[i], b = lanes[j];
                if (a.Group == b.Group)
                {
                    pairs.Add(new Pair { A = a, B = b, Same = true });
                    continue;
                }
                double m = (a.P + b.P) / 2, x1 = m - sp / 2, x2 = m + sp / 2;
                int cl = CrossCount(a, x1, b, x2), cr = CrossCount(a, x2, b, x1);
                bool aLeft;
                if (cl != cr) aLeft = cl < cr;
                else
                {
                    double da = a.PrevLane ?? a.P, db = b.PrevLane ?? b.P;
                    aLeft = Math.Abs(da - db) > 0.5 ? da < db
                          : a.Pref != b.Pref ? a.Pref < b.Pref
                          : a.Index < b.Index;
                }
                pairs.Add(aLeft ? new Pair { A = a, B = b, Left = a, Right = b }
                                : new Pair { A = a, B = b, Left = b, Right = a });
            }
            return pairs;
        }
    }
}
