using System;
using System.Collections.Generic;
using System.Linq;

namespace StraightWire.Core
{
    /// <summary>
    /// Routes every wire of a scene: backwards wires through gaps, lane wires into spaced lanes,
    /// 45° wires into single diagonals where that touches nothing new, then junction dots.
    /// </summary>
    public static class Router
    {
        /// <summary>
        /// Routes <paramref name="scene"/>. <paramref name="state"/> carries lanes from the previous
        /// call (pass the same instance while the user drags) and is updated; null starts fresh.
        /// The same scene and state always give the same result.
        /// </summary>
        public static RouteResult Route(Scene scene, RouterState state)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            state = state ?? new RouterState();
            var ctx = new RouteContext(scene, state);
            BackWires.Prepare(ctx);
            var lanes = Lanes.Prepare(ctx);
            Lanes.Relax(lanes, ctx.Sp);
            Diagonals.Decide(ctx);

            var paths = new Dictionary<string, Pt[]>(StringComparer.Ordinal);
            var forJunctions = new List<(string Group, bool Routed, Pt[] Path)>(ctx.Jobs.Count);
            foreach (var w in ctx.Jobs)
            {
                Pt[] path = w.Routed ? WirePath.For(w).ToArray() : null;
                if (path != null) paths[w.Id] = path;
                forJunctions.Add((w.Group, w.Routed, path));
            }
            state.Replace(lanes.Select(w => new KeyValuePair<string, double>(w.Id, w.Lane)));
            return new RouteResult(paths, Junctions.Find(forJunctions));
        }
    }
}
