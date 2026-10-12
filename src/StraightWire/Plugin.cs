using Grasshopper;
using Grasshopper.GUI.Canvas;
using StraightWire.Interop;
using StraightWire.UI;

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
            WireEditMode.Enabled = false;
            _started = true;
            Instances.CanvasCreated += OnCanvasCreated;
            if (Instances.ActiveCanvas != null) OnCanvasCreated(Instances.ActiveCanvas);
            StyleMenu.AddWhenReady();
        }

        public static void Stop()
        {
            if (!_started) return;
            _started = false;
            Instances.CanvasCreated -= OnCanvasCreated;
            CanvasHook.DetachAll();
            WireEditMode.Enabled = false;
            StyleMenu.Remove();
        }

        static void OnCanvasCreated(GH_Canvas canvas) => Log.Guard("attach", () => CanvasHook.Attach(canvas));
    }
}
