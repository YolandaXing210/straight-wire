// With the installed plugin: the first settings write adds one marker 40 above the top-left of
// the definition, showing the settings; it survives a save and reopen; a second write adds none.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.IO;
using Grasshopper;
using Grasshopper.Kernel;

string Repo = "/Users/yolandaxing/Documents/GitHub/straight-wire";
string Saved = Repo + "/artifacts/verify/with-marker.gh";

object Dev(string method, params object[] args)
{
    var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "StraightWire");
    return asm.GetType("StraightWire.DevHost").GetMethod(method).Invoke(null, args.Length == 0 ? null : args);
}

var io = new GH_DocumentIO();
io.Open(Repo + "/tests/fixtures/twisted-tower.gh");
var doc = io.Document;
float left = doc.Objects.Min(o => o.Attributes.Bounds.Left), top = doc.Objects.Min(o => o.Attributes.Bounds.Top);
var canvas = Instances.ActiveCanvas;
var original = canvas.Document;
try
{
    canvas.Document = doc;
    Dev("Run", "save");
    var markers = doc.Objects.Where(o => o.ComponentGuid == new Guid("8E05DF3D-B91F-431C-ABDB-C638AE1934E4")).ToList();
    output.AppendLine("markers after first save: " + markers.Count);
    if (markers.Count == 1)
    {
        var m = (GH_Component)markers[0];
        var b = m.Attributes.Bounds;
        output.AppendLine("message: " + m.Message);
        output.AppendLine($"bottom-left ({b.Left:0}, {b.Bottom:0}), expected ({left:0}, {top - 40:0})");
    }
    Dev("Run", "style:ortho");
    output.AppendLine("markers after another change: " + doc.Objects.Count(o => o.ComponentGuid == new Guid("8E05DF3D-B91F-431C-ABDB-C638AE1934E4")));
    Directory.CreateDirectory(Path.GetDirectoryName(Saved));
    new GH_DocumentIO(doc).SaveQuiet(Saved);
    var reopened = new GH_DocumentIO();
    reopened.Open(Saved);
    output.AppendLine("markers after reopen: " + reopened.Document.Objects.Count(o => o.ComponentGuid == new Guid("8E05DF3D-B91F-431C-ABDB-C638AE1934E4")));
}
finally
{
    canvas.Document = original;
    canvas.Refresh();
}
