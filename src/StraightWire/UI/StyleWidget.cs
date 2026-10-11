using System.Drawing;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.GUI.Widgets;
using Grasshopper.Kernel;
using StraightWire.Core;

namespace StraightWire.UI
{
    /// <summary>
    /// Three small buttons in the canvas's bottom-left corner (Curve / Ortho / 45°) with the scope
    /// beside them: All, or how many selected wires a click would change. Like Grasshopper's own
    /// widgets, it can be hidden from the canvas widget menu.
    /// </summary>
    internal sealed class StyleWidget : GH_Widget
    {
        static readonly WireStyle[] Order = { WireStyle.Curve, WireStyle.Orthogonal, WireStyle.Diagonal };
        const int Margin = 12, ButtonWidth = 52, ButtonHeight = 22, Gap = 2;

        public override bool Visible
        {
            get => UserSettings.WidgetVisible;
            set => UserSettings.WidgetVisible = value;
        }

        public override string Name => "Straight Wire";

        public override string Description =>
            "Switch between curved, right-angle and 45° wires, for all wires or for the wires on selected components.";

        public override Bitmap Icon_24x24 => null;

        static Rectangle ButtonRect(GH_Canvas canvas, int i) =>
            new Rectangle(Margin + i * (ButtonWidth + Gap), canvas.ClientSize.Height - Margin - ButtonHeight, ButtonWidth, ButtonHeight);

        static int HitButton(GH_Canvas canvas, Point control)
        {
            for (int i = 0; i < Order.Length; i++)
                if (ButtonRect(canvas, i).Contains(control)) return i;
            return -1;
        }

        public override bool Contains(Point pt_control, PointF pt_canvas) => Owner != null && HitButton(Owner, pt_control) >= 0;

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button != MouseButtons.Left) return GH_ObjectResponse.Ignore;
            int i = HitButton(sender, e.ControlLocation);
            if (i < 0) return GH_ObjectResponse.Ignore;
            Commands.SetStyle(Order[i]);
            return GH_ObjectResponse.Handled;
        }

        public override void Render(GH_Canvas canvas)
        {
            var doc = canvas.Document;
            if (doc == null) return;
            var scope = Commands.ScopeOf(doc);
            var g = canvas.Graphics;
            var saved = g.Transform;
            try
            {
                g.ResetTransform();   // widgets draw in control (screen) coordinates
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var edge = new Pen(Color.FromArgb(160, 0, 0, 0)))
                using (var offFill = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
                using (var onFill = new SolidBrush(Color.FromArgb(255, 50, 50, 50)))
                using (var offText = new SolidBrush(Color.FromArgb(255, 60, 60, 60)))
                using (var onText = new SolidBrush(Color.White))
                {
                    for (int i = 0; i < Order.Length; i++)
                    {
                        var r = ButtonRect(canvas, i);
                        bool on = scope.Active == Order[i];
                        g.FillRectangle(on ? onFill : offFill, r);
                        g.DrawRectangle(edge, r);
                        g.DrawString(StyleNames.ShortLabel(Order[i]), GH_FontServer.StandardAdjusted, on ? onText : offText, r, format);
                    }
                    var last = ButtonRect(canvas, Order.Length - 1);
                    format.Alignment = StringAlignment.Near;
                    g.DrawString(scope.Label, GH_FontServer.StandardAdjusted, offText,
                                 new RectangleF(last.Right + 8, last.Top, 120, last.Height), format);
                }
            }
            finally
            {
                g.Transform = saved;
            }
        }
    }
}
