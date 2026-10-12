// Phase 3, task 3: Tidy All on the fixture at spacing 8 (fractional column gaps) with a group
// block (a slider and the component it feeds, a hand-drawn wire between them) and a lane edit
// between loose components; then Undo, Redo, a second tidy, a tidy during a drag, and Tidy
// Selected. Renders the tidied fixture to artifacts/verify/tidy.png.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Drawing;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

string Repo = "/Users/yolandaxing/Documents/GitHub/straight-wire";
Assembly Plugin() =>
    (AppDomain.CurrentDomain.GetData("StraightWire.DevContext") as AssemblyLoadContext)?.Assemblies.FirstOrDefault(a => a.GetName().Name == "StraightWire")
    ?? AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "StraightWire");
string Run(string command) => (string)Plugin().GetType("StraightWire.DevHost").GetMethod("Run").Invoke(null, new object[] { command });
object Call(string type, string method, params object[] args) =>
    Plugin().GetType(type).GetMethod(method, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, args);
object Pts(params (double X, double Y)[] points)
{
    var pt = Plugin().GetType("StraightWire.Core.Pt");
    var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(pt));
    foreach (var p in points) list.Add(Activator.CreateInstance(pt, p.X, p.Y));
    return list;
}

var canvas = Instances.ActiveCanvas;
var original = canvas.Document;
GH_Document doc = null;

GH_Document Open()
{
    var io = new GH_DocumentIO();
    io.Open(Repo + "/tests/fixtures/twisted-tower.gh");
    var d = io.Document;
    d.ValueTable.SetValue("StraightWire.v1", "{\"v\":1,\"mode\":\"diag\",\"spacing\":8,\"wires\":{}}");
    canvas.Document = d;
    d.Enabled = true;
    d.NewSolution(false);
    return d;
}
void Paint(string png = null)
{
    var box = doc.BoundingBox(false);
    var vp = new GH_Viewport(canvas.Viewport) { Width = 1600, Height = 900, Zoom = 1f };
    vp.MidPoint = new PointF(box.X + box.Width / 2, box.Y + box.Height / 2);
    using (var bmp = canvas.GenerateHiResImageTile(vp, Color.White))
        if (png != null) bmp.Save(png, System.Drawing.Imaging.ImageFormat.Png);
}
Dictionary<Guid, PointF> Pivots() => doc.Objects.ToDictionary(o => o.InstanceGuid, o => o.Attributes.Pivot);
string Settings() => doc.ValueTable.GetValue("StraightWire.v1", "");
List<(IGH_Param S, IGH_Param T)> Connections() =>
    doc.Objects.SelectMany(o => o is IGH_Component c ? c.Params.Input : o is IGH_Param p ? new List<IGH_Param> { p } : new List<IGH_Param>())
       .SelectMany(t => t.Sources.Select(s => (s, t))).ToList();
IGH_DocumentObject Top(IGH_Param p) => p.Attributes.GetTopLevel.DocObject;
string WireId(IGH_Param s, IGH_Param t) => s.InstanceGuid + ">" + t.InstanceGuid;
int Moved(Dictionary<Guid, PointF> a, Dictionary<Guid, PointF> b) =>
    a.Count(kv => b.TryGetValue(kv.Key, out var p) && (Math.Abs(p.X - kv.Value.X) > 0.01 || Math.Abs(p.Y - kv.Value.Y) > 0.01));
JsonElement? Entry(string section, string id)
{
    var root = JsonDocument.Parse(Settings()).RootElement;
    return root.TryGetProperty(section, out var s) && s.TryGetProperty(id, out var e) ? e : (JsonElement?)null;
}

