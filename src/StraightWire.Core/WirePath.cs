using System;
using System.Collections.Generic;

namespace StraightWire.Core
{
    /// <summary>A wire's final polyline from the decisions made so far [pathFor, without animation].</summary>
    internal static class WirePath
    {
        public static List<Pt> For(WireJob w)
        {
            var a = new Pt(w.Ax, w.Ay);
            var b = new Pt(w.Bx, w.By);
            bool diagonal = w.Style == WireStyle.Diagonal;
            switch (w.Kind)
            {
                case WireKind.Straight:
                    return Paths.Dedupe(new[] { a, b });
                case WireKind.Manual:
                    var points = new List<Pt>(w.Wire.Points.Count + 2) { a };
                    points.AddRange(w.Wire.Points);
                    points.Add(b);
                    return Paths.Dedupe(points);
                case WireKind.Lane:
                    if (!diagonal) return Paths.Simplify(Paths.Dedupe(Paths.Lane(a, b, w.Lane)));
                    double h = Math.Abs(w.By - w.Ay) / 2, c0 = Math.Min(Routing.Chamfer, h);
                    return Paths.Simplify(Paths.Diagonal(w.Ax, w.Ay, w.Bx, w.By,
                        w.Lane + (w.Diag ? w.Offset : 0), w.Diag ? h : c0));
                default:
                    var p = Paths.Dedupe((diagonal ? BackWires.Diagonal(w) : null) ?? BackWires.Orthogonal(w));
                    return Paths.Simplify(diagonal ? Paths.Dedupe(Paths.Chamfer(p, Routing.Chamfer)) : p);
            }
        }
    }
}
