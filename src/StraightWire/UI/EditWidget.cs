using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.GUI.Widgets;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using StraightWire.Core;

namespace StraightWire.UI
{
    /// <summary>
    /// Wire editing with the mouse: select, slide a lane (or a backwards wire's verticals and
    /// crossing), drag and add or remove bend points.
    ///
    /// Grasshopper offers every mouse press to its widgets before its components and before its
    /// own default behaviour (box selection, relay insertion), so a widget can take a press that
    /// lands on one of our wires and leave every other press exactly as it was. It claims a press
    /// only on a routed wire or one of its handles, never over a component or within reach of a
    /// grip, where Grasshopper would start a new wire. Contains answers only
    /// while the left button is down, so right-click menus and tooltips stay Grasshopper's; hover
    /// is tracked from the canvas's mouse moves instead (see Hover).
    /// </summary>
    internal sealed class EditWidget : GH_Widget
    {
        const float HitPx = 6, HandlePx = 7, DragPx = 3, SnapPx = 8;
        const double Grid = 10;

        enum Kind { None, Lane, Segment, Point, Run }

        readonly struct Target
        {
            public Target(Kind kind, string id, int index, Pt[] path, bool manual, BackRun run = BackRun.None)
            {
                Kind = kind;
                Id = id;
                Index = index;
                Path = path;
                Manual = manual;
                Run = run;
            }

            public Kind Kind { get; }
            public string Id { get; }

            /// <summary>The segment hit, or for Kind.Point the interior point.</summary>
            public int Index { get; }

            public Pt[] Path { get; }
            public bool Manual { get; }

            /// <summary>For Kind.Run, which run of a backwards wire.</summary>
            public BackRun Run { get; }

            public bool Hit => Id != null;
        }

        // The press in progress.
        Target _press;
        Point _downControl;
        PointF _downCanvas;
        bool _dragging;
        List<Pt> _points0;

        // A run drag: the stored runs of every wire it moves, and the hit wire's runs as routed.
        Dictionary<string, BackRuns> _runs0;
        BackRuns _routed0;

        public override bool Visible
        {
            get => UserSettings.EditingEnabled;
            set => UserSettings.EditingEnabled = value;
        }

        public override string Name => "Straight Wire Editing";

        public override string Description => "Select routed wires, slide their lanes, and drag, add or remove bend points.";

        public override Bitmap Icon_24x24 => null;

        /// <summary>Drawing happens with the wires (WireRenderer), not on the widget layer.</summary>
        public override void Render(GH_Canvas canvas) { }

        public override bool Contains(Point pt_control, PointF pt_canvas) =>
            Owner != null && Control.MouseButtons == MouseButtons.Left && Find(Owner, pt_canvas).Hit;

        /// <summary>Updates the hovered wire and the cursor; called from the canvas's MouseMove when no button is down.</summary>
        public void Hover(GH_Canvas canvas, PointF canvasPoint)
        {
            if (!Visible || canvas.ActiveInteraction != null) return;
            var editor = WireEditor.For(canvas);
            var target = Find(canvas, canvasPoint);
            string hover = target.Hit ? target.Id : null;
            if (hover != editor.Hover)
            {
                editor.Hover = hover;
                canvas.Invalidate();
            }
            if (target.Hit)
                canvas.Cursor = target.Kind == Kind.Lane || (target.Kind == Kind.Run && target.Run != BackRun.Cross) ? Cursors.SizeWE
                              : target.Kind == Kind.Run ? Cursors.SizeNS
                              : target.Kind == Kind.Segment || target.Kind == Kind.Point ? Cursors.SizeAll
                              : Cursors.Hand;
        }

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            var response = GH_ObjectResponse.Ignore;
            Log.Guard("edit-press", () => response = Press(sender, e));
            return response;
        }

        GH_ObjectResponse Press(GH_Canvas canvas, GH_CanvasMouseEvent e)
        {
            if (e.Button != MouseButtons.Left || canvas.Document == null) return GH_ObjectResponse.Ignore;
            var editor = WireEditor.For(canvas);
            var target = Find(canvas, e.CanvasLocation);
            bool shift = (Control.ModifierKeys & Keys.Shift) != 0;
            if (!target.Hit)
            {
                // A press anywhere else clears the wire selection and is Grasshopper's to handle.
                if (editor.Selected.Count > 0 && !shift)
                {
                    editor.Selected.Clear();
                    canvas.Invalidate();
                }
                return GH_ObjectResponse.Ignore;
            }

            if (target.Kind == Kind.Point)
            {
                if (!editor.Selected.Contains(target.Id))
                {
                    if (!shift) editor.Selected.Clear();
                    editor.Selected.Add(target.Id);
                }
            }
            else if (shift)
            {
                if (!editor.Selected.Remove(target.Id)) editor.Selected.Add(target.Id);
            }
            else
            {
                editor.Selected.Clear();
                editor.Selected.Add(target.Id);
            }
            canvas.Document.DeselectAll();

            _press = target;
            _downControl = e.ControlLocation;
            _downCanvas = e.CanvasLocation;
            _dragging = false;
            _points0 = CurrentPoints(canvas.Document, target);
            if (target.Kind == Kind.Run) StartRun(canvas.Document, editor, target);
            canvas.Invalidate();
            return GH_ObjectResponse.Capture;
        }

