using System;
using System.Collections.Generic;
using System.Globalization;

namespace StraightWire.Core
{
    /// <summary>A point in Grasshopper canvas units: pixels at zoom 1, y pointing down.</summary>
    public readonly struct Pt : IEquatable<Pt>
    {
        public Pt(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }

        public static double Dist(Pt a, Pt b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public bool Equals(Pt other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Pt other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###})", X, Y);
    }

    /// <summary>An axis-aligned rectangle given by its four edges.</summary>
    public readonly struct Rect
    {
        public Rect(double x0, double y0, double x1, double y1)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
        }

        public static Rect FromXYWH(double x, double y, double w, double h) => new Rect(x, y, x + w, y + h);

        public double X0 { get; }
        public double Y0 { get; }
        public double X1 { get; }
        public double Y1 { get; }

        public Rect Inflate(double d) => new Rect(X0 - d, Y0 - d, X1 + d, Y1 + d);

        /// <summary>Closed test: rectangles that only touch along an edge intersect.</summary>
        public bool Intersects(Rect o) => X0 <= o.X1 && o.X0 <= X1 && Y0 <= o.Y1 && o.Y0 <= Y1;

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "[{0:0.###}, {1:0.###} .. {2:0.###}, {3:0.###}]", X0, Y0, X1, Y1);
    }

    /// <summary>Distance and hit tests, ported from the prototype's geometry helpers.</summary>
    public static class Geo
    {
        /// <summary>The prototype's clamp: never throws, and an empty range (lo &gt; hi) clamps as written.</summary>
        public static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;

        public static double SegDist(Pt p, Pt a, Pt b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
            double t = l2 > 0 ? Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1) : 0;
            double ex = p.X - a.X - dx * t, ey = p.Y - a.Y - dy * t;
            return Math.Sqrt(ex * ex + ey * ey);
        }

        public static double PolyDist(Pt p, IReadOnlyList<Pt> poly)
        {
            double m = double.PositiveInfinity;
            for (int i = 0; i < poly.Count - 1; i++) m = Math.Min(m, SegDist(p, poly[i], poly[i + 1]));
            return m;
        }

        static double Orient(Pt a, Pt b, Pt c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        public static double SegSegDist(Pt p1, Pt p2, Pt q1, Pt q2)
        {
            double d1 = Orient(q1, q2, p1), d2 = Orient(q1, q2, p2), d3 = Orient(p1, p2, q1), d4 = Orient(p1, p2, q2);
            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0))) return 0;
            return Math.Min(Math.Min(SegDist(p1, q1, q2), SegDist(p2, q1, q2)), Math.Min(SegDist(q1, p1, p2), SegDist(q2, p1, p2)));
        }

        /// <summary>
        /// True when some segment of <paramref name="a"/> comes closer than <paramref name="d"/> to
        /// some segment of <paramref name="b"/>. Segment pairs whose boxes are more than d apart are
        /// skipped before the exact test; that is what keeps 45° decisions fast on big canvases.
        /// </summary>
        public static bool PolyPolyWithin(IReadOnlyList<Pt> a, IReadOnlyList<Pt> b, double d)
        {
            // A segment of a farther than d from b's whole box cannot come within d of b.
            double bx0 = double.PositiveInfinity, by0 = double.PositiveInfinity;
            double bx1 = double.NegativeInfinity, by1 = double.NegativeInfinity;
            for (int j = 0; j < b.Count; j++)
            {
                var q = b[j];
                if (q.X < bx0) bx0 = q.X;
                if (q.X > bx1) bx1 = q.X;
                if (q.Y < by0) by0 = q.Y;
                if (q.Y > by1) by1 = q.Y;
            }
            for (int i = 0; i < a.Count - 1; i++)
            {
                Pt p1 = a[i], p2 = a[i + 1];
                double ax0 = Math.Min(p1.X, p2.X) - d, ax1 = Math.Max(p1.X, p2.X) + d;
                double ay0 = Math.Min(p1.Y, p2.Y) - d, ay1 = Math.Max(p1.Y, p2.Y) + d;
                if (bx1 < ax0 || bx0 > ax1 || by1 < ay0 || by0 > ay1) continue;
                for (int j = 0; j < b.Count - 1; j++)
                {
                    Pt q1 = b[j], q2 = b[j + 1];
                    if (Math.Max(q1.X, q2.X) < ax0 || Math.Min(q1.X, q2.X) > ax1 ||
                        Math.Max(q1.Y, q2.Y) < ay0 || Math.Min(q1.Y, q2.Y) > ay1) continue;
                    if (SegSegDist(p1, p2, q1, q2) < d) return true;
                }
            }
            return false;
        }

        /// <summary>Liang–Barsky: does segment a–b touch rectangle r (edges included)?</summary>
        public static bool SegRectHit(Pt a, Pt b, Rect r)
        {
            double t0 = 0, t1 = 1, dx = b.X - a.X, dy = b.Y - a.Y;
            double[] p = { -dx, dx, -dy, dy };
            double[] q = { a.X - r.X0, r.X1 - a.X, a.Y - r.Y0, r.Y1 - a.Y };
            for (int i = 0; i < 4; i++)
            {
                if (p[i] == 0)
                {
                    if (q[i] < 0) return false;
                    continue;
                }
                double t = q[i] / p[i];
                if (p[i] < 0)
                {
                    if (t > t1) return false;
                    if (t > t0) t0 = t;
                }
                else
                {
                    if (t < t0) return false;
                    if (t < t1) t1 = t;
                }
            }
            return true;
        }

        public static Rect BBox(IReadOnlyList<Pt> poly, double pad)
        {
            double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity;
            double x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
            foreach (var p in poly)
            {
                if (p.X < x0) x0 = p.X;
                if (p.X > x1) x1 = p.X;
                if (p.Y < y0) y0 = p.Y;
                if (p.Y > y1) y1 = p.Y;
            }
            return new Rect(x0 - pad, y0 - pad, x1 + pad, y1 + pad);
        }
    }

    /// <summary>Sorted, disjoint intervals on one axis.</summary>
    internal static class Intervals
    {
        /// <summary>Removes (p, q) from every interval in the list. The result stays sorted.</summary>
        public static List<(double Lo, double Hi)> Cut(List<(double Lo, double Hi)> spans, double p, double q)
        {
            var next = new List<(double Lo, double Hi)>(spans.Count + 1);
            foreach (var (u, v) in spans)
            {
                if (q <= u || p >= v)
                {
                    next.Add((u, v));
                    continue;
                }
                if (p > u) next.Add((u, p));
                if (q < v) next.Add((q, v));
            }
            return next;
        }
    }
}
