using System;
using Grasshopper.Kernel;
using StraightWire.Core;

namespace StraightWire
{
    /// <summary>
    /// What this user prefers, for files with no Straight Wire settings of their own. Stored in
    /// Grasshopper's settings folder as StraightWire.xml, never in a .gh file.
    /// </summary>
    internal static class UserSettings
    {
        const string ModeKey = "DefaultMode", SpacingKey = "DefaultSpacing", WidgetKey = "WidgetVisible", EditingKey = "EditingEnabled";

        static GH_SettingsServer _server;

        static GH_SettingsServer Server => _server ?? (_server = new GH_SettingsServer("StraightWire", true));

        public static WireStyle DefaultMode
        {
            get
            {
                try { return StyleNames.TryParseToken(Server.GetValue(ModeKey, "diag"), out var s) ? s : WireStyle.Diagonal; }
                catch { return WireStyle.Diagonal; }
            }
            set => Write(() => Server.SetValue(ModeKey, StyleNames.ToToken(value)));
        }

        public static double DefaultSpacing
        {
            get
            {
                try { return SettingsJson.ClampSpacing(Server.GetValue(SpacingKey, Routing.DefaultSpacing)); }
                catch { return Routing.DefaultSpacing; }
            }
            set => Write(() => Server.SetValue(SpacingKey, SettingsJson.ClampSpacing(value)));
        }

        /// <summary>Whether wires can be edited with the mouse (the editing widget is shown).</summary>
        public static bool EditingEnabled
        {
            get
            {
                try { return Server.GetValue(EditingKey, true); }
                catch { return true; }
            }
            set => Write(() => Server.SetValue(EditingKey, value));
        }

        public static bool WidgetVisible
        {
            get
            {
                try { return Server.GetValue(WidgetKey, true); }
                catch { return true; }
            }
            set => Write(() => Server.SetValue(WidgetKey, value));
        }

        static void Write(Action set)
        {
            try
            {
                set();
                Server.WritePersistentSettings();
            }
            catch (Exception ex)
            {
                Log.Error("settings-write", ex);
            }
        }
    }
}
