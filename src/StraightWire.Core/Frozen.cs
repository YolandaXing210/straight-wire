using System;
using System.Collections.Generic;
using System.Linq;

namespace StraightWire.Core
{
    /// <summary>
    /// Routes held still while a component is being dragged. Routing waits for the drop; until
    /// then every wire keeps its last route, except wires attached to whatever is moving, which
    /// the plugin draws as Grasshopper curves.
    /// </summary>
    public static class Frozen
    {
        /// <summary>
        /// The cached result minus every path whose ends no longer sit on its wire's ports (the
        /// wire is attached to something moving, or gone), and minus the junction dots of those
        /// wires' sources.
        /// </summary>
        public static RouteResult Keep(RouteResult cached, Scene scene)
        {
            var wires = new Dictionary<string, SceneWire>(StringComparer.Ordinal);
            foreach (var w in scene.Wires) wires[w.Id] = w;
            var kept = new Dictionary<string, Pt[]>(StringComparer.Ordinal);
            var unsettled = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in cached.Paths)
            {
                if (!wires.TryGetValue(kv.Key, out var w)) continue;
                var path = kv.Value;
                if (path[0].Equals(w.Start) && path[path.Length - 1].Equals(w.End)) kept[kv.Key] = path;
                else unsettled.Add(w.Group);
            }
            var dots = cached.Junctions.Where(j => !unsettled.Contains(j.Group)).ToList();
            return new RouteResult(kept, dots, cached.LaneFractions, cached.Backs);
        }
    }
}
