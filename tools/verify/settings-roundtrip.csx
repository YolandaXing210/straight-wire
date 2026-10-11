// Opening a file without settings leaves it unmodified; the first style change writes settings
// and marks it modified; per-selection styles and spacing land in the JSON; the JSON survives
// a save and reopen; Follow Global clears the selection's overrides.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.IO;
using System.Runtime.Loader;
using Grasshopper;
using Grasshopper.Kernel;

string Repo = "/Users/yolandaxing/Documents/GitHub/straight-wire";
string Saved = Repo + "/artifacts/verify/roundtrip.gh";

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
var canvas = Instances.ActiveCanvas;
var original = canvas.Document;
try
{
    canvas.Document = doc;
    output.AppendLine("opened: " + Dev("Describe") + " modified=" + doc.IsModified);
    output.AppendLine("style:ortho: " + Dev("Run", "style:ortho") + " modified=" + doc.IsModified);
    foreach (var o in doc.Objects) o.Attributes.Selected = o.Name == "Series";
    output.AppendLine("Series selected, style:bezier: " + Dev("Run", "style:bezier"));
    output.AppendLine("spacing:16: " + Dev("Run", "spacing:16"));
    string json = doc.ValueTable.GetValue("StraightWire.v1", null);
    output.AppendLine("json: " + json);
    Directory.CreateDirectory(Path.GetDirectoryName(Saved));
    new GH_DocumentIO(doc).SaveQuiet(Saved);
    var reopened = new GH_DocumentIO();
    reopened.Open(Saved);
    output.AppendLine("reopened json equal: " + (reopened.Document.ValueTable.GetValue("StraightWire.v1", null) == json));
    output.AppendLine("follow: " + Dev("Run", "follow"));
}
finally
{
    canvas.Document = original;
    canvas.Refresh();
}
