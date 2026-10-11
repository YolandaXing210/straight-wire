// Review focus 3: files with no, broken, newer, or odd settings must all open normally.
// Builds a tiny document per case, puts it on the canvas long enough to describe it, then
// restores the user's document.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Drawing;
using System.Runtime.Loader;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;

object Dev(string method, params object[] args)
{
    var asm = (AppDomain.CurrentDomain.GetData("StraightWire.DevContext") as AssemblyLoadContext)?
                  .Assemblies.FirstOrDefault(a => a.GetName().Name == "StraightWire")
              ?? AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "StraightWire");
    return asm.GetType("StraightWire.DevHost").GetMethod(method).Invoke(null, args.Length == 0 ? null : args);
}

var cases = new (string Name, string Json)[]
{
    ("no settings", null),
    ("broken", "{broken"),
    ("newer", "{\"v\":2,\"mode\":\"spiral\"}"),
    ("odd but readable", "{\"v\":1,\"mode\":\"ortho\",\"spacing\":50,\"wires\":{\"gone>gone\":\"bezier\",\"x>y\":\"zigzag\"}}"),
};
var canvas = Instances.ActiveCanvas;
var original = canvas.Document;
try
{
    foreach (var (name, json) in cases)
    {
        var doc = new GH_Document();
        var s = new Param_Number(); s.CreateAttributes(); s.Attributes.Pivot = new PointF(0, 0); doc.AddObject(s, false);
        var t = new Param_Number(); t.CreateAttributes(); t.Attributes.Pivot = new PointF(200, 40); doc.AddObject(t, false);
        t.AddSource(s);
        if (json != null) doc.ValueTable.SetValue("StraightWire.v1", json);
        canvas.Document = doc;
        output.AppendLine(name + ": " + Dev("Describe"));
    }
}
finally
{
    canvas.Document = original;
    canvas.Refresh();
}
