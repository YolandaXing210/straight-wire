using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Undo;
using Grasshopper.Kernel.Undo.Actions;
using StraightWire.Core;

namespace StraightWire
{
    /// <summary>
    /// Tidy All / Tidy Selected. Reads the canvas into blocks and links (TidyScope), lays them out
    /// (Tidy), and moves everything in one undo step, clearing the lanes and points of wires whose
    /// ends now sit in different places.
    /// </summary>
    internal static class Tidier
    {
        sealed class CanvasRead
        {
            public TidyPlan Plan;
            public List<TidyWire> Wires;
        }

        /// <summary>"Tidy Selected" when the selection makes two or more blocks, otherwise "Tidy All".</summary>
        public static string Label(GH_Canvas canvas)
        {
            if (canvas?.Document == null) return "Tidy All";
            return Read(canvas).Plan.Selection ? "Tidy Selected" : "Tidy All";
        }

        /// <summary>Tidies the canvas's document and says what happened, for tools/verify.</summary>
        public static string Run(GH_Canvas canvas)
        {
            string summary = "failed";
            Log.Guard("tidy", () => summary = TidyCanvas(canvas));
            return summary;
        }

        static string TidyCanvas(GH_Canvas canvas)
        {
            var doc = canvas?.Document;
            if (doc == null) return "no document";
            var editor = WireEditor.For(canvas);
            if (canvas.ActiveInteraction != null || CanvasHook.PretendDrag || editor.Live != null || editor.Pin != null) return "busy";

            var read = Read(canvas);
            if (read.Plan.Links.Count == 0)
            {
                Log.Info("nothing to tidy.");
                return "nothing to tidy";
            }
            var state = DocStore.Get(doc);
            var effective = state.Effective;
            var moved = Core.Tidy.Layout(read.Plan.Blocks, read.Plan.Links, effective.Spacing);
            if (moved.Count == 0)
            {
                Log.Info("already tidy.");
                return "already tidy";
            }

            // Every position is worked out before anything moves.
            var blocks = read.Plan.Blocks.ToDictionary(b => b.Id, StringComparer.Ordinal);
            var moves = new List<(IGH_DocumentObject Obj, SizeF Delta, PointF Pivot)>();
            foreach (var kv in moved.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                var bounds = blocks[kv.Key].Bounds;
                var delta = new SizeF((float)(kv.Value.X - bounds.X0), (float)(kv.Value.Y - bounds.Y0));
                foreach (string id in read.Plan.Members[kv.Key])
                    if (Guid.TryParse(id, out var guid) && doc.FindObject(guid, true) is IGH_DocumentObject obj && obj.Attributes != null)
                        moves.Add((obj, delta, obj.Attributes.Pivot));
            }
            var reset = TidyScope.WiresToReset(read.Plan, moved.Keys, read.Wires)
                                 .Where(id => effective.Lanes.ContainsKey(id) || effective.Points.ContainsKey(id) || effective.Backs.ContainsKey(id)).ToList();

            // Pivot actions record where everything is, before anything changes. The settings are
            // written next, before anything moves, so the next paint's settling already sees the
            // cleared edits. The record holds them in that order too: Undo runs last to first, so
            // objects go back before the settings do, and the paint the settings trigger finds
            // nothing to settle. Redo replays the tidy's own order.
            var pivots = moves.Select(m => new GH_PivotAction(m.Obj)).ToList();
            var settingsUndo = reset.Count > 0 ? DocStore.WriteUnrecorded(doc, effective.WithoutEdits(reset)) : null;
            var record = new GH_UndoRecord("Tidy");
            if (settingsUndo != null) record.AddAction(settingsUndo);
            foreach (var pivot in pivots) record.AddAction(pivot);
            try
            {
                foreach (var (obj, delta, pivot) in moves)
                {
                    obj.Attributes.Pivot = pivot + delta;
                    obj.Attributes.ExpireLayout();
                }
                foreach (var m in moves) m.Obj.Attributes.PerformLayout();
            }
            catch
            {
                foreach (var (obj, _, pivot) in moves)
                {
                    obj.Attributes.Pivot = pivot;
                    obj.Attributes.ExpireLayout();
                }
                settingsUndo?.Revert(doc);
                throw;
            }
            doc.UndoServer.PushUndoRecord(record);
            doc.Modified();
            CanvasHook.ResetFailures();
            CanvasHook.RefreshAll();
            return "moved " + moved.Count + " blocks (" + moves.Count + " objects), reset " + reset.Count + " wires";
        }

        static CanvasRead Read(GH_Canvas canvas)
        {
            var doc = canvas.Document;
            var objects = new List<TidyObject>();
            var groups = new List<TidyGroup>();
            foreach (var obj in doc.Objects)
            {
                if (obj?.Attributes == null) continue;
                string id = obj.InstanceGuid.ToString();
                var r = obj.Attributes.Bounds;
                var bounds = new Rect(r.Left, r.Top, r.Right, r.Bottom);
                if (obj is GH_Group group) groups.Add(new TidyGroup(id, bounds, Members(doc, group), group.Attributes.Selected));
                else
                    objects.Add(new TidyObject(id, bounds, obj.ComponentGuid == StraightWireMarker.Id ? TidyKind.Marker
                                                           : obj is IGH_ActiveObject ? TidyKind.Active : TidyKind.Annotation));
            }
            var wires = SceneReader.Connections(doc).Select(c => new TidyWire(SceneReader.WireId(c.Source, c.Target),
                c.Source.Attributes.GetTopLevel.DocObject.InstanceGuid.ToString(), c.Source.Attributes.OutputGrip.Y,
                c.Target.Attributes.GetTopLevel.DocObject.InstanceGuid.ToString(), c.Target.Attributes.InputGrip.Y)).ToList();
            var selected = doc.SelectedObjects().Where(o => !(o is GH_Group)).Select(o => o.InstanceGuid.ToString()).ToList();
            var plan = TidyScope.Build(objects, groups, wires, selected, WireEditor.For(canvas).Selected.ToList());
            return new CanvasRead { Plan = plan, Wires = wires };
        }

        /// <summary>Every object and group inside a group, however deeply nested.</summary>
        static List<string> Members(GH_Document doc, GH_Group group)
        {
            var ids = new List<string>();
            var seen = new HashSet<Guid> { group.InstanceGuid };
            var pending = new Stack<GH_Group>();
            pending.Push(group);
            while (pending.Count > 0)
            {
                foreach (var id in pending.Pop().ObjectIDs)
                {
                    if (!seen.Add(id)) continue;
                    ids.Add(id.ToString());
                    if (doc.FindObject(id, true) is GH_Group inner) pending.Push(inner);
                }
            }
            return ids;
        }
    }
}