try
{
    doc = Open();
    Paint();

    // A group around a slider and the component it feeds (a fractional pivot next to a whole one),
    // with a hand-drawn wire inside it.
    var inner = Connections().FirstOrDefault(c => Top(c.S) is GH_NumberSlider && Top(c.T) is IGH_Component);
    if (inner.S == null) inner = Connections().First(c => Top(c.S) is IGH_Component && Top(c.T) is IGH_Component && Top(c.S) != Top(c.T));
    output.AppendLine($"group: {Top(inner.S).GetType().Name} at {Top(inner.S).Attributes.Pivot} + {Top(inner.T).GetType().Name}");
    IGH_DocumentObject ga = Top(inner.S), gb = Top(inner.T);
    var group = new GH_Group();
    group.CreateAttributes();
    group.AddObject(ga.InstanceGuid);
    group.AddObject(gb.InstanceGuid);
    doc.AddObject(group, false);
    group.Attributes.ExpireLayout();
    group.Attributes.PerformLayout();
    var so = inner.S.Attributes.OutputGrip;
    var ti = inner.T.Attributes.InputGrip;
    double midX = Math.Round((so.X + ti.X) / 2);
    string innerId = WireId(inner.S, inner.T);
    Call("StraightWire.Commands", "SetPoints", doc, innerId, Pts((midX, so.Y), (midX, ti.Y)), "Move Wire Point");

    // A lane on a wire between two loose components.
    var grouped = new HashSet<IGH_DocumentObject> { ga, gb };
    var loose = Connections().First(c => !grouped.Contains(Top(c.S)) && !grouped.Contains(Top(c.T)) && Top(c.S) != Top(c.T)
                                          && Top(c.S) is IGH_Component && Top(c.T) is IGH_Component);
    string laneId = WireId(loose.S, loose.T);
    Call("StraightWire.Commands", "SetLane", doc, laneId, 0.3);
    Paint();

    var before = Pivots();
    string settingsBefore = Settings();
    var gaBefore = ga.Attributes.Pivot;
    var pointsBefore = Entry("points", innerId).Value.GetProperty("p").EnumerateArray().Select(e => e.GetDouble()).ToArray();
    int undoCount = doc.UndoServer.UndoCount;

    output.AppendLine("tidy: " + Run("tidy"));
    Paint(Repo + "/artifacts/verify/tidy.png");
    var after = Pivots();
    string settingsAfter = Settings();

    // (a) overlaps between blocks: the group, and loose active objects (not the marker).
    var blockRects = new List<RectangleF> { group.Attributes.Bounds };
    var members = new HashSet<Guid>(group.ObjectIDs);
    foreach (var o in doc.Objects)
        if (o is IGH_ActiveObject && !members.Contains(o.InstanceGuid) && o.Name != "Straight Wire") blockRects.Add(o.Attributes.Bounds);
    int overlaps = 0;
    for (int i = 0; i < blockRects.Count; i++)
        for (int j = i + 1; j < blockRects.Count; j++)
        {
            var r = blockRects[i];
            r.Inflate(-0.5f, -0.5f);
            if (r.IntersectsWith(blockRects[j])) overlaps++;
        }
    // (b) links running backwards, (c) straight links.
    RectangleF BlockOf(IGH_DocumentObject o) => members.Contains(o.InstanceGuid) ? group.Attributes.Bounds : o.Attributes.Bounds;
    var links = Connections().Where(c => BlockOf(Top(c.S)) != BlockOf(Top(c.T))).ToList();
    int backwards = links.Count(c => BlockOf(Top(c.S)).Right > BlockOf(Top(c.T)).Left + 0.5);
    int straight = links.Count(c => Math.Abs(c.S.Attributes.OutputGrip.Y - c.T.Attributes.InputGrip.Y) < 0.5);
    output.AppendLine($"(a) overlaps {overlaps}; (b) backwards links {backwards} of {links.Count}; (c) straight links {straight} of {links.Count}; objects moved {Moved(before, after)}");

    // (d) the grouped wire's points moved with the group; (e) the lane is gone; (f) undo name; (g) group kept its shape.
    var shift = new SizeF(ga.Attributes.Pivot.X - gaBefore.X, ga.Attributes.Pivot.Y - gaBefore.Y);
    var pointsAfter = Entry("points", innerId)?.GetProperty("p").EnumerateArray().Select(e => e.GetDouble()).ToArray();
    bool followed = pointsAfter != null && pointsAfter.Length == pointsBefore.Length &&
                    pointsAfter.Select((v, i) => Math.Abs(v - pointsBefore[i] - (i % 2 == 0 ? shift.Width : shift.Height))).Max() < 0.01;
    output.AppendLine($"(d) grouped wire points followed the group by ({shift.Width:0.#},{shift.Height:0.#}): {followed}");
    output.AppendLine($"(e) lane entry gone: {Entry("lanes", laneId) == null}");
    output.AppendLine($"(f) undo name: {doc.UndoServer.FirstUndoName}, records +{doc.UndoServer.UndoCount - undoCount}");
    var offsetBefore = new SizeF(before[gb.InstanceGuid].X - before[ga.InstanceGuid].X, before[gb.InstanceGuid].Y - before[ga.InstanceGuid].Y);
    var offsetAfter = new SizeF(gb.Attributes.Pivot.X - ga.Attributes.Pivot.X, gb.Attributes.Pivot.Y - ga.Attributes.Pivot.Y);
    output.AppendLine($"(g) group kept its shape: {offsetBefore == offsetAfter}");

    // Undo and Redo.
    doc.Undo();
    output.AppendLine($"undo: pivots back {Moved(before, Pivots()) == 0}, settings back {Settings() == settingsBefore}");
    Paint();
    output.AppendLine($"undo then paint: pivots back {Moved(before, Pivots()) == 0}, settings back {Settings() == settingsBefore}");
    doc.Redo();
    output.AppendLine($"redo: pivots as tidied {Moved(after, Pivots()) == 0}, settings as tidied {Settings() == settingsAfter}");
    Paint();

    // A second tidy, and one during a drag.
    int records = doc.UndoServer.UndoCount;
    output.AppendLine($"second tidy: {Run("tidy")}, records +{doc.UndoServer.UndoCount - records}");
    doc.Undo();
    Paint();
    Run("drag:on");
    var held = Pivots();
    output.AppendLine($"tidy while dragging: {Run("tidy")}, moved {Moved(held, Pivots())}");
    Run("drag:off");

    // Tidy Selected on a fresh copy: three linked components.
    doc = Open();
    Paint();
    var chain = Connections().Where(c => Top(c.S) is IGH_Component && Top(c.T) is IGH_Component && Top(c.S) != Top(c.T)).ToList();
    var first = chain.First(c => chain.Any(d => Top(d.S) == Top(c.T) && Top(d.T) != Top(c.S)));
    var second = chain.First(d => Top(d.S) == Top(first.T) && Top(d.T) != Top(first.S));
    var three = new[] { Top(first.S), Top(first.T), Top(second.T) };
    foreach (var o in three) o.Attributes.Selected = true;
    var selectedBefore = Pivots();
    output.AppendLine($"label: {Call("StraightWire.Tidier", "Label", canvas)}");
    output.AppendLine($"tidy selected: {Run("tidy")}");
    var changed = selectedBefore.Where(kv => Math.Abs(Pivots()[kv.Key].X - kv.Value.X) > 0.01 || Math.Abs(Pivots()[kv.Key].Y - kv.Value.Y) > 0.01).Select(kv => kv.Key).ToList();
    output.AppendLine($"moved {changed.Count}, all among the three: {changed.All(id => three.Any(o => o.InstanceGuid == id))}");
}
finally
{
    canvas.Document = original;
    canvas.Refresh();
}