        /// <summary>
        /// Notes what a run drag moves: for an out run every back wire from that output, since they
        /// share it; otherwise the wire hit.
        /// </summary>
        void StartRun(GH_Document doc, WireEditor editor, Target target)
        {
            var stored = DocStore.Get(doc).Effective.Backs;
            var ids = new List<string> { target.Id };
            if (target.Run == BackRun.Out)
            {
                var source = editor.LastRead.Wires.First(w => w.Id == target.Id).Source;
                ids = editor.LastRead.Wires.Where(w => w.Source == source && editor.LastRoutes.Backs.ContainsKey(w.Id)).Select(w => w.Id).ToList();
            }
            _runs0 = ids.ToDictionary(id => id, id => stored.TryGetValue(id, out var runs) ? runs : new BackRuns(), StringComparer.Ordinal);
            _routed0 = editor.LastRoutes.Backs[target.Id];
        }

        public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (!_press.Hit) return GH_ObjectResponse.Ignore;
            Log.Guard("edit-drag", () => Drag(sender, e));
            return GH_ObjectResponse.Handled;
        }

        void Drag(GH_Canvas canvas, GH_CanvasMouseEvent e)
        {
            var editor = WireEditor.For(canvas);
            if (!_dragging)
            {
                double moved = Math.Sqrt(Math.Pow(e.ControlLocation.X - _downControl.X, 2) + Math.Pow(e.ControlLocation.Y - _downControl.Y, 2));
                if (moved < DragPx || _press.Kind == Kind.None) return;
                _dragging = true;
            }
            bool free = (Control.ModifierKeys & Keys.Alt) != 0;
            var path0 = _press.Path;
            Pt start = path0[0], end = path0[path0.Length - 1];
            switch (_press.Kind)
            {
                case Kind.Lane:
                    editor.Pin = (_press.Id, e.CanvasLocation.X);
                    break;
                case Kind.Point:
                {
                    int i = _press.Index;
                    Pt a = i == 0 ? start : _points0[i - 1];
                    Pt b = i == _points0.Count - 1 ? end : _points0[i + 1];
                    var p = WireEdit.Snap(new Pt(e.CanvasLocation.X, e.CanvasLocation.Y), a, b, SnapPx * canvas.Viewport.ZoomInverse, Grid, free);
                    editor.Live = (_press.Id, PathOf(start, WireEdit.MovePoint(_points0, i, p), end));
                    break;
                }
                case Kind.Segment:
                {
                    double dx = e.CanvasLocation.X - _downCanvas.X, dy = e.CanvasLocation.Y - _downCanvas.Y;
                    editor.Live = (_press.Id, PathOf(start, WireEdit.SlideSegment(path0, _points0, _press.Index, dx, dy, Grid, free), end));
                    break;
                }
                case Kind.Run:
                {
                    double dx = e.CanvasLocation.X - _downCanvas.X, dy = e.CanvasLocation.Y - _downCanvas.Y;
                    editor.BackPins = _runs0.ToDictionary(kv => kv.Key, kv => WireEdit.DragBack(kv.Value, _routed0, _press.Run, dx, dy), StringComparer.Ordinal);
                    break;
                }
            }
            canvas.Refresh();
        }

        public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (!_press.Hit) return GH_ObjectResponse.Ignore;
            var editor = WireEditor.For(sender);
            try
            {
                if (_dragging) Log.Guard("edit-commit", () => Commit(sender, editor));
            }
            finally
            {
                editor.Pin = null;
                editor.Live = null;
                editor.BackPins = null;
                _runs0 = null;
                _press = default;
                _dragging = false;
                sender.Refresh();
            }
            return GH_ObjectResponse.Release;
        }

        void Commit(GH_Canvas canvas, WireEditor editor)
        {
            var doc = canvas.Document;
            if (doc == null) return;
            if (_press.Kind == Kind.Lane)
            {
                if (editor.LastRoutes != null && editor.LastRoutes.LaneFractions.TryGetValue(_press.Id, out double fraction))
                    Commands.SetLane(doc, _press.Id, fraction);
            }
            else if (_press.Kind == Kind.Run)
            {
                if (editor.LastRoutes != null && editor.LastRoutes.Backs.TryGetValue(_press.Id, out var routed) && routed[_press.Run] is double value)
                    Commands.SetBack(doc, _runs0.Keys.ToList(), _press.Run, value);
            }
            else if (editor.Live is (string id, Pt[] live) && id == _press.Id)
            {
                Commands.SetPoints(doc, id, WireEdit.PointsOf(live), _press.Kind == Kind.Point ? "Move Wire Point" : "Move Wire Segment");
            }
        }

        public override GH_ObjectResponse RespondToMouseDoubleClick(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            var response = GH_ObjectResponse.Ignore;
            Log.Guard("edit-double-click", () =>
            {
                if (e.Button != MouseButtons.Left || sender.Document == null) return;
                var target = Find(sender, e.CanvasLocation);
                if (!target.Hit) return;
                var points = CurrentPoints(sender.Document, target);
                if (target.Kind == Kind.Point)
                    Commands.SetPoints(sender.Document, target.Id, WireEdit.RemovePoint(points, target.Index), "Remove Wire Point");
                else
                {
                    // Insert into the wire's current points: for a hand-drawn wire its stored points,
                    // whose path is exactly the drawn one.
                    var path = PathOf(target.Path[0], points, target.Path[target.Path.Length - 1]);
                    var hit = WireEdit.HitWire(new[] { new KeyValuePair<string, Pt[]>(target.Id, path) },
                                               new Pt(e.CanvasLocation.X, e.CanvasLocation.Y), double.MaxValue);
                    int segment = hit?.Segment ?? target.Index;
                    Commands.SetPoints(sender.Document, target.Id, WireEdit.InsertPoint(path, segment, new Pt(e.CanvasLocation.X, e.CanvasLocation.Y)), "Add Wire Point");
                }
                _press = default;
                _dragging = false;
                response = GH_ObjectResponse.Handled;
            });
            return response;
        }

        /// <summary>
        /// What the mouse is over: a handle of a selected or hovered wire first, then any routed wire
        /// (for an automatic backwards wire, which of its runs). Nothing over a component, and no
        /// wire, only a handle, near a grip.
        /// </summary>
        static Target Find(GH_Canvas canvas, PointF canvasPoint)
        {
            var doc = canvas.Document;
            var editor = WireEditor.For(canvas);
            var routes = editor.LastRoutes;
            var read = editor.LastRead;
            if (doc == null || routes == null || read == null) return default;
            var over = doc.FindAttribute(canvasPoint, false)?.DocObject;
            if (over != null && !(over is GH_Group)) return default;

            var manual = new HashSet<string>(read.Wires.Where(w => w.Manual).Select(w => w.Id), StringComparer.Ordinal);
            var p = new Pt(canvasPoint.X, canvasPoint.Y);
            float zoomInverse = canvas.Viewport.ZoomInverse;

            var handleOwners = editor.Selected.ToList();
            if (editor.Hover != null && !handleOwners.Contains(editor.Hover)) handleOwners.Add(editor.Hover);
            foreach (string id in handleOwners)
            {
                if (!routes.Paths.TryGetValue(id, out var path)) continue;
                int handle = WireEdit.HitHandle(path, p, HandlePx * zoomInverse);
                if (handle >= 0) return new Target(Kind.Point, id, handle, path, manual.Contains(id));
            }

            // Grasshopper starts a new wire from a press this near a grip, which is where every wire begins and ends.
            if (doc.RelevantObjectAtPoint(canvasPoint, GH_RelevantObjectFilter.Grips) != null) return default;

            var hit = WireEdit.HitWire(routes.Paths, p, HitPx * zoomInverse);
            if (hit == null) return default;
            var hitPath = routes.Paths[hit.Value.Id];
            bool isManual = manual.Contains(hit.Value.Id);
            var action = WireEdit.ActionFor(routes.LaneFractions.ContainsKey(hit.Value.Id), isManual, hit.Value.Segment, hitPath.Length);
            var kind = action == SegmentAction.Lane ? Kind.Lane : action == SegmentAction.Segment ? Kind.Segment : Kind.None;
            if (kind == Kind.None && routes.Backs.TryGetValue(hit.Value.Id, out var runs))
            {
                var run = WireEdit.BackRunFor(hitPath, runs, hit.Value.Segment);
                if (run != BackRun.None) return new Target(Kind.Run, hit.Value.Id, hit.Value.Segment, hitPath, isManual, run);
            }
            return new Target(kind, hit.Value.Id, hit.Value.Segment, hitPath, isManual);
        }

        /// <summary>A hand-drawn wire's stored points; an automatic wire's current bends.</summary>
        static List<Pt> CurrentPoints(GH_Document doc, Target target)
        {
            if (target.Manual && DocStore.Get(doc).Effective.Points.TryGetValue(target.Id, out var stored)) return stored.Points.ToList();
            return WireEdit.PointsOf(target.Path);
        }

        static Pt[] PathOf(Pt start, IReadOnlyList<Pt> points, Pt end)
        {
            var path = new Pt[points.Count + 2];
            path[0] = start;
            for (int i = 0; i < points.Count; i++) path[i + 1] = points[i];
            path[path.Length - 1] = end;
            return path;
        }
    }
}
