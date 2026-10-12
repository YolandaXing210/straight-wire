namespace StraightWire
{
    /// <summary>One editing switch for this plugin session; never persisted.</summary>
    internal static class WireEditMode
    {
        static bool _enabled;

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value) return;
                _enabled = value;
                CanvasHook.CloseRelayChoicesAll();
                if (!value) CanvasHook.CancelEditingAll();
                CanvasHook.RefreshAll();
            }
        }
    }
}
