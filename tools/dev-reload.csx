// Loads the Debug build of Straight Wire into the running Rhino without a restart.
// Send this file's text to the Rhino MCP tool execute_rhinocommon_csharp_code.
// Each run detaches and unloads the previous dev build first (and detaches an installed copy).
// Build first with:
//   dotnet build src/StraightWire --nologo -v q
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

string Gha = "/Users/yolandaxing/Documents/GitHub/straight-wire/src/StraightWire/bin/Debug/StraightWire.gha";
string Slot = "StraightWire.DevContext";

// An installed copy is detached rather than refused: the dev build takes over until Rhino restarts.
var installed = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
    a.GetName().Name == "StraightWire" && AssemblyLoadContext.GetLoadContext(a) == AssemblyLoadContext.Default);
if (installed != null)
    output.AppendLine("Installed plugin: " + (installed.GetType("StraightWire.DevHost")?.GetMethod("Detach")?.Invoke(null, null) as string ?? "no DevHost to detach"));
{
    if (AppDomain.CurrentDomain.GetData(Slot) is AssemblyLoadContext previous)
    {
        var old = previous.Assemblies.FirstOrDefault(a => a.GetName().Name == "StraightWire");
        output.AppendLine(old?.GetType("StraightWire.DevHost")?.GetMethod("Detach")?.Invoke(null, null) as string
                          ?? "The previous build had no DevHost.");
        AppDomain.CurrentDomain.SetData(Slot, null);
        previous.Unload();
    }
    var context = new AssemblyLoadContext("StraightWire.Dev." + DateTime.Now.Ticks, isCollectible: true);
    Assembly asm;
    using (var stream = File.OpenRead(Gha)) asm = context.LoadFromStream(stream);
    AppDomain.CurrentDomain.SetData(Slot, context);
    output.AppendLine(asm.GetType("StraightWire.DevHost").GetMethod("Attach").Invoke(null, null) as string);
    output.AppendLine("Build written at " + File.GetLastWriteTime(Gha).ToString("HH:mm:ss") + ".");
}
