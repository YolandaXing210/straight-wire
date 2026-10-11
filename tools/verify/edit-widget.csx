// Phase 2, task 6: drives the editing widget with constructed mouse events on the fixture
// (right-angle style) and reports what each gesture did. Renders the selection with its
// handles to artifacts/verify/edit-selection.png.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;

string Repo = "/Users/yolandaxing/Documents/GitHub/straight-wire";
object Dev(string method, params object[] args)
{
    var asm = (AppDomain.CurrentDomain.GetData("StraightWire.DevContext") as AssemblyLoadContext)?
                  .Assemblies.FirstOrDefault(a => a.GetName().Name == "StraightWire")
              ?? AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "StraightWire");
    return asm.GetType("StraightWire.DevHost").GetMethod(method).Invoke(null, args.Length == 0 ? null : args);
}
string Field(string name) => ((string)Dev("Describe")).Split(' ').FirstOrDefault(x => x.StartsWith(name + "=")) ?? name + "=?";
var canvas = Instances.ActiveCanvas;
var original = canvas.Document;
var widget = canvas.Widgets.First(w => w.GetType().Name == "EditWidget");
GH_Document doc = null;

void Paint(string png = null)
{
    var box = doc.BoundingBox(false);
    var vp = new GH_Viewport(canvas.Viewport) { Width = 1400, Height = 800, Zoom = 1f };
    vp.MidPoint = new PointF(box.X + box.Width / 2, box.Y + box.Height / 2);
    using (var bmp = canvas.GenerateHiResImageTile(vp, Color.White))
        if (png != null) bmp.Save(png, System.Drawing.Imaging.ImageFormat.Png);
}
GH_CanvasMouseEvent Ev(PointF p, int clicks = 1)
{
    var c = canvas.Viewport.ProjectPoint(p);
    return new GH_CanvasMouseEvent(new Point((int)c.X, (int)c.Y), p, MouseButtons.Left, clicks, 0);
}
List<(string Id, string Kind, PointF[] Pts)> Paths() =>
    ((string)Dev("Paths")).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
    {
        var parts = line.Split('|');
        var pts = parts[2].Split(';').Select(xy => xy.Split(',')).Select(a => new PointF(float.Parse(a[0], CultureInfo.InvariantCulture), float.Parse(a[1], CultureInfo.InvariantCulture))).ToArray();
        return (parts[0], parts[1], pts);
    }).ToList();
PointF Mid(PointF a, PointF b) => new PointF((a.X + b.X) / 2, (a.Y + b.Y) / 2);
PointF Add(PointF a, float dx, float dy) => new PointF(a.X + dx, a.Y + dy);

try
{
    var io = new GH_DocumentIO();
    io.Open(Repo + "/tests/fixtures/twisted-tower.gh");
    doc = io.Document;
    doc.ValueTable.SetValue("StraightWire.v1", "{\"v\":1,\"mode\":\"ortho\",\"spacing\":10,\"wires\":{}}");
    canvas.Document = doc; doc.Enabled = true; doc.NewSolution(false);
    Paint();

    var box = doc.BoundingBox(false);
    output.AppendLine("press on empty canvas: " + widget.RespondToMouseDown(canvas, Ev(new PointF(box.Right + 300, box.Bottom + 300))));
    var series = doc.Objects.First(o => o.Name == "Series").Attributes.Bounds;
    output.AppendLine("press on a component: " + widget.RespondToMouseDown(canvas, Ev(new PointF(series.X + series.Width / 2, series.Y + series.Height / 2))));

    // Lane drag on a right-angle lane wire's middle run.
    var lane = Paths().First(p => p.Kind == "lane" && p.Pts.Length == 4 && Math.Abs(p.Pts[1].Y - p.Pts[2].Y) > 40);
    var m = Mid(lane.Pts[1], lane.Pts[2]);
    output.AppendLine("press on a lane: " + widget.RespondToMouseDown(canvas, Ev(m)) + " " + Field("selected"));
    widget.RespondToMouseMove(canvas, Ev(Add(m, 1, 0)));
    widget.RespondToMouseMove(canvas, Ev(Add(m, 30, 0)));
    Paint();
    widget.RespondToMouseMove(canvas, Ev(Add(m, 30, 0)));
    output.AppendLine("release: " + widget.RespondToMouseUp(canvas, Ev(Add(m, 30, 0))));
    Paint();
    var after = Paths().First(p => p.Id == lane.Id);
    output.AppendLine($"lane x {lane.Pts[1].X:0.#} -> {after.Pts[1].X:0.#}; {Field("lanes")}; undo: {doc.UndoServer.FirstUndoName}");

    // Drag the wire's first bend: it becomes hand-drawn.
    var bend = after.Pts[1];
    output.AppendLine("press on a handle: " + widget.RespondToMouseDown(canvas, Ev(bend)));
    widget.RespondToMouseMove(canvas, Ev(Add(bend, 0, 1)));
    widget.RespondToMouseMove(canvas, Ev(Add(bend, -3, 33)));
    Paint();
    output.AppendLine("release: " + widget.RespondToMouseUp(canvas, Ev(Add(bend, -3, 33))));
    Paint(Repo + "/artifacts/verify/edit-selection.png");
    var manual = Paths().First(p => p.Id == lane.Id);
    output.AppendLine($"kind {manual.Kind}, path {string.Join(" ", manual.Pts.Select(p => $"({p.X:0},{p.Y:0})"))}; {Field("handdrawn")}; undo: {doc.UndoServer.FirstUndoName}");

    // Double-click a segment: one more point. Double-click that point: back.
    var segMid = Mid(manual.Pts[1], manual.Pts[2]);
    output.AppendLine("double-click segment: " + widget.RespondToMouseDoubleClick(canvas, Ev(segMid, 2)));
    widget.RespondToMouseUp(canvas, Ev(segMid));
    Paint();
    var added = Paths().First(p => p.Id == lane.Id);
    output.AppendLine($"points {manual.Pts.Length - 2} -> {added.Pts.Length - 2}; undo: {doc.UndoServer.FirstUndoName}");
    var newPoint = added.Pts.Skip(1).Take(added.Pts.Length - 2).First(p => !manual.Pts.Contains(p));
    output.AppendLine("double-click that point: " + widget.RespondToMouseDoubleClick(canvas, Ev(newPoint, 2)));
    Paint();
    output.AppendLine($"points now {Paths().First(p => p.Id == lane.Id).Pts.Length - 2}; undo: {doc.UndoServer.FirstUndoName}");

    // Delete resets the selected wire; Undo brings the edits back.
    typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas, new object[] { new KeyEventArgs(Keys.Delete) });
    Paint();
    output.AppendLine($"after Delete: kind {Paths().First(p => p.Id == lane.Id).Kind}; {Field("handdrawn")} {Field("lanes")}; undo: {doc.UndoServer.FirstUndoName}");
    doc.Undo();
    Paint();
    output.AppendLine($"after Undo: kind {Paths().First(p => p.Id == lane.Id).Kind}; {Field("handdrawn")} {Field("lanes")}");
    output.AppendLine("undo history: " + string.Join(" | ", doc.UndoServer.UndoNames));
}
finally
{
    canvas.Document = original;
    canvas.Refresh();
}
