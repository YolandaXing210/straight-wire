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
                "nodes={0} wires={1} routed={2} curve={3} hidden={4} settings={5} effective={6} overrides={7} writable={8} " +
                "canvases={9} routes={10} last-route={11:0.00}ms",
                read.Scene.Nodes.Count, read.Wires.Count, read.RoutedCount,
                read.Wires.Count(w => w.Style == WireStyle.Curve && w.Display != GH_ParamWireDisplay.hidden),
                read.Wires.Count(w => w.Display == GH_ParamWireDisplay.hidden),
                state.Status, StyleNames.Summary(effective), effective.Overrides.Count, state.CanWrite,
                CanvasHook.Count,
                state.Routes == null ? "none" : state.Routes.Paths.Count + " paths/" + state.Routes.Junctions.Count + " dots",
                CanvasHook.LastRouteMilliseconds);
        }

        /// <summary>
        /// Test hooks for tools/verify: "fail-next-paint", "retry", and the user's commands
        /// "style:bezier|ortho|diag", "spacing:N", "follow", "save", "default".
        /// The user's commands act on the active canvas's document and return Describe().
        /// </summary>
        public static string Run(string command)
        {
            string[] parts = command.Split(':');
            switch (parts[0])
            {
                case "fail-next-paint":
                    CanvasHook.ThrowOnNextRoute = true;
                    return "the next takeover will throw";
                case "retry":
                    CanvasHook.ResetFailures();
                    CanvasHook.RefreshAll();
                    return "canvases will try again";
                case "style" when parts.Length == 2 && StyleNames.TryParseToken(parts[1], out var style):
                    Commands.SetStyle(style);
                    return Describe();
                case "spacing" when parts.Length == 2:
                    Commands.SetSpacing(double.Parse(parts[1], CultureInfo.InvariantCulture));
                    return Describe();
                case "follow":
                    Commands.FollowGlobal();
                    return Describe();
                case "save":
                    Commands.SaveToFile();
                    return Describe();
                case "default":
                    Commands.MakeMyDefault();
                    return Describe();
                default:
                    return "unknown command: " + command;
            }
        }
    }
}
