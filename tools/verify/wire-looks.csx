// Review focus 4 and 5. One row per wire look, routed right-angle (every target sits 30 lower
// than its source, so routed wires visibly bend), rendered to artifacts/verify/wire-looks.png.
// Grasshopper's cached faint pen is thrown away first, so this paint is the one that rebuilds it.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Drawing;
using System.IO;
using System.Reflection;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Grasshopper.GUI.Canvas;

string Png = "/Users/yolandaxing/Documents/GitHub/straight-wire/artifacts/verify/wire-looks.png";
Directory.CreateDirectory(Path.GetDirectoryName(Png));
string[] rows =
{
    "item", "list", "tree", "empty", "faint", "faint + target selected", "hidden",
    "hidden + source selected", "source selected", "target selected", "both selected", "curve override",
};
var doc = new GH_Document();
var pairs = new List<(Param_Number S, Param_Number T)>();
for (int i = 0; i < rows.Length; i++)
{
    var s = new Param_Number { NickName = "s" + i };
    s.CreateAttributes(); s.Attributes.Pivot = new PointF(0, i * 60); doc.AddObject(s, false);
    var t = new Param_Number { NickName = "t" + i };
    t.CreateAttributes(); t.Attributes.Pivot = new PointF(260, i * 60 + 30); doc.AddObject(t, false);
    string r = rows[i];
    if (r == "list") { s.PersistentData.Append(new GH_Number(1)); s.PersistentData.Append(new GH_Number(2)); }
    else if (r == "tree") { s.PersistentData.Append(new GH_Number(1), new GH_Path(0)); s.PersistentData.Append(new GH_Number(2), new GH_Path(1)); }
    else if (r != "empty") s.PersistentData.Append(new GH_Number(1));
    t.AddSource(s);
    if (r.StartsWith("faint")) t.WireDisplay = GH_ParamWireDisplay.faint;
    if (r.StartsWith("hidden")) t.WireDisplay = GH_ParamWireDisplay.hidden;
    if (r.Contains("target selected") || r == "both selected") t.Attributes.Selected = true;
    if (r.Contains("source selected") || r == "both selected") s.Attributes.Selected = true;
    pairs.Add((s, t));
}
var curve = pairs[rows.Length - 1];
string curveId = curve.S.InstanceGuid + ">" + curve.T.InstanceGuid;
doc.ValueTable.SetValue("StraightWire.v1", "{\"v\":1,\"mode\":\"ortho\",\"spacing\":10,\"wires\":{\"" + curveId + "\":\"bezier\"}}");

var canvas = Instances.ActiveCanvas;
var original = canvas.Document;
var before = new[] { GH_Skin.wire_default, GH_Skin.wire_empty, GH_Skin.wire_selected_a, GH_Skin.wire_selected_b };
foreach (var f in typeof(GH_Painter).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                                    .Where(f => f.Name.Contains("GenerateWirePen_Static_Faint")))
    f.SetValue(canvas.Painter, null);
try
{
    canvas.Document = doc;
    doc.Enabled = true;
    doc.NewSolution(false);
    var vp = new GH_Viewport(canvas.Viewport) { Width = 900, Height = 1500, Zoom = 2f };
    vp.MidPoint = new PointF(130, rows.Length * 30);
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
var gen = typeof(GH_Painter).GetMethod("GenerateWirePen", BindingFlags.Instance | BindingFlags.NonPublic);
var faint = (Pen)gen.Invoke(canvas.Painter, new object[] { PointF.Empty, PointF.Empty, false, false, GH_WireType.faint });
// Read the colour off the brush: on Rhino's Mac System.Drawing, Pen.Color can be Empty for this pen.
var faintColour = (faint.Brush as SolidBrush)?.Color ?? faint.Color;
output.AppendLine("faint pen: " + faintColour + " (expect A=50 and the RGB of " + before[0] + ")");
