using System;
using System.Collections.Generic;
using System.Linq;

namespace StraightWire.Core
{
    /// <summary>What dragging a part of a wire does.</summary>
    public enum SegmentAction
    {
        /// <summary>Selects only.</summary>
        None,
        /// <summary>Slides an automatic wire's lane sideways.</summary>
        Lane,
        /// <summary>Slides a hand-drawn wire's segment sideways.</summary>
        Segment,
    }

    public readonly struct WireHit
    {
        public WireHit(string id, int segment, double distance)
        {
            Id = id;
            Segment = segment;
            Distance = distance;
        }

        public string Id { get; }

        /// <summary>Index of the segment hit: it runs from path[Segment] to path[Segment + 1].</summary>
        public int Segment { get; }

        public double Distance { get; }
    }

    /// <summary>
    /// The geometry of editing a wire by hand, ported from the prototype: hit-testing, snapping,
    /// and moving, sliding, inserting and removing points. A hand-drawn wire's path is its start,
    /// its interior points, and its end; "points" below always means the interior ones.
    /// </summary>
    public static class WireEdit
    {
        static readonly (double X, double Y)[] Directions =
        {
            (1, 0), (0, 1), (Math.Sqrt(0.5), Math.Sqrt(0.5)), (Math.Sqrt(0.5), -Math.Sqrt(0.5)),
        };

        /// <summary>The nearest wire segment within <paramref name="tolerance"/> of p, or null.</summary>
        public static WireHit? HitWire(IEnumerable<KeyValuePair<string, Pt[]>> paths, Pt p, double tolerance)
        {
            WireHit? best = null;
            double bestDistance = tolerance;
            foreach (var kv in paths)
            {
                var path = kv.Value;
                for (int i = 0; i < path.Length - 1; i++)
                {
                    double d = Geo.SegDist(p, path[i], path[i + 1]);
                    if (d >= bestDistance) continue;
                    bestDistance = d;
                    best = new WireHit(kv.Key, i, d);
                }
            }
            return best;
        }

        /// <summary>The interior point within <paramref name="tolerance"/> of p (as an index among the interior points), or -1.</summary>
        public static int HitHandle(Pt[] path, Pt p, double tolerance)
        {
            for (int i = 1; i < path.Length - 1; i++)
                if (Math.Abs(p.X - path[i].X) <= tolerance && Math.Abs(p.Y - path[i].Y) <= tolerance) return i - 1;
            return -1;
        }

        /// <summary>
        /// What a drag on <paramref name="segment"/> does: an automatic lane wire's middle run
        /// slides its lane; any segment of a hand-drawn wire (with points) slides; anything else
        /// only selects.
        /// </summary>
        public static SegmentAction ActionFor(bool laneWire, bool manual, int segment, int pathLength)
        {
            if (laneWire && segment >= 1 && segment <= pathLength - 3) return SegmentAction.Lane;
            if (manual && pathLength > 2) return SegmentAction.Segment;
            return SegmentAction.None;
        }

        /// <summary>
        /// Which run of an automatic backwards wire <paramref name="segment"/> belongs to, given its
        /// runs as routed: the verticals by their x, anything between them the crossing (with its
        /// corners in 45° style), the stubs and their corners none.
        /// </summary>
        public static BackRun BackRunFor(Pt[] path, BackRuns routed, int segment)
        {
            if (segment <= 0 || segment >= path.Length - 2) return BackRun.None;
            double outX = path[0].X + (routed.Out ?? Routing.Stub), inX = path[path.Length - 1].X - (routed.In ?? Routing.Stub);
            bool VerticalAt(int i, double x) => Math.Abs(path[i].X - x) < 0.01 && Math.Abs(path[i + 1].X - x) < 0.01;
            if (VerticalAt(segment, outX)) return BackRun.Out;
            if (VerticalAt(segment, inX)) return BackRun.In;
            int first = -1, last = -1;
            for (int i = 0; i < path.Length - 1; i++)
            {
                if (first < 0 && VerticalAt(i, outX)) first = i;
                if (VerticalAt(i, inX)) last = i;
            }
            return first >= 0 && last > first && segment > first && segment < last ? BackRun.Cross : BackRun.None;
        }

        /// <summary>
        /// The runs a drag of <paramref name="run"/> makes: the stored runs, with that run at its routed
        /// value moved by the mouse (sideways for a vertical, up or down for the crossing).
        /// </summary>
        public static BackRuns DragBack(BackRuns stored, BackRuns routed, BackRun run, double dx, double dy)
        {
            double delta = run == BackRun.Out ? dx : run == BackRun.In ? -dx : dy;
            return stored.With(run, (routed[run] ?? 0) + delta);
        }

