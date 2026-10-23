using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using StraightWire.Core;

namespace StraightWire
{
    /// <summary>One wire as the canvas sees it, with what drawing needs besides its route.</summary>
    internal sealed class WireDraw
    {
        public string Id;
        public IGH_Param Source, Target;
        public PointF Start, End;
        public bool SourceSelected, TargetSelected;
        public GH_ParamWireDisplay Display;
        public WireStyle Style;

        /// <summary>Routed by Core. False for Curve-style wires and for hidden-display wires.</summary>
        public bool Routed;

        /// <summary>Hand-drawn: the file has points for this wire.</summary>
        public bool Manual;
    }

    internal sealed class SceneRead
    {
        public Scene Scene;
        public List<WireDraw> Wires;
        public int RoutedCount;
    }

    /// <summary>
    /// Turns a Grasshopper document into Core's Scene. Nodes are the top-level components and
    /// parameters; wires are the sources of every input. A wire's id is "sourceGuid>targetGuid".
    /// </summary>
    internal static class SceneReader
    {
        public static SceneRead Read(GH_Document doc, FileSettings settings, WireEditor editor = null)
        {
            var nodes = new List<SceneNode>();
            var wires = new List<SceneWire>();
            var draws = new List<WireDraw>();
            int routed = 0;
            foreach (var obj in doc.Objects)
            {
                if (!(obj is IGH_ActiveObject) || obj.Attributes == null) continue;
                nodes.Add(new SceneNode(obj.InstanceGuid.ToString(), ToRect(obj.Attributes.Bounds)));
                foreach (var target in InputsOf(obj))
                    foreach (var source in target.Sources)
                    {
                        var draw = new WireDraw
                        {
                            Id = WireId(source, target),
                            Source = source,
                            Target = target,
                            Start = source.Attributes.OutputGrip,
                            End = target.Attributes.InputGrip,
                            SourceSelected = source.Attributes.GetTopLevel.Selected,
                            TargetSelected = target.Attributes.GetTopLevel.Selected,
                            Display = target.WireDisplay,
                        };
                        draw.Style = settings.StyleOf(draw.Id);
                        draw.Routed = draw.Style != WireStyle.Curve && draw.Display != GH_ParamWireDisplay.hidden;
                        draws.Add(draw);
                        if (draw.Display == GH_ParamWireDisplay.hidden) continue;
                        if (draw.Routed) routed++;
                        settings.Points.TryGetValue(draw.Id, out var manual);
                        draw.Manual = manual != null;
                        double? lane = manual == null && settings.Lanes.TryGetValue(draw.Id, out double f) ? f : (double?)null;
                        double? pin = editor?.Pin is (string pinId, double pinX) && pinId == draw.Id ? pinX : (double?)null;
                        BackRuns back = null;
                        if (manual == null && (editor?.BackPins == null || !editor.BackPins.TryGetValue(draw.Id, out back)))
                            settings.Backs.TryGetValue(draw.Id, out back);
                        wires.Add(new SceneWire(draw.Id, source.InstanceGuid.ToString(),
                            source.Attributes.GetTopLevel.DocObject.InstanceGuid.ToString(), obj.InstanceGuid.ToString(),
                            ToPt(draw.Start), ToPt(draw.End), draw.Style, lane, pin, manual?.Points, back));
                    }
            }
            return new SceneRead { Scene = new Scene(nodes, wires, settings.Spacing), Wires = draws, RoutedCount = routed };
        }

        public static IEnumerable<(IGH_Param Source, IGH_Param Target)> Connections(GH_Document doc)
        {
            foreach (var obj in doc.Objects)
                foreach (var target in InputsOf(obj))
                    foreach (var source in target.Sources)
                        yield return (source, target);
        }

        public static HashSet<string> LiveWireIds(GH_Document doc)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (source, target) in Connections(doc)) ids.Add(WireId(source, target));
            return ids;
        }

        /// <summary>Wires with at least one end on a selected object, in document order: what "selected" style changes act on.</summary>
        public static List<string> WireIdsTouchingSelection(GH_Document doc)
        {
            var ids = new List<string>();
            foreach (var (source, target) in Connections(doc))
                if (source.Attributes.GetTopLevel.Selected || target.Attributes.GetTopLevel.Selected)
                    ids.Add(WireId(source, target));
            return ids;
        }

        public static string WireId(IGH_Param source, IGH_Param target) => source.InstanceGuid + ">" + target.InstanceGuid;

        /// <summary>The source of every wire that runs backwards: back wires from one source share their out run.</summary>
        public static Dictionary<string, string> BackGroups(GH_Document doc)
        {
            var groups = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (source, target) in Connections(doc))
                if (target.Attributes.InputGrip.X - source.Attributes.OutputGrip.X < Routing.BackwardBelow)
                    groups[WireId(source, target)] = source.InstanceGuid.ToString();
            return groups;
        }

        /// <summary>For every wire, the top-left corners of its source and target components: what hand-drawn points are anchored to.</summary>
        public static Dictionary<string, (Pt Source, Pt Target)> Anchors(GH_Document doc)
        {
            var anchors = new Dictionary<string, (Pt Source, Pt Target)>(StringComparer.Ordinal);
            foreach (var (source, target) in Connections(doc))
            {
                var s = source.Attributes.GetTopLevel.Bounds;
                var t = target.Attributes.GetTopLevel.Bounds;
                anchors[WireId(source, target)] = (new Pt(s.Left, s.Top), new Pt(t.Left, t.Top));
            }
            return anchors;
        }

        static IEnumerable<IGH_Param> InputsOf(IGH_DocumentObject obj)
        {
            if (obj is IGH_Component component) return component.Params.Input;
            if (obj is IGH_Param param) return new[] { param };
            return Array.Empty<IGH_Param>();
        }

        static Rect ToRect(RectangleF r) => new Rect(r.Left, r.Top, r.Right, r.Bottom);

        static Pt ToPt(PointF p) => new Pt(p.X, p.Y);
    }
}
