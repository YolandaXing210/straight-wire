// 500 number parameters in a 25 x 20 grid with 800 seeded random wires, routed at 45°.
// Renders once (routing everything), moves one parameter 10 units and renders again
// (re-routing, as a drag does each frame), and reports the plugin's last route time.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.GUI.Canvas;

object Dev(string method, params object[] args)
{
    var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "StraightWire");
    return asm.GetType("StraightWire.DevHost").GetMethod(method).Invoke(null, args.Length == 0 ? null : args);
}

var rng = new Random(7);
var doc = new GH_Document();
var grid = new Param_Number[25, 20];
for (int c = 0; c < 25; c++)
    for (int r = 0; r < 20; r++)
    {
        var p = new Param_Number { NickName = c + "," + r };
        p.CreateAttributes();
        p.Attributes.Pivot = new PointF(c * 160, r * 60);
        doc.AddObject(p, false);
        grid[c, r] = p;
    }
for (int i = 0; i < 800; i++)
{
    int c = rng.Next(0, 24), r = rng.Next(0, 20);
    int tc = Math.Min(24, c + 1 + rng.Next(0, 3)), tr = Math.Clamp(r + rng.Next(-4, 5), 0, 19);
    grid[tc, tr].AddSource(grid[c, r]);
}
doc.ValueTable.SetValue("StraightWire.v1", "{\"v\":1,\"mode\":\"diag\",\"spacing\":10,\"wires\":{}}");
var canvas = Instances.ActiveCanvas;
var original = canvas.Document;
try
{
    canvas.Document = doc;
    var vp = new GH_Viewport(canvas.Viewport) { Width = 1200, Height = 800, Zoom = 0.4f };
    vp.MidPoint = new PointF(2000, 600);
    canvas.GenerateHiResImageTile(vp, Color.White).Dispose();
    output.AppendLine("first route: " + Dev("Describe"));
    grid[12, 10].Attributes.Pivot = new PointF(grid[12, 10].Attributes.Pivot.X + 10, grid[12, 10].Attributes.Pivot.Y);
    grid[12, 10].Attributes.ExpireLayout();
    canvas.GenerateHiResImageTile(vp, Color.White).Dispose();
    output.AppendLine("after a move: " + Dev("Describe"));
}
finally
{
    canvas.Document = original;
    canvas.Refresh();
}
