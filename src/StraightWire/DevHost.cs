using StraightWire.Interop;

namespace StraightWire
{
    /// <summary>
    /// Entry points for tools/dev-reload.csx, which loads a development build into a running Rhino
    /// without a restart. Grasshopper never sees that build as a plugin, so the marker component
    /// is not registered; everything else behaves as installed. The checks in tools/verify also
    /// call these, on dev builds and on the installed plugin alike.
    /// </summary>
    public static class DevHost
    {
        public static string Attach()
        {
            Plugin.Start();
            return "Straight Wire dev build attached. Wire pens available: " + NativeWires.Available + ".";
        }

        public static string Detach()
        {
            Plugin.Stop();
            return "Straight Wire dev build detached.";
        }
    }
}
