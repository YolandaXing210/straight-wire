// Run in Rhino 8 using execute_rhinocommon_csharp_code after a Release build.
// Uses an isolated assembly and canvas; does not replace the active plugin or document.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Drawing;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.GUI.Widgets;
using Grasshopper.Kernel;

string repo = "/Users/lehands/Documents/GitHub/straight-wire";
var context = new AssemblyLoadContext("StraightWire.EditModeVerification", true);
Assembly asm;
using (var stream = File.OpenRead(repo + "/src/StraightWire/bin/Release/StraightWire.gha")) asm = context.LoadFromStream(stream);
Type T(string name) => asm.GetType("StraightWire." + name, true);
object Call(string type, string method, params object[] args) => T(type).GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, args);
var mode = T("WireEditMode").GetProperty("Enabled");
void Mode(bool on) => mode.SetValue(null, on);
int passed = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL: " + label); passed++; output.AppendLine("PASS: " + label); }
var canvas = new GH_Canvas();
var other = new GH_Canvas();
GH_Document fixture = null;
try
{
    Check(!(bool)mode.GetValue(null), "new session defaults OFF");
    var io = new GH_DocumentIO();
    Check(io.Open(repo + "/tests/fixtures/twisted-tower.gh"), "fixture opens");
    fixture = io.Document;
    fixture.ValueTable.SetValue("StraightWire.v1", "{\"v\":1,\"mode\":\"ortho\",\"spacing\":10,\"wires\":{}}");
    canvas.Document = fixture;
    canvas.Size = new Size(1400, 800);
    Call("CanvasHook", "Attach", canvas);
    Call("CanvasHook", "Attach", other);
    var widget = canvas.Widgets.First(w => w.GetType().Assembly == asm && w.GetType().Name == "EditWidget");
    var otherWidget = other.Widgets.First(w => w.GetType().Assembly == asm && w.GetType().Name == "EditWidget");
    var editor = Call("WireEditor", "For", canvas);
    var otherEditor = Call("WireEditor", "For", other);
    object Field(object obj, string key) => obj.GetType().GetField(key).GetValue(obj);
    void Paint()
    {
        var vp = new GH_Viewport(canvas.Viewport) { Width = 1400, Height = 800, Zoom = 1f };
        using (var bmp = canvas.GenerateHiResImageTile(vp, Color.White)) { }
    }
    Paint();
    var routes = Field(editor, "LastRoutes");
    Check(routes != null, "OFF still routes wires");
    var paths = (System.Collections.IDictionary)routes.GetType().GetProperty("Paths").GetValue(routes);
    var points = paths.Values.Cast<Array>().First(x => x.Length >= 4);
    PointF Pt(int i) { var p = points.GetValue(i); return new PointF((float)(double)p.GetType().GetProperty("X").GetValue(p), (float)(double)p.GetType().GetProperty("Y").GetValue(p)); }
    var a = Pt(1); var b = Pt(2); var mid = new PointF((a.X+b.X)/2, (a.Y+b.Y)/2);
    GH_CanvasMouseEvent Ev(PointF p, int clicks = 1) => new GH_CanvasMouseEvent(Point.Round(canvas.Viewport.ProjectPoint(p)), p, MouseButtons.Left, clicks, 0);
    string Saved() => fixture.ValueTable.GetValue("StraightWire.v1", "");
    string Undo() => string.Join("|", fixture.UndoServer.UndoNames);
    var before = Saved(); var undo = Undo(); var modified = fixture.IsModified;
    Check(!widget.Contains(Point.Empty, mid), "OFF Contains is false");
    Check(widget.RespondToMouseDown(canvas, Ev(mid)) == GH_ObjectResponse.Ignore, "OFF mouse down ignored");
    Check(widget.RespondToMouseMove(canvas, Ev(mid)) == GH_ObjectResponse.Ignore, "OFF mouse move ignored");
    Check(widget.RespondToMouseUp(canvas, Ev(mid)) == GH_ObjectResponse.Ignore, "OFF mouse up ignored");
    Check(widget.RespondToMouseDoubleClick(canvas, Ev(mid, 2)) == GH_ObjectResponse.Ignore, "OFF double click ignored");
    var menu = (ToolStripMenuItem)T("UI.StyleMenu").GetMethod("Build", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
    var toggle = menu.DropDownItems.OfType<ToolStripMenuItem>().First(x => x.Text == "Wire Edit Mode");
    var spacing = menu.DropDownItems.OfType<ToolStripMenuItem>().First(x => x.Text == "Wire Spacing");
    Check(menu.DropDownItems.IndexOf(toggle) + 1 == menu.DropDownItems.IndexOf(spacing) && !toggle.Checked, "menu position and initial check");
    toggle.PerformClick();
    Check(toggle.Checked && (bool)mode.GetValue(null), "menu click enables mode immediately");
    menu.Dispose();
    Check(widget.Visible && otherWidget.Visible, "ON shared across canvases");
    widget.GetType().GetMethod("Hover").Invoke(widget, new object[] { canvas, mid });
    Check(Field(editor, "Hover") != null, "ON hover detects wire");
    Check(widget.RespondToMouseDown(canvas, Ev(mid)) == GH_ObjectResponse.Capture, "ON selection captures");
    canvas.ActiveWidget = widget;
    widget.RespondToMouseMove(canvas, Ev(new PointF(mid.X+30, mid.Y+30)));
    Check(Field(editor, "Pin") != null || Field(editor, "Live") != null, "ON drag creates preview");
    otherEditor.GetType().GetField("Hover").SetValue(otherEditor, "test");
    ((System.Collections.Generic.HashSet<string>)Field(otherEditor,"Selected")).Add("test");
    Mode(false);
    Check(canvas.ActiveWidget == null, "OFF releases own active widget");
    Check(Field(editor,"Pin") == null && Field(editor,"Live") == null && Field(editor,"Hover") == null, "OFF clears drag and hover");
    Check(((System.Collections.Generic.HashSet<string>)Field(editor,"Selected")).Count == 0 && ((System.Collections.Generic.HashSet<string>)Field(otherEditor,"Selected")).Count == 0 && Field(otherEditor,"Hover") == null, "OFF clears every canvas selection");
    Check(widget.RespondToMouseUp(canvas, Ev(mid)) == GH_ObjectResponse.Ignore, "late mouse up cannot commit");
    Check(before == Saved() && undo == Undo() && modified == fixture.IsModified, "cancel preserves settings, undo and modified state");
    Mode(true); Paint();
    Check(widget.RespondToMouseDoubleClick(canvas, Ev(mid,2)) == GH_ObjectResponse.Handled, "ON double click adds points");
    Check(before != Saved(), "control points saved");
    before = Saved(); undo = Undo(); modified = fixture.IsModified;
    for (int i=0;i<3;i++) { Mode(false); Mode(true); }
    Check(before == Saved() && undo == Undo() && modified == fixture.IsModified, "repeated toggles preserve saved points and undo");
    Mode(false);
    var keys = new KeyEventArgs(Keys.Delete);
    var hooks = (System.Collections.IEnumerable)T("CanvasHook").GetField("Hooks", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    var hook = hooks.Cast<object>().First();
    T("CanvasHook").GetMethod("EditKeys", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hook, new object[] { keys });
    Check(!keys.Handled && before == Saved(), "OFF editing keys leave native handling untouched");
    var foreignWidget = canvas.Widgets.First(w => w.GetType().Name == "StyleWidget" && w.GetType().Assembly == asm);
    Mode(true); canvas.ActiveWidget = foreignWidget; Mode(false);
    Check(ReferenceEquals(canvas.ActiveWidget, foreignWidget), "OFF preserves another active widget");
    canvas.ActiveWidget = null;
    otherWidget.Visible = true;
    Check((bool)mode.GetValue(null) && widget.Visible, "widget visibility uses shared session state");
    otherWidget.Visible = false;
    Check(!(bool)mode.GetValue(null), "widget visibility disables shared state");
    output.AppendLine("TOTAL PASS: " + passed + "; native UI / Relay still require real mouse testing.");
}
finally
{
    Call("CanvasHook", "DetachAll");
    canvas.Document = null;
    canvas.Dispose(); other.Dispose(); fixture?.Dispose(); context.Unload();
}
