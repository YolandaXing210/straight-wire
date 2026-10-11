using System;
using System.Collections.Generic;

namespace StraightWire.Core
{
    /// <summary>Polyline builders and clean-up, ported from the prototype.</summary>
    internal static class Paths
    {
        const double Same = 0.01;

        /// <summary>
        /// Drops points within 0.01 of the previous kept point [dedupe]. Unlike the prototype, the
        /// exact last point is always kept, so a path ends on its port even when a point lands
        /// right next to it.
        /// </summary>
        public static List<Pt> Dedupe(IReadOnlyList<Pt> p)
        {
            var o = new List<Pt>(p.Count) { p[0] };
            for (int i = 1; i < p.Count; i++)
            {
                var q = o[o.Count - 1];
                if (Math.Abs(p[i].X - q.X) > Same || Math.Abs(p[i].Y - q.Y) > Same) o.Add(p[i]);
            }
            var last = p[p.Count - 1];
            if (o.Count == 1) o.Add(last);
            else o[o.Count - 1] = last;
            return o;
        }

        /// <summary>Merges runs of collinear, same-direction segments [simplify].</summary>
        public static List<Pt> Simplify(IReadOnlyList<Pt> p)
        {
            if (p.Count < 3) return new List<Pt>(p);
            var o = new List<Pt>(p.Count) { p[0] };
            for (int i = 1; i < p.Count - 1; i++)
            {
                Pt a = o[o.Count - 1], b = p[i], c = p[i + 1];
                double ux = b.X - a.X, uy = b.Y - a.Y, vx = c.X - b.X, vy = c.Y - b.Y;
                double lu = Math.Sqrt(ux * ux + uy * uy), lv = Math.Sqrt(vx * vx + vy * vy);
                if (lu > 0 && lv > 0 && Math.Abs(ux * vy - uy * vx) / (lu * lv) < 1e-4 && ux * vx + uy * vy > 0) continue;
                o.Add(b);
            }
            o.Add(p[p.Count - 1]);
            return o;
        }

        /// <summary>
        /// Cuts each right-angle corner back by up to <paramref name="c"/> along both legs [chamfer].
        /// Corners that are not about 90° (a 45° turn is already a clean trace corner) are left alone.
        /// </summary>
        public static List<Pt> Chamfer(IReadOnlyList<Pt> p, double c)
        {
            int n = p.Count;
            if (n < 3) return new List<Pt>(p);
            var o = new List<Pt>(n * 2) { p[0] };
            for (int i = 1; i < n - 1; i++)
            {
                Pt a = p[i - 1], v = p[i], b = p[i + 1];
                double la = Pt.Dist(a, v), lb = Pt.Dist(v, b);
                double ba = i - 1 == 0 ? la - 2 : la / 2, bb = i + 1 == n - 1 ? lb - 2 : lb / 2;
                double d = Math.Max(0, Math.Min(c, Math.Min(ba, bb)));
                bool square = la > 0 && lb > 0 &&
                              Math.Abs((v.X - a.X) * (b.X - v.X) + (v.Y - a.Y) * (b.Y - v.Y)) < 0.1 * la * lb;
                if (d < 0.01 || !square)
                {
                    o.Add(v);
                    continue;
                }
                o.Add(new Pt(v.X + (a.X - v.X) / la * d, v.Y + (a.Y - v.Y) / la * d));
                o.Add(new Pt(v.X + (b.X - v.X) / lb * d, v.Y + (b.Y - v.Y) / lb * d));
            }
            o.Add(p[n - 1]);
            return o;
        }

        /// <summary>
        /// A vertical run at x with 45° cuts of size c at both ends [diagPath]. c = |dy|/2 collapses it
        /// into one diagonal. The cut shrinks to keep 2 units of horizontal run at each port.
        /// </summary>
        public static List<Pt> Diagonal(double ax, double ay, double bx, double by, double x, double c)
        {
            double sg = by < ay ? -1 : 1;
            c = Math.Max(0, Math.Min(Math.Min(c, Math.Abs(by - ay) / 2), Math.Min(x - ax - 2, bx - 2 - x)));
            return Dedupe(new[]
            {
                new Pt(ax, ay), new Pt(x - c, ay), new Pt(x, ay + sg * c),
                new Pt(x, by - sg * c), new Pt(x + c, by), new Pt(bx, by),
            });
        }

        /// <summary>Out to the lane at x, along it, and in [orthoPath for a lane wire].</summary>
        public static List<Pt> Lane(Pt a, Pt b, double x) =>
            new List<Pt> { a, new Pt(x, a.Y), new Pt(x, b.Y), b };

        /// <summary>A backwards wire: a stub out, across at y, a stub in [orthoPath for a back wire].</summary>
        public static List<Pt> Back(Pt a, Pt b, double y)
        {
            double x1 = a.X + Routing.Stub, x2 = b.X - Routing.Stub;
            return new List<Pt> { a, new Pt(x1, a.Y), new Pt(x1, y), new Pt(x2, y), new Pt(x2, b.Y), b };
        }
    }
}
