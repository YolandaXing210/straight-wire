// Drags each run of the fixture's backwards wire (right-angle style) with constructed mouse
// events: the out vertical right, the in vertical left, the crossing down. Each stays where it
// was dropped, as one "Move Wire Lane" undo step, and Delete puts the wire back.
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

void Paint()
{
    var box = doc.BoundingBox(false);
    var vp = new GH_Viewport(canvas.Viewport) { Width = 1400, Height = 800, Zoom = 1f };
    vp.MidPoint = new PointF(box.X + box.Width / 2, box.Y + box.Height / 2);
    using (canvas.GenerateHiResImageTile(vp, Color.White)) { }
}
GH_CanvasMouseEvent Ev(PointF p)
{
    var c = canvas.Viewport.ProjectPoint(p);
    return new GH_CanvasMouseEvent(new Point((int)c.X, (int)c.Y), p, MouseButtons.Left, 1, 0);
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
string Show(PointF[] pts) => string.Join(" ", pts.Select(p => $"({p.X:0.#},{p.Y:0.#})"));

// Presses segment `segment` of the wire, drags it by (dx, dy) and releases; returns the new path.
PointF[] Drag(string id, int segment, float dx, float dy)
{
    var path = Paths().First(p => p.Id == id).Pts;
    var at = Mid(path[segment], path[segment + 1]);
    var press = widget.RespondToMouseDown(canvas, Ev(at));
    widget.RespondToMouseMove(canvas, Ev(Add(at, dx / 30, dy / 30)));
    widget.RespondToMouseMove(canvas, Ev(Add(at, dx, dy)));
    Paint();
    widget.RespondToMouseMove(canvas, Ev(Add(at, dx, dy)));
    var release = widget.RespondToMouseUp(canvas, Ev(Add(at, dx, dy)));
    Paint();
    output.AppendLine($"  press {press}, release {release}; {Field("backs")}; undo: {doc.UndoServer.FirstUndoName}");
    return Paths().First(p => p.Id == id).Pts;
}

bool pass = true;
void Check(string what, bool ok)
{
    output.AppendLine((ok ? "  ok: " : "  WRONG: ") + what);
    pass &= ok;
}

try
{
    var io = new GH_DocumentIO();
    io.Open(Repo + "/tests/fixtures/twisted-tower.gh");
    doc = io.Document;
    doc.ValueTable.SetValue("StraightWire.v1", "{\"v\":1,\"mode\":\"ortho\",\"spacing\":10,\"wires\":{}}");
    canvas.Document = doc; doc.Enabled = true; doc.NewSolution(false);
    Paint();

    var back = Paths().First(p => p.Pts[p.Pts.Length - 1].X < p.Pts[0].X + 8);
    var start = back.Pts;
    output.AppendLine($"back wire ({back.Kind}): {Show(start)}");

    output.AppendLine("out vertical +30 x:");
    var afterOut = Drag(back.Id, 1, 30, 0);
    output.AppendLine("  " + Show(afterOut));
    Check("out vertical moved 30 right", Math.Abs(afterOut[1].X - (start[1].X + 30)) < 0.01 && Math.Abs(afterOut[2].X - afterOut[1].X) < 0.01);
    Check("undo step is Move Wire Lane", doc.UndoServer.FirstUndoName == "Move Wire Lane");

    output.AppendLine("in vertical -30 x:");
    var afterIn = Drag(back.Id, 3, -30, 0);
    output.AppendLine("  " + Show(afterIn));
    Check("in vertical moved 30 left, out vertical kept", Math.Abs(afterIn[3].X - (start[3].X - 30)) < 0.01 && Math.Abs(afterIn[1].X - afterOut[1].X) < 0.01);

    output.AppendLine("crossing +20 y:");
    var afterCross = Drag(back.Id, 2, 0, 20);
    output.AppendLine("  " + Show(afterCross));
    Check("crossing moved down (20, or as far as the gap allows)", afterCross[2].Y > start[2].Y + 0.01 && afterCross[2].Y <= start[2].Y + 20.01);
    Check("both verticals kept", Math.Abs(afterCross[1].X - afterOut[1].X) < 0.01 && Math.Abs(afterCross[3].X - afterIn[3].X) < 0.01);
    Check("one stored entry", Field("backs") == "backs=1");
    output.AppendLine("undo history: " + string.Join(" | ", doc.UndoServer.UndoNames));

    // The last press selected the wire: Delete resets it, Undo brings the edit back.
    typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(canvas, new object[] { new KeyEventArgs(Keys.Delete) });
    Paint();
    var reset = Paths().First(p => p.Id == back.Id).Pts;
    Check("Delete puts the wire back: " + Show(reset), reset.SequenceEqual(start) && Field("backs") == "backs=0");
    doc.Undo();
    Paint();
    var undone = Paths().First(p => p.Id == back.Id).Pts;
    Check("Undo brings the edit back: " + Show(undone), undone.SequenceEqual(afterCross));
    output.AppendLine(pass ? "PASS" : "FAIL");
}
finally
{
    canvas.Document = original;
    canvas.Refresh();
}
