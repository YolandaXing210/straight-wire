using System;
using System.Collections.Generic;

namespace StraightWire.Core
{
    /// <summary>
    /// Keeps hand-drawn points with their wire when its components move. Points remember where
    /// both components were when they were last settled: if both have since moved by the same
    /// amount (dragged together), the points move with them; if only one moved, the points stay
    /// and the end segment stretches. Either way the anchors are brought up to date, so each
    /// move is judged on its own, and undoing a move settles the points straight back.
    /// </summary>
    public static class Anchoring
    {
        const double Tolerance = 0.01;

        /// <summary>
        /// Settles every hand-drawn wire against where its components are now
        /// (<paramref name="anchorsOf"/> gives the source and target top-left corners, or null for
        /// a wire that is gone). Returns the same instance when nothing changed.
        /// </summary>
        public static FileSettings Settle(FileSettings settings, Func<string, (Pt Source, Pt Target)?> anchorsOf)
        {
            FileSettings result = settings;
            foreach (var kv in settings.Points)
            {
                if (!(anchorsOf(kv.Key) is (Pt source, Pt target))) continue;
                var m = kv.Value;
                double sx = source.X - m.SourceAt.X, sy = source.Y - m.SourceAt.Y;
                double tx = target.X - m.TargetAt.X, ty = target.Y - m.TargetAt.Y;
                bool sourceMoved = Math.Abs(sx) > Tolerance || Math.Abs(sy) > Tolerance;
                bool targetMoved = Math.Abs(tx) > Tolerance || Math.Abs(ty) > Tolerance;
                if (!sourceMoved && !targetMoved) continue;
                bool together = Math.Abs(sx - tx) <= Tolerance && Math.Abs(sy - ty) <= Tolerance;
                var settled = together ? m.Shifted(sx, sy).WithAnchors(source, target) : m.WithAnchors(source, target);
                result = result.WithPoints(kv.Key, settled);
            }
            return result;
        }
    }
}
