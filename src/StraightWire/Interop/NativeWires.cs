using System;
using System.Drawing;
using System.Reflection;
using Grasshopper.GUI.Canvas;

namespace StraightWire.Interop
{
    /// <summary>
    /// Grasshopper's own wire pens. GH_Painter.GenerateWirePen is private, but it is the one place
    /// that knows every wire look: item, list (compound), tree (dashed compound), faint, the orange
    /// of empty data and the selection gradient, all from the user's skin colours. Drawing our
    /// polylines with it makes a routed wire look exactly like the native wire it replaces.
    ///
    /// If a future Grasshopper renames it, Available is false and the plugin stays out of the way.
    /// </summary>
    internal static class NativeWires
    {
        delegate Pen PenFactory(GH_Painter painter, PointF a, PointF b, bool asel, bool bsel, GH_WireType type);

        static readonly PenFactory Factory = Bind();

        public static bool Available => Factory != null;

        static PenFactory Bind()
        {
            try
            {
                var method = typeof(GH_Painter).GetMethod("GenerateWirePen",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null,
                    new[] { typeof(PointF), typeof(PointF), typeof(bool), typeof(bool), typeof(GH_WireType) }, null);
                if (method == null || method.ReturnType != typeof(Pen)) return null;
                return (PenFactory)Delegate.CreateDelegate(typeof(PenFactory), method);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// The pen Grasshopper would use for a wire from a to b. The faint pen is one shared
        /// instance Grasshopper keeps, so dispose a pen only when <see cref="IsShared"/> is false.
        /// </summary>
        public static Pen Pen(GH_Painter painter, PointF a, PointF b, bool asel, bool bsel, GH_WireType type) =>
            Factory?.Invoke(painter, a, b, asel, bsel, type);

        public static bool IsShared(GH_WireType type) => type == GH_WireType.faint;

        /// <summary>
        /// Grasshopper builds its faint pen once per painter, from wire_default, the first time it
        /// needs one, and keeps it. If that first time fell inside our transparent window, faint
        /// wires would stay invisible for the rest of the session, plugin or not. Asking for it
        /// while the colours are still real makes sure it is built from them.
        /// </summary>
        public static void WarmFaintPen(GH_Painter painter) =>
            Factory?.Invoke(painter, PointF.Empty, PointF.Empty, false, false, GH_WireType.faint);
    }
}
