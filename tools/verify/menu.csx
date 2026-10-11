// Lists Grasshopper's menu bar and the Straight Wire menu's items.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
using System.Windows.Forms;
using Grasshopper;

var strip = Instances.DocumentEditor?.MainMenuStrip;
if (strip == null) output.AppendLine("Grasshopper's editor is not open");
else
{
    output.AppendLine("menu bar: " + string.Join(" | ", strip.Items.Cast<ToolStripItem>().Select(i => i.Text)));
    var ours = strip.Items.Cast<ToolStripItem>().OfType<ToolStripMenuItem>().FirstOrDefault(i => i.Text == "Straight Wire");
    if (ours != null)
        output.AppendLine("Straight Wire: " + string.Join(" | ", ours.DropDownItems.Cast<ToolStripItem>().Select(i => i is ToolStripSeparator ? "—" : i.Text)));
}
