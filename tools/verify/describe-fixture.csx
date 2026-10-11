// Opens the twisted-tower fixture on the canvas just long enough to ask the loaded build to
// describe it, then puts the user's document back. Set Json to give the fixture settings.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Runtime.Loader;
using Grasshopper;
using Grasshopper.Kernel;

string Repo = "/Users/yolandaxing/Documents/GitHub/straight-wire";
string Json = null;   // e.g. "{\"v\":1,\"mode\":\"ortho\",\"spacing\":10,\"wires\":{}}"

object Dev(string method, params object[] args)
{
    var asm = (AppDomain.CurrentDomain.GetData("StraightWire.DevContext") as AssemblyLoadContext)?
                  .Assemblies.FirstOrDefault(a => a.GetName().Name == "StraightWire")
              ?? AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "StraightWire");
    return asm.GetType("StraightWire.DevHost").GetMethod(method).Invoke(null, args.Length == 0 ? null : args);
}

var io = new GH_DocumentIO();
if (!io.Open(Repo + "/tests/fixtures/twisted-tower.gh")) output.AppendLine("could not open the fixture");
else
{
    var doc = io.Document;
    if (Json != null) doc.ValueTable.SetValue("StraightWire.v1", Json);
    var canvas = Instances.ActiveCanvas;
    var original = canvas.Document;
    try
    {
        canvas.Document = doc;
        output.AppendLine((string)Dev("Describe"));
        output.AppendLine("modified=" + doc.IsModified);
    }
    finally
    {
        canvas.Document = original;
        canvas.Refresh();
    }
}
