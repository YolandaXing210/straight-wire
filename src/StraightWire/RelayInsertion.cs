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
    internal static class RelayInsertion
    {
        public static bool Insert(GH_Canvas canvas, VisibleWires snapshot, VisibleWires.Hit hit, PointF location)
        {
            if (WireEditMode.Enabled || !snapshot.IsCurrent(canvas)) return false;
            var doc = snapshot.Document;
            var state = DocStore.Get(doc);
            if (!state.CanWrite || state.Status == SettingsStatus.Broken) return false;
            var wire = hit.Entry.Wire;
            var source = wire.Source;
            var target = wire.Target;
            var sources = target.Sources.ToArray();
            if (!sources.Contains(source)) return false;
            var before = doc.ValueTable.GetValue(SettingsJson.Key, null);
            bool modified = doc.IsModified;
            var relay = new GH_Relay();
            relay.CreateAttributes();
            relay.Attributes.Pivot = location;
            relay.Attributes.PerformLayout();
            relay.WireDisplay = target.WireDisplay;
            var actions = new List<IGH_UndoAction>();
            bool added = false;
            try
            {
                added = doc.AddObject(relay, false);
                if (!added) return false;
                actions.Add(new GH_AddObjectAction(relay));
                actions.Add(new GH_WireAction(relay));
                actions.Add(new GH_WireAction(target));
                relay.AddSource(source);
                relay.ResolveDisplayName();
                target.ReplaceSource(source, relay);
                var expected = sources.Select(p => ReferenceEquals(p, source) ? relay : p).ToArray();
                if (!target.Sources.SequenceEqual(expected)) throw new InvalidOperationException("Relay replacement changed source order.");

                string after = before;
                var settings = state.Effective;
                if (settings.Overrides.ContainsKey(wire.Id) || settings.Points.ContainsKey(wire.Id) || settings.Lanes.ContainsKey(wire.Id))
                {
                    var path = hit.Entry.Path;
                    int segment = hit.Segment;
                    // Curve style may hide saved hand edits. Preserve them too, in their stored order.
                    if (!hit.Entry.ReplacedCurve && settings.Points.TryGetValue(wire.Id, out var manual))
                    {
                        path = new[] { new Pt(wire.Start.X, wire.Start.Y) }.Concat(manual.Points)
                            .Concat(new[] { new Pt(wire.End.X, wire.End.Y) }).ToArray();
                        segment = WireEdit.HitWire(new[] { new KeyValuePair<string, Pt[]>(wire.Id, path) },
                            new Pt(location.X, location.Y), double.MaxValue).Value.Segment;
                    }
                    Pt At(IGH_Param p) { var b = p.Attributes.GetTopLevel.Bounds; return new Pt(b.X, b.Y); }
                    var next = RelaySplit.Apply(settings, wire.Id, SceneReader.WireId(source, relay), SceneReader.WireId(relay, target),
                        path, segment, At(source), At(relay), At(target));
                    after = SettingsJson.Serialize(next);
                    doc.ValueTable.SetValue(SettingsJson.Key, after);
                }
                actions.Add(new RelaySettingsUndo(before, after));
                DocStore.Reload(doc);
                if (doc.UndoUtil.RecordEvent("Insert Wire Relay", actions) == Guid.Empty)
                    throw new InvalidOperationException("Could not record Relay undo.");
            }
            catch
            {
                if (added)
                {
                    target.RemoveAllSources();
                    foreach (var p in sources) target.AddSource(p);
                    relay.RemoveAllSources();
                    doc.RemoveObject(relay, false);
                }
                ApplySettings(doc, before);
                doc.IsModified = modified;
                throw;
            }
            // The transaction is committed. A downstream solution failure must not roll it back.
            doc.Modified();
            relay.ExpireSolution(false);
            target.ExpireSolution(false);
            canvas.Invalidate();
            doc.NewSolution(false);
            return true;
        }

        static void ApplySettings(GH_Document doc, string json)
        {
            if (json == null) doc.ValueTable.DeleteValue(SettingsJson.Key);
            else doc.ValueTable.SetValue(SettingsJson.Key, json);
            DocStore.Reload(doc);
        }

        // No synchronous redraw midway through the compound graph undo/redo.
        sealed class RelaySettingsUndo : GH_UndoAction
        {
            readonly string _before, _after;
            public RelaySettingsUndo(string before, string after) { _before = before; _after = after; }
            public override bool ExpiresSolution => false;
            public override bool ExpiresDisplay => true;
            protected override void Internal_Undo(GH_Document doc) => ApplySettings(doc, _before);
            protected override void Internal_Redo(GH_Document doc) => ApplySettings(doc, _after);
        }
    }
}
