// Phase 3, task 4: the menu label follows the selection, the widget's Tidy button and Alt+T both
// tidy. Renders the widget corner to artifacts/verify/tidy-widget.png.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Drawing;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;

string Repo = "/Users/yolandaxing/Documents/GitHub/straight-wire";
Assembly Plugin() =>
    (AppDomain.CurrentDomain.GetData("StraightWire.DevContext") as AssemblyLoadContext)?.Assemblies.FirstOrDefault(a => a.GetName().Name == "StraightWire")
    ?? AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "StraightWire");
string Label() => (string)Plugin().GetType("StraightWire.Tidier").GetMethod("Label", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { canvas });

var canvas = Instances.ActiveCanvas;
var original = canvas.Document;
GH_Document doc = null;
try
{
    var io = new GH_DocumentIO();
    io.Open(Repo + "/tests/fixtures/twisted-tower.gh");
    doc = io.Document;
    doc.ValueTable.SetValue("StraightWire.v1", "{\"v\":1,\"mode\":\"ortho\",\"spacing\":10,\"wires\":{}}");
    canvas.Document = doc;
    doc.Enabled = true;
    doc.NewSolution(false);
    canvas.Refresh();

    output.AppendLine("label, nothing selected: " + Label());
    var link = doc.Objects.OfType<IGH_Component>().SelectMany(c => c.Params.Input)
                  .SelectMany(t => t.Sources.Select(s => (S: s.Attributes.GetTopLevel.DocObject, T: t.Attributes.GetTopLevel.DocObject)))
                  .First(c => c.S != c.T && c.S is IGH_Component);
    link.S.Attributes.Selected = true;
    link.T.Attributes.Selected = true;
    output.AppendLine("label, two linked components selected: " + Label());
    doc.DeselectAll();

    // The widget's Tidy button: 8 px after the third style button.
    var widget = canvas.Widgets.First(w => w.GetType().Name == "StyleWidget");
    var tidyCentre = new Point(12 + 3 * (52 + 2) - 2 + 8 + 26, canvas.ClientSize.Height - 12 - 11);
    output.AppendLine("widget claims the Tidy button: " + widget.Contains(tidyCentre, canvas.Viewport.UnprojectPoint(tidyCentre)));
    var press = new GH_CanvasMouseEvent(tidyCentre, canvas.Viewport.UnprojectPoint(tidyCentre), MouseButtons.Left, 1, 0);
    output.AppendLine("click Tidy: " + widget.RespondToMouseDown(canvas, press) + ", undo: " + doc.UndoServer.FirstUndoName);

    // Alt+T, after undoing the widget's tidy.
    doc.Undo();
    int records = doc.UndoServer.UndoCount;
    typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic)
                   .Invoke(canvas, new object[] { new KeyEventArgs(Keys.T | Keys.Alt) });
    output.AppendLine("Alt+T: records +" + (doc.UndoServer.UndoCount - records) + ", undo: " + doc.UndoServer.FirstUndoName);

    canvas.Refresh();
    using (var shot = new Bitmap(canvas.ClientSize.Width, canvas.ClientSize.Height))
    {
        canvas.DrawToBitmap(shot, new Rectangle(Point.Empty, canvas.ClientSize));
        using (var corner = shot.Clone(new Rectangle(0, canvas.ClientSize.Height - 60, Math.Min(420, canvas.ClientSize.Width), 60), shot.PixelFormat))
            corner.Save(Repo + "/artifacts/verify/tidy-widget.png", System.Drawing.Imaging.ImageFormat.Png);
    }
}
finally
{
    canvas.Document = original;
    canvas.Refresh();
}