        public static List<Pt> PointsOf(Pt[] path) => path.Skip(1).Take(Math.Max(0, path.Length - 2)).ToList();

        /// <summary>
        /// Where a dragged point lands. Near a 0°, 45° or 90° line through either neighbour it
        /// lands on that line, or on the crossing of one line from each neighbour when that is
        /// close; otherwise on the grid. Free (Option held) leaves it where it is.
        /// </summary>
        public static Pt Snap(Pt raw, Pt a, Pt b, double tolerance, double grid, bool free)
        {
            if (free) return raw;
            var lines = new List<(Pt Origin, (double X, double Y) Dir, Pt Foot, double Dist)>();
            foreach (var origin in new[] { a, b })
                foreach (var d in Directions)
                {
                    double t = (raw.X - origin.X) * d.X + (raw.Y - origin.Y) * d.Y;
                    var foot = new Pt(origin.X + d.X * t, origin.Y + d.Y * t);
                    double dist = Pt.Dist(raw, foot);
                    if (dist < tolerance) lines.Add((origin, d, foot, dist));
                }
            Pt? best = null;
            double bestDist = double.PositiveInfinity;
            foreach (var la in lines)
                foreach (var lb in lines)
                {
                    if (!la.Origin.Equals(a) || !lb.Origin.Equals(b)) continue;
                    double den = la.Dir.X * lb.Dir.Y - la.Dir.Y * lb.Dir.X;
                    if (Math.Abs(den) < 1e-6) continue;
                    double t = ((lb.Origin.X - la.Origin.X) * lb.Dir.Y - (lb.Origin.Y - la.Origin.Y) * lb.Dir.X) / den;
                    var x = new Pt(la.Origin.X + la.Dir.X * t, la.Origin.Y + la.Dir.Y * t);
                    double dist = Pt.Dist(x, raw);
                    if (dist < tolerance * 1.5 && dist < bestDist)
                    {
                        best = x;
                        bestDist = dist;
                    }
                }
            if (best != null) return best.Value;
            if (lines.Count > 0) return lines.OrderBy(l => l.Dist).First().Foot;
            return new Pt(Math.Round(raw.X / grid) * grid, Math.Round(raw.Y / grid) * grid);
        }

        public static List<Pt> MovePoint(IReadOnlyList<Pt> points, int index, Pt p)
        {
            var result = points.ToList();
            result[index] = p;
            return result;
        }

        /// <summary>
        /// Slides segment <paramref name="segment"/> of <paramref name="path0"/> (the path when the
        /// drag began) by the mouse movement, perpendicular to itself; the interior points at its
        /// two ends move, the neighbouring segments stretch. Horizontal and vertical segments move
        /// in whole grid steps unless free; free only turns the grid off, the slide stays sideways.
        /// </summary>
        public static List<Pt> SlideSegment(Pt[] path0, IReadOnlyList<Pt> points0, int segment, double dx, double dy, double grid, bool free)
        {
            Pt a = path0[segment], b = path0[segment + 1];
            double length = Pt.Dist(a, b);
            if (length == 0) length = 1;
            double nx = -(b.Y - a.Y) / length, ny = (b.X - a.X) / length;
            double s = dx * nx + dy * ny;
            if (!free && (Math.Abs(nx) < 1e-6 || Math.Abs(ny) < 1e-6)) s = Math.Round(s / grid) * grid;
            dx = nx * s;
            dy = ny * s;
            var result = points0.ToList();
            foreach (int j in new[] { segment - 1, segment })
                if (j >= 0 && j < result.Count) result[j] = new Pt(result[j].X + dx, result[j].Y + dy);
            return result;
        }

        /// <summary>A new point on <paramref name="segment"/> nearest to <paramref name="at"/>, inserted between that segment's ends.</summary>
        public static List<Pt> InsertPoint(Pt[] path, int segment, Pt at)
        {
            Pt a = path[segment], b = path[segment + 1];
            double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
            double t = l2 > 0 ? Geo.Clamp(((at.X - a.X) * dx + (at.Y - a.Y) * dy) / l2, 0, 1) : 0;
            var result = PointsOf(path);
            result.Insert(segment, new Pt(a.X + dx * t, a.Y + dy * t));
            return result;
        }

        public static List<Pt> RemovePoint(IReadOnlyList<Pt> points, int index)
        {
            var result = points.ToList();
            result.RemoveAt(index);
            return result;
        }
    }
}
