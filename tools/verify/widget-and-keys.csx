// Checks the widget is registered and hit-tests its buttons, and that Alt+2 on the canvas
// switches a document to Orthogonal (by raising the canvas's own KeyDown).
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Drawing;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows.Forms;
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

var canvas = Instances.ActiveCanvas;
var widget = canvas.Widgets.FirstOrDefault(w => w.GetType().Name == "StyleWidget");
output.AppendLine("widget registered: " + (widget != null));
if (widget != null)
{
    int h = canvas.ClientSize.Height;
    output.AppendLine("hit on first button: " + widget.Contains(new Point(20, h - 20), PointF.Empty));
    output.AppendLine("hit far away: " + widget.Contains(new Point(400, 20), PointF.Empty));
}

var doc = new GH_Document();
var s = new Param_Number(); s.CreateAttributes(); s.Attributes.Pivot = new PointF(0, 0); doc.AddObject(s, false);
var t = new Param_Number(); t.CreateAttributes(); t.Attributes.Pivot = new PointF(200, 40); doc.AddObject(t, false);
t.AddSource(s);
var original = canvas.Document;
try
{
    canvas.Document = doc;
    var onKeyDown = typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic);
    onKeyDown.Invoke(canvas, new object[] { new KeyEventArgs(Keys.Alt | Keys.D2) });
    output.AppendLine("after Alt+2: " + Dev("Describe"));
}
finally
{
    canvas.Document = original;
    canvas.Refresh();
}
