// Calls a public static method of StraightWire.DevHost on the loaded build (a dev build from
// tools/dev-reload.csx, or the installed plugin) and prints what it returns.
// Edit the two lines below, then send this file's text to execute_rhinocommon_csharp_code.
using System.Reflection;
using System.Runtime.Loader;

string Method = "Describe";   // Attach, Detach, Describe, Run
object[] Args = null;         // for Run, e.g. new object[] { "style:ortho" }

var asm = (AppDomain.CurrentDomain.GetData("StraightWire.DevContext") as AssemblyLoadContext)?
              .Assemblies.FirstOrDefault(a => a.GetName().Name == "StraightWire")
          ?? AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "StraightWire");
output.AppendLine(asm == null
    ? "Straight Wire is not loaded."
    : asm.GetType("StraightWire.DevHost").GetMethod(Method).Invoke(null, Args) as string);
