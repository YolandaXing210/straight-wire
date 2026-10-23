// A press near a parameter grip starts a new wire, as it does without Straight Wire: the editing
// widget leaves every press Grasshopper sees as a grip alone, even where a routed wire runs
// out of that grip. Presses on the same wires further out still select them, and a selected
// wire's bend point near a grip is still a handle.
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
List<(string Id, PointF[] Pts)> Paths() =>
    ((string)Dev("Paths")).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
        (line.Split('|')[0], line.Split('|')[2].Split(';').Select(xy => xy.Split(','))
            .Select(a => new PointF(float.Parse(a[0], CultureInfo.InvariantCulture), float.Parse(a[1], CultureInfo.InvariantCulture))).ToArray()))
        .ToList();
string F(double v) => v.ToString(CultureInfo.InvariantCulture);
// The point d along a path from its start.
PointF Along(PointF[] pts, float d)
{
    for (int i = 0; i + 1 < pts.Length; i++)
    {
        float dx = pts[i + 1].X - pts[i].X, dy = pts[i + 1].Y - pts[i].Y, len = (float)Math.Sqrt(dx * dx + dy * dy);
        if (d <= len && len > 0) return new PointF(pts[i].X + dx * d / len, pts[i].Y + dy * d / len);
        d -= len;
    }
    return pts[pts.Length - 1];
}
// What the widget does with a press there; a captured press is released again.
GH_ObjectResponse Press(PointF p)
{
    var response = widget.RespondToMouseDown(canvas, Ev(p));
    if (response == GH_ObjectResponse.Capture) widget.RespondToMouseUp(canvas, Ev(p));
    return response;
}
bool IsGrip(PointF p)
{
    var relevant = doc.RelevantObjectAtPoint(p);
    return relevant != null && (relevant.ObjectType == GH_ObjectSpecies.grip_in || relevant.ObjectType == GH_ObjectSpecies.grip_out);
}

try
{
    var io = new GH_DocumentIO();
    io.Open(Repo + "/tests/fixtures/twisted-tower.gh");
    doc = io.Document;
    doc.ValueTable.SetValue("StraightWire.v1", "{\"v\":1,\"mode\":\"ortho\",\"spacing\":10,\"wires\":{}}");
    canvas.Document = doc; doc.Enabled = true; doc.NewSolution(false);
    Paint();

    int near = 0, nearTaken = 0, far = 0, farTaken = 0;
    var taken = new List<string>();
    var empty = new PointF(doc.BoundingBox(false).Right + 300, doc.BoundingBox(false).Bottom + 300);
    foreach (var (_, path) in Paths())
    {
        Press(empty);   // no wire selected, so no handles in the way
        var reversed = path.Reverse().ToArray();
        foreach (float d in new[] { 3f, 6f, 9f, 11f })
            foreach (var p in new[] { Along(path, d), Along(reversed, d) })
            {
                if (!IsGrip(p)) continue;
                near++;
                if (Press(p) == GH_ObjectResponse.Capture)
                {
                    nearTaken++;
                    if (taken.Count < 5) taken.Add($"({p.X:0.#},{p.Y:0.#}) {d} from a grip");
                }
            }
        float length = 0;
        for (int i = 0; i + 1 < path.Length; i++) length += (float)Math.Sqrt(Math.Pow(path[i + 1].X - path[i].X, 2) + Math.Pow(path[i + 1].Y - path[i].Y, 2));
        var middle = Along(path, length / 2);
        if (length > 60 && doc.RelevantObjectAtPoint(middle) == null)
        {
            far++;
            if (Press(middle) == GH_ObjectResponse.Capture) farTaken++;
        }
    }
    output.AppendLine($"presses on a wire inside Grasshopper's grip radius: {near}, taken by the widget: {nearTaken} (want 0)");
    foreach (string where in taken) output.AppendLine("  taken at " + where);
    output.AppendLine($"presses on a wire's middle, clear of everything: {far}, taken by the widget: {farTaken} (want {far})");

    // Make a lane wire hand-drawn with its first point 10 from its output grip, and select it.
    var (id, lane) = Paths().First(p => p.Pts.Length == 4 && Math.Abs(p.Pts[1].Y - p.Pts[2].Y) > 40);
    PointF s = lane[0], t = lane[3], handle = new PointF(s.X + 10, s.Y);
    doc.ValueTable.SetValue("StraightWire.v1", "{\"v\":1,\"mode\":\"ortho\",\"spacing\":10,\"wires\":{},\"points\":{\"" + id +
        "\":{\"at\":[0,0,0,0],\"p\":[" + F(handle.X) + "," + F(s.Y) + "," + F(handle.X) + "," + F(t.Y) + "]}}}");
    var asm = (AppDomain.CurrentDomain.GetData("StraightWire.DevContext") as AssemblyLoadContext)?.Assemblies.FirstOrDefault(a => a.GetName().Name == "StraightWire")
              ?? AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "StraightWire");
    asm.GetType("StraightWire.DocStore").GetMethod("Reload").Invoke(null, new object[] { doc });
    Paint();
    Press(new PointF(handle.X, (s.Y + t.Y) / 2));
    bool isGrip = IsGrip(handle);
    var onHandle = Press(handle);
    var beside = Press(new PointF(s.X + 2, s.Y));
    output.AppendLine($"selected wire's bend point 10 from its grip (grip zone: {isGrip}): {onHandle} (want Capture); " +
                      $"the same wire 2 from the grip: {beside} (want Ignore)");
    bool handles = isGrip && onHandle == GH_ObjectResponse.Capture && beside == GH_ObjectResponse.Ignore;
    output.AppendLine(nearTaken == 0 && farTaken == far && near > 0 && far > 0 && handles ? "PASS" : "FAIL");
}
finally
{
    canvas.Document = original;
    canvas.Refresh();
}
