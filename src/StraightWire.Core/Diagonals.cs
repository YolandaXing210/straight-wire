using System;
using System.Collections.Generic;
using System.Linq;

namespace StraightWire.Core
{
    /// <summary>
    /// In 45° style a lane wire tries to become one straight diagonal (two bends). It keeps the
    /// diagonal only if that touches no more wires and nodes than its four-bend route would.
    /// Wires are decided shortest rise first, each against the others' current paths.
    /// Ported from the prototype's decideDiag() and touches().
    /// </summary>
    internal static class Diagonals
    {
        public static void Decide(RouteContext ctx)
        {
            var grid = new SpatialGrid(Routing.GridCell);
            foreach (var w in ctx.Jobs)
            {
                w.Diag = false;
                w.Offset = 0;
                w.Target = null;
                if (!w.Routed) continue;
                w.Target = w.Kind == WireKind.Lane && w.Style == WireStyle.Diagonal
                    ? Paths.Diagonal(w.Ax, w.Ay, w.Bx, w.By, w.Lane, Routing.Chamfer)
                    : WirePath.For(w);
                w.TargetBox = Geo.BBox(w.Target, 0);
                grid.Add(w.Index, w.TargetBox);
            }

            double thr = ctx.Sp * Routing.TouchFraction;
            var wireHits = new List<int>();
            var nodeHits = new List<int>();
            var candidates = ctx.Jobs
                .Where(w => w.Routed && w.Kind == WireKind.Lane && w.Style == WireStyle.Diagonal)
                .OrderBy(w => Math.Abs(w.By - w.Ay)).ThenBy(w => w.Index)
                .ToList();
            foreach (var w in candidates)
            {
                double h = Math.Abs(w.By - w.Ay) / 2;
                if (h <= Routing.Chamfer)
                {
                    w.Diag = true;
                    continue;
                }
                double lo = w.Ax + Routing.DiagonalStub + h, hi = w.Bx - Routing.DiagonalStub - h;
                if (lo > hi) continue;
                int baseline = Touches(ctx, grid, w.Target, w, thr, wireHits, nodeHits);
                foreach (double off in Offsets(w.Lane, lo, hi, ctx.Sp))
                {
                    double x = w.Lane + off;
                    if (x < lo - 0.01 || x > hi + 0.01) continue;
                    var p = Paths.Diagonal(w.Ax, w.Ay, w.Bx, w.By, x, h);
                    if (Touches(ctx, grid, p, w, thr, wireHits, nodeHits) > baseline) continue;
                    w.Diag = true;
                    w.Offset = off;
                    w.Target = p;
                    w.TargetBox = Geo.BBox(p, 0);
                    grid.Add(w.Index, w.TargetBox);
                    break;
                }
            }
        }

        /// <summary>0, ±SP … ±8SP, then the two ends of the range; nearest first, ties in that order.</summary>
        static IEnumerable<double> Offsets(double lane, double lo, double hi, double sp)
        {
            var offs = new List<double> { 0 };
            for (int k = 1; k <= 8; k++)
            {
                offs.Add(k * sp);
                offs.Add(-k * sp);
            }
            offs.Add(lo - lane);
            offs.Add(hi - lane);
            return offs.Select((o, i) => (o, i)).OrderBy(t => Math.Abs(t.o)).ThenBy(t => t.i).Select(t => t.o).ToList();
        }

        /// <summary>
        /// How many other wires (not from the same source) come within <paramref name="thr"/> of
        /// the path, plus how many nodes (other than its own two, padded by 3) it crosses.
        /// </summary>
        internal static int Touches(RouteContext ctx, SpatialGrid wires, List<Pt> path, WireJob w, double thr,
                                    List<int> wireHits, List<int> nodeHits)
        {
            var bb = Geo.BBox(path, thr);
            int n = 0;
            wires.Query(bb, wireHits);
            foreach (int k in wireHits)
            {
                var o = ctx.Jobs[k];
                if (o == w || o.Target == null || o.Group == w.Group) continue;
                if (Geo.PolyPolyWithin(path, o.Target, thr)) n++;
            }
            ctx.NodeGrid.Query(bb.Inflate(Routing.TouchPad), nodeHits);
            foreach (int k in nodeHits)
            {
                if (k == w.ANode || k == w.BNode) continue;
                var r = ctx.NodeRects[k].Inflate(Routing.TouchPad);
                if (!bb.Intersects(r)) continue;
                for (int i = 0; i < path.Count - 1; i++)
                {
                    if (!Geo.SegRectHit(path[i], path[i + 1], r)) continue;
                    n++;
                    break;
                }
            }
            return n;
        }
    }
}
