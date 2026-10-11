// Builds tests/fixtures/twisted-tower.gh: the prototype's example definition, made of real
// Grasshopper components, used by the checks in tools/verify. Send to execute_rhinocommon_csharp_code.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Drawing;
using System.IO;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

string FixturePath = "/Users/yolandaxing/Documents/GitHub/straight-wire/tests/fixtures/twisted-tower.gh";
var doc = new GH_Document();
var made = new Dictionary<string, IGH_DocumentObject>();

IGH_DocumentObject Add(string id, string guid, float x, float y)
{
    var obj = Instances.ComponentServer.EmitObjectProxy(new Guid(guid)).CreateInstance();
    obj.CreateAttributes();
    obj.Attributes.Pivot = new PointF(x, y);
    doc.AddObject(obj, false);
    made[id] = obj;
    return obj;
}

void Slider(string id, string name, decimal min, decimal max, decimal value, int decimals, float x, float y)
{
    var s = (GH_NumberSlider)Add(id, "57da07bd-ecab-415d-9d86-af36d7073abc", x, y);
    s.NickName = name;
    s.Slider.DecimalPlaces = decimals;
    s.Slider.Minimum = min;
    s.Slider.Maximum = max;
    s.Slider.Value = value;
}

IGH_Param Out(string id, int i) => made[id] is IGH_Component c ? c.Params.Output[i] : (IGH_Param)made[id];
IGH_Param In(string id, int i) => made[id] is IGH_Component c ? c.Params.Input[i] : (IGH_Param)made[id];
void Wire(string a, int ai, string b, int bi) => In(b, bi).AddSource(Out(a, ai));

Slider("floors", "Floors", 2, 60, 24, 0, 40, 60);
Slider("fh", "Floor H", 2.5m, 5, 3.2m, 2, 70, 150);
Slider("radius", "Radius", 4, 30, 12, 1, 20, 410);
Slider("sides", "Sides", 3, 12, 6, 0, 80, 480);
Slider("twist", "Twist", 0, 0.1m, 0.02m, 3, 330, 575);
Slider("div", "Divisions", 4, 40, 16, 0, 560, 20);
Add("series", "e64c5fb1-845c-4ab1-8911-5f338516ba67", 330, 80);
Add("unitz", "9103c240-a6a9-4223-9b42-dbd19bf38e2b", 500, 250);
Add("poly", "845527a6-5cea-4ae9-a667-96ae1667a4e8", 300, 330);
Add("mult", "ce46b74e-00c9-43c4-805a-193b69ea4a11", 590, 420);
Add("move", "e9eb1dcf-92f6-4d4d-84ae-96222d60f56b", 660, 190);
Add("rot", "b7798b74-037e-4f0c-8ac7-dc1043d093e0", 790, 330);
Add("loft", "a7a41d0a-2188-4f7a-82cc-1a2c4e4ec850", 960, 200);
Add("divc", "2162e72e-72fc-4bf8-9459-d4d82fa8aa14", 900, 450);
Add("flip", "41aa4112-9c9b-42f4-847e-503b9d90e4c7", 1080, 480);
Add("intc", "2b2a4145-3dff-41d4-a8de-1ea9d29eef33", 960, 600);
Add("area", "86b28a7e-94d9-4791-8306-e13e10d5f8d5", 1110, 150);
Add("panel", "59e0b89a-e487-49f8-bab8-b5bab16be14c", 1250, 110);

Wire("floors", 0, "series", 2); Wire("fh", 0, "series", 1);
Wire("series", 0, "unitz", 0); Wire("series", 0, "mult", 0); Wire("twist", 0, "mult", 1);
Wire("radius", 0, "poly", 1); Wire("sides", 0, "poly", 2);
Wire("poly", 0, "move", 0); Wire("unitz", 0, "move", 1);
Wire("move", 0, "rot", 0); Wire("mult", 0, "rot", 1);
Wire("rot", 0, "loft", 0); Wire("rot", 0, "divc", 0); Wire("div", 0, "divc", 1);
Wire("divc", 0, "flip", 0); Wire("flip", 0, "intc", 0);
Wire("loft", 0, "area", 0); Wire("area", 0, "panel", 0);

foreach (var o in doc.Objects) { o.Attributes.ExpireLayout(); o.Attributes.PerformLayout(); }
Directory.CreateDirectory(Path.GetDirectoryName(FixturePath));
output.AppendLine(new GH_DocumentIO(doc).SaveQuiet(FixturePath)
    ? "saved " + FixturePath + " with " + doc.ObjectCount + " objects"
    : "save failed");
