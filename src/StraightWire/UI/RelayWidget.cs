using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.GUI.Widgets;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

namespace StraightWire.UI
{
    /// <summary>Only double-clicks: bridge visible paths to native Relay objects.</summary>
    internal sealed class RelayWidget : GH_Widget
    {
        const double HitPixels = 6;
        VisibleWires _snapshot;
        ContextMenuStrip _choices;

        // This routing adapter is not a user-toggleable overlay. Wire Edit Mode owns activation.
        public override bool Visible { get => !WireEditMode.Enabled; set { } }
        public override string Name => "Straight Wire Relay Insertion";
        public override string Description => "Insert a Relay on the visible wire with a double-click.";
        public override Bitmap Icon_24x24 => null;
        public override void Render(GH_Canvas canvas) { }

        public void Publish(VisibleWires snapshot) => _snapshot = snapshot;
        public void Clear() { _snapshot = null; CloseChoices(); }
        public void CloseChoices()
        {
            var menu = _choices;
            _choices = null;
            menu?.Dispose();
        }

        static bool Available(GH_Canvas canvas, PointF point)
        {
            if (canvas?.Document == null || canvas.ActiveInteraction != null || WireEditMode.Enabled) return false;
            var obj = canvas.Document.FindAttribute(point, false)?.DocObject;
            if (obj != null && !(obj is GH_Group)) return false;
            // Native component and grip gestures keep priority, including multi-wire grip relays.
            return canvas.Document.FindAttributeByGrip(point, true, true, true, 8) == null;
        }

        public override bool Contains(Point control, PointF point)
        {
            if (_snapshot == null || Owner?.Document != _snapshot.Document || !Available(Owner, point)) return false;
            double tolerance = HitPixels * Owner.Viewport.ZoomInverse;
            return _snapshot.Hits(point, tolerance).Count > 0 || _snapshot.OnReplacedCurve(point, tolerance);
        }

        // Contains is queried by GH for several event types. Claim none of these gestures.
        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas canvas, GH_CanvasMouseEvent e) => GH_ObjectResponse.Ignore;
        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas canvas, GH_CanvasMouseEvent e) => GH_ObjectResponse.Ignore;
        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas canvas, GH_CanvasMouseEvent e) => GH_ObjectResponse.Ignore;

        public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas canvas, GH_CanvasMouseEvent e)
        {
            if (e.Button != MouseButtons.Left || _snapshot == null || !Available(canvas, e.CanvasLocation)) return GH_ObjectResponse.Ignore;
            // Once this adapter receives a double-click, never fall through to a hidden native
            // curve after a stale snapshot, failed edit, or dismissed ambiguous selection.
            Log.Guard("relay-double-click", () =>
            {
                CloseChoices();
                var snapshot = _snapshot;
                if (!snapshot.IsCurrent(canvas)) { canvas.Invalidate(); return; }
                double tolerance = HitPixels * canvas.Viewport.ZoomInverse;
                var hits = snapshot.Hits(e.CanvasLocation, tolerance);
                if (hits.Count == 0)
                {
                    if (snapshot.OnReplacedCurve(e.CanvasLocation, tolerance))
                        canvas.ShowComponentSearchBox(e.ControlLocation);
                    return;
                }
                if (hits.Count == 1)
                {
                    if (!RelayInsertion.Insert(canvas, snapshot, hits[0], e.CanvasLocation))
                        Log.Info("Relay insertion skipped: the document or wire changed, or its settings cannot be edited.");
                    return;
                }
                var menu = new ContextMenuStrip();
                _choices = menu;
                menu.Items.Add(new ToolStripMenuItem("Choose the connection for this Relay") { Enabled = false });
                foreach (var hit in hits.OrderBy(h => h.Entry.Wire.Id, StringComparer.Ordinal))
                {
                    var choice = hit;
                    var wire = hit.Entry.Wire;
                    string Label(IGH_Param p) => p.Attributes.GetTopLevel.DocObject.NickName + " / " + p.NickName;
                    var item = new ToolStripMenuItem(Label(wire.Source) + " → " + Label(wire.Target) +
                        " [" + wire.Source.InstanceGuid.ToString("N").Substring(0, 6) + " → " +
                        wire.Target.InstanceGuid.ToString("N").Substring(0, 6) + "]") { Tag = wire.Id, ToolTipText = wire.Id };
                    item.Click += (s, args) => Log.Guard("relay-choice", () =>
                    {
                        if (Available(canvas, e.CanvasLocation)) RelayInsertion.Insert(canvas, snapshot, choice, e.CanvasLocation);
                        CloseChoices();
                    });
                    menu.Items.Add(item);
                }
                menu.Closed += (s, args) =>
                {
                    if (ReferenceEquals(_choices, menu)) _choices = null;
                    menu.Dispose();
                };
                menu.Show(canvas, e.ControlLocation);
            });
            return GH_ObjectResponse.Handled;
        }
    }
}
