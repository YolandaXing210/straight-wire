using Grasshopper;
using Grasshopper.GUI.Canvas;
using StraightWire.Interop;

namespace StraightWire
{
    /// <summary>Starts and stops everything the plugin hooks into Grasshopper. Both are idempotent.</summary>
    internal static class Plugin
    {
        static bool _started;

        public static void Start()
        {
            if (_started) return;
            if (!NativeWires.Available)
            {
                Log.Once("unavailable", "this Grasshopper does not expose the wire pens Straight Wire draws with, so it stays off.");
                return;
            }
            _started = true;
            Instances.CanvasCreated += OnCanvasCreated;
            if (Instances.ActiveCanvas != null) OnCanvasCreated(Instances.ActiveCanvas);
        }

        public static void Stop()
        {
            if (!_started) return;
            _started = false;
            Instances.CanvasCreated -= OnCanvasCreated;
        }

        static void OnCanvasCreated(GH_Canvas canvas)
        {
            // Nothing to attach yet; Task 13 adds the canvas hook.
        }
    }
}
