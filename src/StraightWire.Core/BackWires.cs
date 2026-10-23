using System;
using System.Collections.Generic;
using System.Linq;

namespace StraightWire.Core
{
    /// <summary>
    /// Wires whose target sits left of their source. When one node is above the other, the
    /// crossing runs through the gap between them, skipping anything parked there; only when
    /// there is no gap does it go around. Ported from the prototype's backChannel() and backDiag().
    ///
    /// The user can set each of the three runs between the stubs (SceneWire.Back). The out run
    /// belongs to the output, so all back wires from one output share it; a set vertical steps
    /// past any component it would cross, as a dragged lane does.
    /// </summary>
    internal static class BackWires
    {
        public static void Prepare(RouteContext ctx)
        {
            var scratch = new List<int>();
            var groups = new Dictionary<string, List<WireJob>>(StringComparer.Ordinal);
            foreach (var w in ctx.Jobs)
            {
                if (!w.Routed || w.Kind != WireKind.Back) continue;
                if (!groups.TryGetValue(w.Group, out var g)) groups[w.Group] = g = new List<WireJob>();
                g.Add(w);
            }
            foreach (var g in groups.Values)
            {
                double? outRun = g.Select(w => w.Wire.Back?.Out).FirstOrDefault(o => o != null);
                foreach (var w in g)
                {
                    double? inRun = w.Wire.Back?.In;
                    w.OutX = w.Ax + Math.Max(Routing.Stub, outRun ?? Routing.Stub);
                    w.InX = w.Bx - Math.Max(Routing.Stub, inRun ?? Routing.Stub);
                    Channel(ctx, w, scratch);
                    if (inRun != null)
                        w.InX = Clear(ctx, w.InX, double.NegativeInfinity, w.Bx - Routing.Stub,
                                      Math.Min(w.By, w.Yb), Math.Max(w.By, w.Yb), new[] { w.ANode, w.BNode });
                }
                if (outRun == null) continue;
                var first = g[0];
                double y0 = Math.Min(first.Ay, g.Min(w => w.Yb)), y1 = Math.Max(first.Ay, g.Max(w => w.Yb));
                var own = g.Select(w => w.BNode).Append(first.ANode).ToArray();
                double x = Clear(ctx, first.OutX, first.Ax + Routing.Stub, double.PositiveInfinity, y0, y1, own);
                foreach (var w in g) w.OutX = x;
            }
        }

        /// <summary>
        /// Where a set vertical spanning y0..y1 goes: the nearest x to <paramref name="x"/> within
        /// lo..hi that keeps clearance from every node overlapping that span, except the wire's own.
        /// Few verticals are set, so every node is looked at rather than a range of the grid.
        /// </summary>
        static double Clear(RouteContext ctx, double x, double lo, double hi, double y0, double y1, int[] own)
        {
            const double C = Routing.Clearance;
            var free = new List<(double Lo, double Hi)> { (lo, hi) };
            for (int k = 0; k < ctx.NodeRects.Length; k++)
            {
                if (Array.IndexOf(own, k) >= 0) continue;
                var n = ctx.NodeRects[k];
                if (n.Y1 + C > y0 && n.Y0 - C < y1) free = Intervals.Cut(free, n.X0 - C, n.X1 + C);
            }
            bool found = false;
            (double Lo, double Hi) best = default;
            double bestDistance = double.PositiveInfinity;
            foreach (var s in free)
            {
                double d = x < s.Lo ? s.Lo - x : x > s.Hi ? x - s.Hi : 0;
                if (d >= bestDistance) continue;
                best = s;
                bestDistance = d;
                found = true;
            }
            return found ? Geo.Clamp(x, best.Lo, best.Hi) : x;
        }

