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
            if (doc == null) return "no document on the canvas; edit-mode=" + (WireEditMode.Enabled ? "on" : "off");
            var state = DocStore.Get(doc);
            var effective = state.Effective;
            var read = SceneReader.Read(doc, effective);
            return string.Format(CultureInfo.InvariantCulture,
                "nodes={0} wires={1} routed={2} curve={3} hidden={4} settings={5} effective={6} overrides={7} writable={8} " +
                "canvases={9} routes={10} last-route={11:0.00}ms widget={12}",
                read.Scene.Nodes.Count, read.Wires.Count, read.RoutedCount,
                read.Wires.Count(w => w.Style == WireStyle.Curve && w.Display != GH_ParamWireDisplay.hidden),
                read.Wires.Count(w => w.Display == GH_ParamWireDisplay.hidden),
                state.Status, StyleNames.Summary(effective), effective.Overrides.Count, state.CanWrite,
                CanvasHook.Count,
                state.Routes == null ? "none" : state.Routes.Paths.Count + " paths/" + state.Routes.Junctions.Count + " dots"
                    + " drawn=" + (Instances.ActiveCanvas == null ? 0 : WireEditor.For(Instances.ActiveCanvas).LastRoutes?.Paths.Count ?? 0),
                CanvasHook.LastRouteMilliseconds,
                CanvasHook.HasWidget(Instances.ActiveCanvas) ? (UserSettings.WidgetVisible ? "shown" : "hidden") : "missing")
                + " edit-mode=" + (WireEditMode.Enabled ? "on" : "off")
                + " selected=" + (Instances.ActiveCanvas == null ? 0 : WireEditor.For(Instances.ActiveCanvas).Selected.Count)
                + " lanes=" + effective.Lanes.Count + " handdrawn=" + effective.Points.Count;
        }

        /// <summary>
        /// Every drawn route as "id|kind|x,y;x,y…", one per line; kind is lane, manual or other.
        /// For tools/verify, which drives the editing widget with constructed mouse events.
        /// </summary>
        public static string Paths()
        {
            var canvas = Instances.ActiveCanvas;
            if (canvas == null) return "";
            var editor = WireEditor.For(canvas);
            if (editor.LastRoutes == null || editor.LastRead == null) return "";
            var manual = new System.Collections.Generic.HashSet<string>(editor.LastRead.Wires.Where(w => w.Manual).Select(w => w.Id));
            return string.Join("\n", editor.LastRoutes.Paths.Select(kv =>
                kv.Key + "|" + (manual.Contains(kv.Key) ? "manual" : editor.LastRoutes.LaneFractions.ContainsKey(kv.Key) ? "lane" : "other") + "|" +
                string.Join(";", kv.Value.Select(p => p.X.ToString(CultureInfo.InvariantCulture) + "," + p.Y.ToString(CultureInfo.InvariantCulture)))));
        }

        /// <summary>
        /// Test hooks for tools/verify: "fail-next-paint", "retry", and the user's commands
        /// "style:bezier|ortho|diag", "spacing:N", "follow", "save", "default", "edit-mode:on|off", "tidy".
        /// The user's commands act on the active canvas's document and return Describe().
        /// </summary>
        public static string Run(string command)
        {
            string[] parts = command.Split(':');
            switch (parts[0])
            {
                case "edit-mode" when parts.Length == 2 && (parts[1] == "on" || parts[1] == "off"):
                    WireEditMode.Enabled = parts[1] == "on";
                    return Describe();
                case "drag":
                    CanvasHook.PretendDrag = parts.Length == 2 && parts[1] == "on";
                    CanvasHook.RefreshAll();
                    return "pretend drag " + (CanvasHook.PretendDrag ? "on" : "off");
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
                case "tidy":
                    return Tidier.Run(Instances.ActiveCanvas);
                default:
                    return "unknown command: " + command;
            }
        }
    }
}
