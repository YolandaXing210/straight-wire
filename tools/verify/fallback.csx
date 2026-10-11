// Forces the next takeover to throw, renders the fixture, and checks the canvas fell back to
// Grasshopper's own wires with the skin intact. Then lets the canvas try again.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Drawing;
using System.IO;
using System.Runtime.Loader;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.GUI.Canvas;

string Repo = "/Users/yolandaxing/Documents/GitHub/straight-wire";
string Png = Repo + "/artifacts/verify/fallback.png";
Directory.CreateDirectory(Path.GetDirectoryName(Png));

object Dev(string method, params object[] args)
{
    var asm = (AppDomain.CurrentDomain.GetData("StraightWire.DevContext") as AssemblyLoadContext)?
                  .Assemblies.FirstOrDefault(a => a.GetName().Name == "StraightWire")
              ?? AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "StraightWire");
    return asm.GetType("StraightWire.DevHost").GetMethod(method).Invoke(null, args.Length == 0 ? null : args);
}

var io = new GH_DocumentIO();
io.Open(Repo + "/tests/fixtures/twisted-tower.gh");
var doc = io.Document;
doc.ValueTable.SetValue("StraightWire.v1", "{\"v\":1,\"mode\":\"diag\",\"spacing\":10,\"wires\":{}}");
var canvas = Instances.ActiveCanvas;
var original = canvas.Document;
var before = new[] { GH_Skin.wire_default, GH_Skin.wire_empty, GH_Skin.wire_selected_a, GH_Skin.wire_selected_b };
try
{
    canvas.Document = doc;
    doc.Enabled = true;
    doc.NewSolution(false);
    output.AppendLine((string)Dev("Run", "fail-next-paint"));
    var box = doc.BoundingBox(false);
    var vp = new GH_Viewport(canvas.Viewport) { Width = 1600, Height = 900 };
    vp.Zoom = Math.Min(1600f / (box.Width + 80), 900f / (box.Height + 80));
    vp.MidPoint = new PointF(box.X + box.Width / 2, box.Y + box.Height / 2);
    using (var bmp = canvas.GenerateHiResImageTile(vp, Color.White))
        bmp.Save(Png, System.Drawing.Imaging.ImageFormat.Png);
    output.AppendLine("saved " + Png);
}
finally
{
    canvas.Document = original;
    output.AppendLine((string)Dev("Run", "retry"));
}
var after = new[] { GH_Skin.wire_default, GH_Skin.wire_empty, GH_Skin.wire_selected_a, GH_Skin.wire_selected_b };
output.AppendLine("skin colours restored: " + before.SequenceEqual(after));
