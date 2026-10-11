using System;
using System.Collections.Generic;

namespace StraightWire.Core
{
    /// <summary>
    /// Wires whose target sits left of their source. When one node is above the other, the
    /// crossing runs through the gap between them, skipping anything parked there; only when
    /// there is no gap does it go around. Ported from the prototype's backChannel() and backDiag().
    /// </summary>
    internal static class BackWires
    {
        public static void Prepare(RouteContext ctx)
        {
            var scratch = new List<int>();
            foreach (var w in ctx.Jobs)
                if (w.Routed && w.Kind == WireKind.Back) Channel(ctx, w, scratch);
        }

        public static void Channel(RouteContext ctx, WireJob w, List<int> scratch)
        {
            const double C = Routing.Clearance;
            Rect a = w.A, b = w.B;
            double xl = w.Bx - Routing.Stub - C, xr = w.Ax + Routing.Stub + C;
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
                    w.Yb = (best.Lo + best.Hi) / 2;
                    w.HasBand = true;
                    w.Band0 = best.Lo;
                    w.Band1 = best.Hi;
                    return;
                }
            }
            double above = Math.Min(a.Y0, b.Y0) - Routing.AroundGap;
            double below = Math.Max(a.Y1, b.Y1) + Routing.AroundGap;
            double Cost(double y) => Math.Abs(w.Ay - y) + Math.Abs(y - w.By);
            w.Yb = Cost(above) <= Cost(below) ? above : below;
            w.HasBand = false;
        }

        /// <summary>
        /// In 45° style the crossing becomes one diagonal when the gap is at least as tall as the
        /// horizontal run (keeping a chamfer's room at each end); otherwise null.
        /// </summary>
        public static List<Pt> Diagonal(WireJob w)
        {
            if (!w.HasBand) return null;
            double x1 = w.Ax + Routing.Stub, x2 = w.Bx - Routing.Stub, run = x1 - x2;
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

        public static List<Pt> Orthogonal(WireJob w) => Paths.Back(new Pt(w.Ax, w.Ay), new Pt(w.Bx, w.By), w.Yb);
    }
}
