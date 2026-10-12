// Opens a disposable, independent GH canvas for real mouse testing of the Release build.
// Close the window to dispose its document and unload its test assembly.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Drawing;
using System.Windows.Forms;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Data;

const string slot="StraightWire.RelayUITest";
if(AppDomain.CurrentDomain.GetData(slot)!=null) throw new Exception("Close the existing Relay UI test window first.");
var context=new AssemblyLoadContext("StraightWire.RelayUITest",true);
Assembly asm;
using(var stream=File.OpenRead("/Users/lehands/Documents/GitHub/straight-wire/src/StraightWire/bin/Release/StraightWire.gha"))asm=context.LoadFromStream(stream);
var d=new GH_Document();
var canvas=new GH_Canvas { Dock=DockStyle.Fill };
var form=new Form { Text="Straight Wire — disposable Relay test", ClientSize=new Size(1050,700), StartPosition=FormStartPosition.CenterScreen };
form.Controls.Add(canvas);
var source=new Param_Number { NickName="Source" };source.CreateAttributes();source.Attributes.Pivot=new PointF(100,200);source.Attributes.PerformLayout();
var target=new Param_Number { NickName="Target" };target.CreateAttributes();target.Attributes.Pivot=new PointF(800,450);target.Attributes.PerformLayout();
d.AddObject(source,false);d.AddObject(target,false);source.PersistentData.Append(new GH_Number(42),new GH_Path(0));target.AddSource(source);
d.ValueTable.SetValue("StraightWire.v1","{\"v\":1,\"mode\":\"ortho\",\"spacing\":10,\"wires\":{}}");
canvas.Document=d;canvas.Viewport.Zoom=1;canvas.Viewport.MidPoint=new PointF(450,325);
asm.GetType("StraightWire.CanvasHook").GetMethod("Attach").Invoke(null,new object[]{canvas});
var mode=asm.GetType("StraightWire.WireEditMode").GetProperty("Enabled");
var menu=new MenuStrip();var edit=new ToolStripMenuItem("Wire Edit Mode") { CheckOnClick=true };
edit.CheckedChanged+=(s,e)=>mode.SetValue(null,edit.Checked);menu.Items.Add(edit);form.MainMenuStrip=menu;form.Controls.Add(menu);
AppDomain.CurrentDomain.SetData(slot,new object[]{context,asm,d,canvas,form,source,target});
form.FormClosed+=(s,e)=>{
 asm.GetType("StraightWire.CanvasHook").GetMethod("DetachAll").Invoke(null,null);
 canvas.Document=null;canvas.Dispose();d.Dispose();AppDomain.CurrentDomain.SetData(slot,null);context.Unload();
};
form.Show();d.Enabled=true;d.NewSolution(false);canvas.Refresh();
output.AppendLine("Opened isolated test window. Original active document="+Grasshopper.Instances.ActiveCanvas?.Document?.DisplayName);
