using System.Globalization;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using StraightWire.Core;
using StraightWire.Interop;

namespace StraightWire
{
    /// <summary>
    /// Entry points for tools/dev-reload.csx, which loads a development build into a running Rhino
    /// without a restart. Grasshopper never sees that build as a plugin, so the marker component
    /// is not registered; everything else behaves as installed. The checks in tools/verify also
    /// call these, on dev builds and on the installed plugin alike.
    /// </summary>
    public static class DevHost
    {
        public static string Attach()
        {
            Plugin.Start();
            return "Straight Wire dev build attached. Wire pens available: " + NativeWires.Available + ".";
        }

        public static string Detach()
        {
            Plugin.Stop();
            return "Straight Wire dev build detached.";
        }

        /// <summary>A one-line summary of the document on the active canvas.</summary>
        public static string Describe()
        {
            var doc = Instances.ActiveCanvas?.Document;
            if (doc == null) return "no document on the canvas";
            var state = DocStore.Get(doc);
            var effective = state.Effective;
            var read = SceneReader.Read(doc, effective);
            return string.Format(CultureInfo.InvariantCulture,
                "nodes={0} wires={1} routed={2} curve={3} hidden={4} settings={5} effective={6} overrides={7} writable={8}",
                read.Scene.Nodes.Count, read.Wires.Count, read.RoutedCount,
                read.Wires.Count(w => w.Style == WireStyle.Curve && w.Display != GH_ParamWireDisplay.hidden),
                read.Wires.Count(w => w.Display == GH_ParamWireDisplay.hidden),
                state.Status, StyleNames.Summary(effective), effective.Overrides.Count, state.CanWrite);
        }
    }
}
