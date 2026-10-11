using System;
using System.Drawing;
using System.Linq;
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

        static readonly FieldInfo FaintPenField = FindFaintPenField();
        static readonly Pen Invisible = new Pen(Color.Transparent, 1.5f);

        static FieldInfo FindFaintPenField()
        {
            try
            {
                return typeof(GH_Painter).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                    .FirstOrDefault(f => f.FieldType == typeof(Pen) && f.Name.Contains("GenerateWirePen_Static_Faint"));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Native faint wires are drawn with that cached pen rather than from the skin colours, so
        /// making the colours transparent does not hide them. While we take over, the cache holds an
        /// invisible pen instead. Returns the pen to put back, or null if the swap was not possible
        /// (then faint wires show twice, which is the only cost). Call after <see cref="WarmFaintPen"/>.
        /// </summary>
        public static Pen HideFaint(GH_Painter painter)
        {
            if (FaintPenField == null || painter == null) return null;
            try
            {
                if (!(FaintPenField.GetValue(painter) is Pen pen) || pen == Invisible) return null;
                FaintPenField.SetValue(painter, Invisible);
                return pen;
            }
            catch
            {
                return null;
            }
        }

        public static void RestoreFaint(GH_Painter painter, Pen pen)
        {
            if (FaintPenField == null || painter == null || pen == null) return;
            try { FaintPenField.SetValue(painter, pen); }
            catch { }
        }
    }
}
