// Release-build integration checks on isolated documents/canvases. Never replaces the user's plugin.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Drawing;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;

string repo = "/Users/lehands/Documents/GitHub/straight-wire";
// Native popup testing must be a separate run on a hosted canvas. An unparented
// canvas may be an invalid macOS popup owner; native failures escape managed catches.
const bool testAmbiguousMenu = false;
string tracePath = Path.Combine(Path.GetTempPath(), "StraightWire-relay-verification.log");
void Trace(string message) => File.AppendAllText(tracePath, DateTime.Now.ToString("O") + " " + message + Environment.NewLine);
Trace("START; ambiguity=" + testAmbiguousMenu);
var context = new AssemblyLoadContext("StraightWire.RelayVerification", true);
Assembly asm;
using (var stream = File.OpenRead(repo + "/src/StraightWire/bin/Release/StraightWire.gha")) asm = context.LoadFromStream(stream);
var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
Type T(string name) => asm.GetType("StraightWire." + name, true);
object Call(string type, string method, params object[] args) => T(type).GetMethod(method, flags).Invoke(null, args);
object F(object obj,string field) => obj.GetType().GetField(field,flags).GetValue(obj);
object Invoke(object obj,string method,params object[] args) => obj.GetType().GetMethod(method,flags).Invoke(obj,args);
void Mode(bool enabled) => T("WireEditMode").GetProperty("Enabled").SetValue(null,enabled);
int passed = 0;
void Check(bool ok,string label) { Trace((ok ? "PASS " : "FAIL ") + label); if(!ok) throw new Exception("FAIL "+label); passed++; output.AppendLine("PASS "+label); }
PointF P(object pt) => new PointF((float)(double)pt.GetType().GetProperty("X").GetValue(pt),(float)(double)pt.GetType().GetProperty("Y").GetValue(pt));
try
{
foreach(string scenario in testAmbiguousMenu ? new[]{"ambiguous"} : new[]{"bezier","ortho","diag","custom","lane","ortho-horizontal","diag-horizontal","ortho-zoomout","diag-zoomin"})
{
    string style=scenario.Split('-')[0];
{
    var d = new GH_Document();
    var canvas = new GH_Canvas();
    Form host = null;
    try
    {
        Trace("BEGIN " + scenario);
        if (testAmbiguousMenu)
        {
            host = new Form { Text = "Straight Wire — isolated menu verification", ClientSize = new Size(1100,700) };
            canvas.Dock = DockStyle.Fill;
            host.Controls.Add(canvas);
            host.Show();
            Trace("Hosted canvas shown");
        }
        Param_Number Add(string name,float x,float y)
        {
            var p = new Param_Number { NickName = name };
            p.CreateAttributes(); p.Attributes.Pivot = new PointF(x,y); p.Attributes.PerformLayout();
            d.AddObject(p,false); return p;
        }
        var source = Add("A",50,50);
        var extra = Add("B",50,350);
        var target = Add("T",650,250);
        var branch = Add("U",650,-150);
        source.PersistentData.Append(new GH_Number(7),new GH_Path(0,2));
        source.PersistentData.Append(new GH_Number(8),new GH_Path(0,2));
        source.PersistentData.Append(new GH_Number(9),new GH_Path(3));
        extra.PersistentData.Append(new GH_Number(99),new GH_Path(5));
        target.AddSource(extra); target.AddSource(source); branch.AddSource(source);
        string id = source.InstanceGuid+">"+target.InstanceGuid;
        if(style=="ambiguous") { branch.Attributes.Pivot=target.Attributes.Pivot; branch.Attributes.PerformLayout(); }
        string mode = style == "ambiguous" ? "bezier" : style == "custom" || style == "lane" ? "ortho" : style;
        string edits = "";
        string extraStyle = scenario.EndsWith("horizontal") ? $",\"{extra.InstanceGuid}>{target.InstanceGuid}\":\"bezier\"" : "";
        if(style=="custom")
        {
            var a=source.Attributes.Bounds;var b=target.Attributes.Bounds;
            edits = $",\"points\":{{\"{id}\":{{\"at\":[{a.X},{a.Y},{b.X},{b.Y}],\"p\":[180,50,180,180,400,180,400,250]}}}}";
        }
        if(style=="lane") edits = $",\"lanes\":{{\"{id}\":0.35}}";
        d.ValueTable.SetValue("StraightWire.v1",$"{{\"v\":1,\"mode\":\"{mode}\",\"spacing\":10,\"wires\":{{\"{id}\":\"{mode}\"{extraStyle}}}{edits}}}");
        canvas.Document=d; canvas.Size=new Size(1100,700); canvas.Viewport.Zoom=scenario.EndsWith("zoomout") ? 0.5f : scenario.EndsWith("zoomin") ? 2f : 1f;
        Call("CanvasHook","Attach",canvas);
        var widget=canvas.Widgets.First(w=>w.GetType().Assembly==asm && w.GetType().Name=="RelayWidget");
        void Paint()
        {
            var vp=new GH_Viewport(canvas.Viewport){Width=1100,Height=700,Zoom=canvas.Viewport.Zoom};
            vp.MidPoint=new PointF(350,100);
            using(var bmp=canvas.GenerateHiResImageTile(vp,Color.White)){}
        }
        d.Enabled=true; d.NewSolution(false); Paint();
        string Data(IGH_Param param) => string.Join("|", param.VolatileData.Paths.Select((path,i)=>path+":"+string.Join(",",param.VolatileData.get_Branch(i).Cast<object>().Select(x=>x?.ToString()??"null"))));
        var data=Data(target);var branchData=Data(branch);
        object snapshot=F(widget,"_snapshot");
        Check(snapshot!=null,scenario+" paint publishes snapshot");
        var entries=((System.Collections.IEnumerable)F(snapshot,"Entries")).Cast<object>().ToList();
        var entry=entries.First(x=>(string)F(F(x,"Wire"),"Id")==id);
        var path=((Array)F(entry,"Path")).Cast<object>().Select(P).ToArray();
        PointF point=PointF.Empty;object chosen=null;
        // Pick an unambiguous visible segment, away from native grips and objects.
        for(int i=0;i<path.Length-1;i++)
        {
            if(scenario.EndsWith("horizontal") && Math.Abs(path[i].Y-path[i+1].Y)>0.01)continue;
            foreach(float fraction in new[]{0.25f,0.5f,0.75f})
            {
            var p=new PointF(path[i].X+(path[i+1].X-path[i].X)*fraction,path[i].Y+(path[i+1].Y-path[i].Y)*fraction);
            if(p.X<120||p.X>580)continue;
            var hs=((System.Collections.IEnumerable)Invoke(snapshot,"Hits",p,6.0)).Cast<object>().ToList();
            if((hs.Count==1 || style=="ambiguous" && hs.Count>1) && hs.Any(h=>ReferenceEquals(F(h,"Entry"),entry))) {point=p;chosen=hs.First(h=>ReferenceEquals(F(h,"Entry"),entry));break;}
        }
        }
        Check(chosen!=null,scenario+" has unique visible hit");
        Check((bool)Invoke(snapshot,"IsCurrent",canvas),scenario+" snapshot is current");
        GH_CanvasMouseEvent Ev(PointF p) => new GH_CanvasMouseEvent(Point.Round(canvas.Viewport.ProjectPoint(p)),p,MouseButtons.Left,2,0);
        Check(widget.Contains(Point.Round(canvas.Viewport.ProjectPoint(point)),point),scenario+" widget contains visible segment");
        Check(widget.RespondToMouseDown(canvas,Ev(point))==GH_ObjectResponse.Ignore && widget.RespondToMouseMove(canvas,Ev(point))==GH_ObjectResponse.Ignore && widget.RespondToMouseUp(canvas,Ev(point))==GH_ObjectResponse.Ignore,scenario+" single gestures ignored");
        string before=d.ValueTable.GetValue("StraightWire.v1",null);int undo=d.UndoServer.UndoCount;
        Trace("Double click " + scenario);
        Check(widget.RespondToMouseDoubleClick(canvas,Ev(point))==GH_ObjectResponse.Handled,scenario+" double click consumed");
        if(style=="ambiguous")
        {
            Check(!d.Objects.OfType<GH_Relay>().Any(), "ambiguous click does not arbitrarily insert");
            var menu=(ContextMenuStrip)F(widget,"_choices");
            Check(menu!=null && menu.Items.Count>=3, "ambiguous click presents individual connections");
            Trace("Menu cancellation");
            menu.Close();
            Check(d.UndoServer.UndoCount==undo && d.ValueTable.GetValue("StraightWire.v1",null)==before, "cancel leaves graph and settings unchanged");
            widget.RespondToMouseDoubleClick(canvas,Ev(point));
            menu=(ContextMenuStrip)F(widget,"_choices");
            Trace("Menu selection");
            menu.Items.OfType<ToolStripMenuItem>().Single(item=>Equals(item.Tag,id)).PerformClick();
        }
        var relay=d.Objects.OfType<GH_Relay>().SingleOrDefault();
        Check(relay!=null,scenario+" exactly one native Relay");
        output.AppendLine("Relay requested="+point+" actual="+relay.Attributes.Pivot);
        Check(Math.Abs(relay.Attributes.Pivot.X-point.X)<=1 && Math.Abs(relay.Attributes.Pivot.Y-point.Y)<=1,scenario+" Relay at clicked point within native layout rounding");
        Check(target.Sources.Count==2 && ReferenceEquals(target.Sources[0],extra) && ReferenceEquals(target.Sources[1],relay) && relay.Sources.Count==1 && ReferenceEquals(relay.Sources[0],source),scenario+" connection order retained");
        Check(branch.Sources.Count==1 && ReferenceEquals(branch.Sources[0],source),scenario+" fan-out branch untouched");
        Check(Data(target)==data && Data(branch)==branchData,scenario+" data tree and values retained");
        Check(d.UndoServer.UndoCount==undo+1,scenario+" single Undo record");
        string after=d.ValueTable.GetValue("StraightWire.v1",null);
        Check(!after.Contains("\""+id+"\""),scenario+" old settings id migrated");
        var relayId=relay.InstanceGuid;
        d.Undo(); d.NewSolution(false); Paint();
        Check(!d.Objects.OfType<GH_Relay>().Any() && target.Sources.SequenceEqual(new IGH_Param[]{extra,source}),scenario+" Undo restores graph");
        Check(d.ValueTable.GetValue("StraightWire.v1",null)==before,scenario+" Undo restores exact settings");
        Check(Data(target)==data,scenario+" Undo retains data");
        d.Redo(); d.NewSolution(false); Paint();
        Check(d.Objects.OfType<GH_Relay>().Single().InstanceGuid==relayId && target.Sources[1].InstanceGuid==relayId,scenario+" Redo restores same relay and connections");
        Check(d.ValueTable.GetValue("StraightWire.v1",null)==after && Data(target)==data,scenario+" Redo restores settings and data");
        Check(!(bool)Call("RelayInsertion","Insert",canvas,snapshot,chosen,point),scenario+" stale event cannot insert duplicate");
        Mode(true);
        Check(!widget.Visible && widget.RespondToMouseDoubleClick(canvas,Ev(point))==GH_ObjectResponse.Ignore,scenario+" Edit Mode disables relay bridge");
        Mode(false);
    }
    finally { Trace("Cleanup " + scenario); Call("CanvasHook","DetachAll");canvas.Document=null;canvas.Dispose();host?.Dispose();d.Dispose(); }
}
}
output.AppendLine("TOTAL PASS "+passed+". Real mouse dispatch, ambiguity menu and ghost-path search need UI verification.");
Trace("COMPLETE " + passed);
}
finally { Mode(false); Call("CanvasHook","DetachAll");context.Unload(); }
