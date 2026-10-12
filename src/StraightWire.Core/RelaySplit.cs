using System;
using System.Collections.Generic;
using System.Linq;

namespace StraightWire.Core
{
    /// <summary>Re-key one connection's saved edits when a relay splits it in two.</summary>
    public static class RelaySplit
    {
        public static FileSettings Apply(FileSettings settings, string oldId, string firstId, string secondId,
            IReadOnlyList<Pt> path, int segment, Pt sourceAt, Pt relayAt, Pt targetAt)
        {
            if (path == null || path.Count < 2 || segment < 0 || segment >= path.Count - 1)
                throw new ArgumentOutOfRangeException(nameof(segment));
            var styles = settings.Overrides.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var lanes = settings.Lanes.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var points = settings.Points.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            if (styles.TryGetValue(oldId, out var style))
            {
                styles.Remove(oldId);
                styles[firstId] = styles[secondId] = style;
            }
            bool edited = points.Remove(oldId) | lanes.Remove(oldId);
            if (edited)
            {
                // Keep every bend on its original side. Relay grips replace the split endpoints.
                var before = path.Skip(1).Take(segment).ToArray();
                var after = path.Skip(segment + 1).Take(path.Count - segment - 2).ToArray();
                if (before.Length > 0) points[firstId] = new ManualPoints(before, sourceAt, relayAt);
                if (after.Length > 0) points[secondId] = new ManualPoints(after, relayAt, targetAt);
            }
            return new FileSettings(settings.Mode, settings.Spacing, styles, lanes, points);
        }
    }
}
