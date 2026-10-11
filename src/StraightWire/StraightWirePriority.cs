using Grasshopper.Kernel;

namespace StraightWire
{
    public class StraightWirePriority : GH_AssemblyPriority
    {
        public override GH_LoadingInstruction PriorityLoad()
        {
            Log.Guard("priority-load", Plugin.Start);
            return GH_LoadingInstruction.Proceed;
        }
    }
}