        public static void Channel(RouteContext ctx, WireJob w, List<int> scratch)
        {
            const double C = Routing.Clearance;
            Rect a = w.A, b = w.B;
            double xl = w.InX - C, xr = w.OutX + C;
            bool hasGap = false;
            double g0 = 0, g1 = 0;
            if (b.Y1 + 2 * C <= a.Y0)
            {
                hasGap = true;
                g0 = b.Y1 + C;
                g1 = a.Y0 - C;
            }
            else if (a.Y1 + 2 * C <= b.Y0)
            {
                hasGap = true;
                g0 = a.Y1 + C;
                g1 = b.Y0 - C;
            }
            if (hasGap)
            {
                var free = new List<(double Lo, double Hi)> { (g0, g1) };
                ctx.NodeGrid.Query(new Rect(xl - C, g0 - C, xr + C, g1 + C), scratch);
                foreach (int k in scratch)
                {
                    if (k == w.ANode || k == w.BNode) continue;
                    var n = ctx.NodeRects[k];
                    if (n.X1 + C < xl || n.X0 - C > xr) continue;
                    free = Intervals.Cut(free, n.Y0 - C, n.Y1 + C);
                }
                bool found = false;
                (double Lo, double Hi) best = default;
                foreach (var s in free)
                {
                    if (s.Hi - s.Lo < 4) continue;
                    if (!found || s.Hi - s.Lo > best.Hi - best.Lo)
                    {
                        best = s;
                        found = true;
                    }
                }
                if (found)
                {
                    w.AutoYb = (best.Lo + best.Hi) / 2;
                    w.Yb = w.AutoYb;
                    if (w.Wire.Back?.Cross is double cross)
                    {
                        // A set crossing may sit in any free stretch of the gap: the one nearest where it was put.
                        double want = w.AutoYb + cross, distance = double.PositiveInfinity;
                        foreach (var s in free)
                        {
                            if (s.Hi - s.Lo < 4) continue;
                            double d = want < s.Lo ? s.Lo - want : want > s.Hi ? want - s.Hi : 0;
                            if (d >= distance) continue;
                            best = s;
                            distance = d;
                        }
                        w.Yb = Geo.Clamp(want, best.Lo, best.Hi);
                    }
                    w.HasBand = true;
                    w.Band0 = best.Lo;
                    w.Band1 = best.Hi;
                    return;
                }
            }
            double above = Math.Min(a.Y0, b.Y0) - Routing.AroundGap;
            double below = Math.Max(a.Y1, b.Y1) + Routing.AroundGap;
            double Cost(double y) => Math.Abs(w.Ay - y) + Math.Abs(y - w.By);
            bool goAbove = Cost(above) <= Cost(below);
            w.AutoYb = goAbove ? above : below;
            w.Yb = w.AutoYb;
            if (w.Wire.Back?.Cross is double around)
                w.Yb = goAbove ? Math.Min(w.AutoYb + around, Math.Min(a.Y0, b.Y0) - C)
                               : Math.Max(w.AutoYb + around, Math.Max(a.Y1, b.Y1) + C);
            w.HasBand = false;
        }

        /// <summary>
        /// In 45° style the crossing becomes one diagonal when the gap is at least as tall as the
        /// horizontal run (keeping a chamfer's room at each end); otherwise null.
        /// </summary>
        public static List<Pt> Diagonal(WireJob w)
        {
            if (!w.HasBand) return null;
            double x1 = w.OutX, x2 = w.InX, run = x1 - x2;
            double sg = w.By < w.Ay ? -1 : 1, m = Routing.Chamfer + 2;
            double lo = Math.Max(w.Band0, Math.Min(w.Ay, w.By) + m);
            double hi = Math.Min(w.Band1, Math.Max(w.Ay, w.By) - m);
            if (hi - lo < run) return null;
            double mid = Geo.Clamp(w.Yb, lo + run / 2, hi - run / 2);
            double yp = mid - sg * run / 2, yq = mid + sg * run / 2;
            return new List<Pt>
            {
                new Pt(w.Ax, w.Ay), new Pt(x1, w.Ay), new Pt(x1, yp),
                new Pt(x2, yq), new Pt(x2, w.By), new Pt(w.Bx, w.By),
            };
        }

        public static List<Pt> Orthogonal(WireJob w) => Paths.Back(new Pt(w.Ax, w.Ay), new Pt(w.Bx, w.By), w.OutX, w.InX, w.Yb);
    }
}
