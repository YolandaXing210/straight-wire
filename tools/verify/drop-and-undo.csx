// Phase 2, task 5: routing waits for a component drop; style changes undo and redo; hand-drawn
// points follow their wire when both components move together, and only then.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Drawing;
using System.Globalization;
using System.Runtime.Loader;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.GUI.Canvas;

string Repo = "/Users/yolandaxing/Documents/GitHub/straight-wire";
string Key = "StraightWire.v1";

object Dev(string method, params object[] args)
{
    var asm = (AppDomain.CurrentDomain.GetData("StraightWire.DevContext") as AssemblyLoadContext)?
                  .Assemblies.FirstOrDefault(a => a.GetName().Name == "StraightWire")
              ?? AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "StraightWire");
    return asm.GetType("StraightWire.DevHost").GetMethod(method).Invoke(null, args.Length == 0 ? null : args);
}
string Field(string name) => ((string)Dev("Describe")).Split(' ').FirstOrDefault(x => x.StartsWith(name + "=")) ?? name + "=?";
string N(double v) => v.ToString(CultureInfo.InvariantCulture);
GH_Document Fixture()
{
    var io = new GH_DocumentIO();
    io.Open(Repo + "/tests/fixtures/twisted-tower.gh");
    return io.Document;
}
void Paint(GH_Canvas c, GH_Document d)
{
    var box = d.BoundingBox(false);
    var vp = new GH_Viewport(c.Viewport) { Width = 800, Height = 600, Zoom = 0.6f };
    vp.MidPoint = new PointF(box.X + box.Width / 2, box.Y + box.Height / 2);
    c.GenerateHiResImageTile(vp, Color.White).Dispose();
}
void Move(IGH_DocumentObject o, float dx, float dy)
{
    o.Attributes.Pivot = new PointF(o.Attributes.Pivot.X + dx, o.Attributes.Pivot.Y + dy);
    o.Attributes.ExpireLayout();
    o.Attributes.PerformLayout();
}

var canvas = Instances.ActiveCanvas;
var original = canvas.Document;
try
{
    // 1. A component drag holds routing until the drop.
    var doc = Fixture();
    doc.ValueTable.SetValue(Key, "{\"v\":1,\"mode\":\"diag\",\"spacing\":10,\"wires\":{}}");
    canvas.Document = doc; doc.Enabled = true; doc.NewSolution(false);
    Paint(canvas, doc);
    output.AppendLine("before drag: " + Field("drawn") + " " + Field("last-route"));
    output.AppendLine((string)Dev("Run", "drag:on"));
    Move(doc.Objects.First(o => o.Name == "Area"), 40, 30);
    Paint(canvas, doc);
    output.AppendLine("dragging Area: " + Field("drawn") + " (expect 16: its 2 wires drawn as curves)");
    output.AppendLine((string)Dev("Run", "drag:off"));
    Paint(canvas, doc);
    output.AppendLine("dropped: " + Field("drawn") + " " + Field("last-route"));

    // 2. Style changes are one undo step each. (Grasshopper's settings store keeps an empty value
    //    for any key that was read while missing, so "no settings" reads back as empty, not null.)
    doc = Fixture();
    canvas.Document = doc;
    Dev("Run", "style:ortho");
    output.AppendLine("after style:ortho: " + Field("settings") + " undo count " + doc.UndoServer.UndoCount + " (" + doc.UndoServer.FirstUndoName + ")");
    doc.Undo();
    output.AppendLine("after Undo: " + Field("settings") + " key " + (string.IsNullOrEmpty(doc.ValueTable.GetValue(Key, null)) ? "empty" : "present"));
    doc.Redo();
    output.AppendLine("after Redo: " + Field("settings") + " " + Field("effective"));

    // 3. Hand-drawn points follow only a together-move.
    doc = Fixture();
    var series = (IGH_Component)doc.Objects.First(o => o.Name == "Series");
    var unitz = (IGH_Component)doc.Objects.First(o => o.Name == "Unit Z");
    string id = series.Params.Output[0].InstanceGuid + ">" + unitz.Params.Input[0].InstanceGuid;
    RectangleF sb = series.Attributes.Bounds, tb = unitz.Attributes.Bounds;
    doc.ValueTable.SetValue(Key, "{\"v\":1,\"mode\":\"ortho\",\"spacing\":10,\"wires\":{},\"points\":{\"" + id + "\":{\"at\":[" +
        N(sb.Left) + "," + N(sb.Top) + "," + N(tb.Left) + "," + N(tb.Top) + "],\"p\":[450,180,450,260]}}}");
    canvas.Document = doc;
    string Pts() { var j = doc.ValueTable.GetValue(Key, ""); int i = j.IndexOf("\"p\":["); return i < 0 ? "none" : j.Substring(i + 5, j.IndexOf(']', i) - i - 5); }
    Paint(canvas, doc);
    output.AppendLine("points at start: " + Pts());
    Move(series, 30, 20); Move(unitz, 30, 20); Paint(canvas, doc);
    output.AppendLine("both moved +30,+20: " + Pts() + " (expect 480,200,480,280)");
    Move(unitz, 50, 0); Paint(canvas, doc);
    output.AppendLine("only Unit Z moved +50: " + Pts() + " (expect unchanged)");
    Move(unitz, -50, 0); Paint(canvas, doc);
    Move(series, -30, -20); Paint(canvas, doc);
    Move(unitz, -30, -20); Paint(canvas, doc);
    output.AppendLine("moved back one at a time: " + Pts() + " (expect 480,200,480,280: never together in one step)");
    Move(series, 30, 20); Move(unitz, 30, 20); Paint(canvas, doc);
    Move(series, -30, -20); Move(unitz, -30, -20); Paint(canvas, doc);
    output.AppendLine("together and back: " + Pts() + " (expect 480,200,480,280 again)");
}
finally
{
    Dev("Run", "drag:off");
    canvas.Document = original;
    canvas.Refresh();
}
