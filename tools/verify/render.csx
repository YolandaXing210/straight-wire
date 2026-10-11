// Renders the twisted-tower fixture with the loaded build in one style, to
// artifacts/verify/tower-<mode>.png, and checks the wire skin colours come back.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Drawing;
using System.IO;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.GUI.Canvas;

string Repo = "/Users/yolandaxing/Documents/GitHub/straight-wire";
string Mode = "diag";   // "bezier", "ortho" or "diag"
string Png = Repo + "/artifacts/verify/tower-" + Mode + ".png";
Directory.CreateDirectory(Path.GetDirectoryName(Png));

var io = new GH_DocumentIO();
if (!io.Open(Repo + "/tests/fixtures/twisted-tower.gh")) output.AppendLine("could not open the fixture");
else
{
    var doc = io.Document;
    doc.ValueTable.SetValue("StraightWire.v1", "{\"v\":1,\"mode\":\"" + Mode + "\",\"spacing\":10,\"wires\":{}}");
    var canvas = Instances.ActiveCanvas;
    var original = canvas.Document;
    var before = new[] { GH_Skin.wire_default, GH_Skin.wire_empty, GH_Skin.wire_selected_a, GH_Skin.wire_selected_b };
    try
    {
        canvas.Document = doc;
        doc.Enabled = true;
        doc.NewSolution(false);
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
        canvas.Refresh();
    }
    var after = new[] { GH_Skin.wire_default, GH_Skin.wire_empty, GH_Skin.wire_selected_a, GH_Skin.wire_selected_b };
    output.AppendLine("skin colours restored: " + before.SequenceEqual(after));
}
