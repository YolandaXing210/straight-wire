using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using StraightWire.Core;
using StraightWire.Interop;

namespace StraightWire
{
    /// <summary>
    /// Draws one paint's wires the way Grasshopper would, except along routed polylines. The
    /// rules for type, faint and hidden are Grasshopper's own (GH_Attributes.RenderIncomingWires);
    /// the pens are GH_Painter's, so list, tree, empty, faint and selection all look native.
    /// </summary>
    internal static class WireRenderer
    {
        const float DotRadius = 2.6f;
        const float DotsBelowZoom = 0.5f;

        const float HandlePx = 7;

        public static void Draw(GH_Canvas canvas, SceneRead read, RouteResult routes, WireEditor editor)
        {
            var g = canvas.Graphics;
            var painter = canvas.Painter;
            var visible = canvas.Viewport.VisibleRegion;
            visible.Inflate(10, 10);
            bool fancy = CentralSettings.CanvasFancyWires;
            var dots = new Dictionary<string, DotLook>(StringComparer.Ordinal);

            foreach (var w in read.Wires)
            {
                bool selected = w.SourceSelected || w.TargetSelected;
                // Grasshopper draws its own marker for a hidden wire, in colours we never touch.
                if (w.Display == GH_ParamWireDisplay.hidden && !selected) continue;
                var type = fancy ? GH_Painter.DetermineWireType(w.Source.VolatileData) : GH_WireType.generic;
                if (w.Display == GH_ParamWireDisplay.faint && !selected) type = GH_WireType.faint;

                Pt[] path = null;
                if (w.Routed && editor.Live is (string liveId, Pt[] livePath) && liveId == w.Id) path = livePath;
                if (path == null && (!w.Routed || !routes.Paths.TryGetValue(w.Id, out path)))
                {
                    painter.DrawConnection(w.End, w.Start, GH_WireDirection.left, GH_WireDirection.right,
                                           w.TargetSelected, w.SourceSelected, type);
                    continue;
                }

                Note(dots, w, type);
                if (!Bounds(path).IntersectsWith(visible)) continue;
                bool picked = editor.Selected.Contains(w.Id);
                if (picked && type == GH_WireType.faint) type = fancy ? GH_Painter.DetermineWireType(w.Source.VolatileData) : GH_WireType.generic;
                var pen = picked ? NativeWires.Pen(painter, w.Start, w.End, true, true, type)
                                 : NativeWires.Pen(painter, w.Start, w.End, w.SourceSelected, w.TargetSelected, type);
                if (pen == null) continue;
                var points = ToPoints(path);
                try
                {
                    g.DrawLines(pen, points);
                    if (!picked && w.Id == editor.Hover)
                        using (var glow = new Pen(Color.FromArgb(120, GH_Skin.wire_selected_a), pen.Width + 2) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round })
                            g.DrawLines(glow, points);
                }
                finally
                {
                    if (!NativeWires.IsShared(type)) pen.Dispose();
                }
                if (picked) DrawHandles(g, points, canvas.Viewport.ZoomInverse);
            }

            if (canvas.Viewport.Zoom < DotsBelowZoom) return;
            foreach (var dot in routes.Junctions)
            {
                if (!dots.TryGetValue(dot.Group, out var look) || !look.Visible) continue;
                float x = (float)dot.At.X, y = (float)dot.At.Y;
                if (!visible.Contains(x, y)) continue;
                using (var brush = new SolidBrush(look.Color))
                    g.FillEllipse(brush, x - DotRadius, y - DotRadius, 2 * DotRadius, 2 * DotRadius);
            }
        }

        /// <summary>A junction dot takes its source's wire colour, and shows only if some wire of its group is drawn at full strength.</summary>
        struct DotLook
        {
            public Color Color;
            public bool Visible;
        }

        static void Note(Dictionary<string, DotLook> dots, WireDraw w, GH_WireType type)
        {
            string group = w.Source.InstanceGuid.ToString();
            if (!dots.TryGetValue(group, out var look))
                look.Color = w.Source.VolatileDataCount == 0 ? GH_Skin.wire_empty
                           : w.SourceSelected ? GH_Skin.wire_selected_a
                           : GH_Skin.wire_default;
            look.Visible |= type != GH_WireType.faint;
            dots[group] = look;
        }

        /// <summary>Squares on every bend of a selected wire, a constant size on screen.</summary>
        static void DrawHandles(Graphics g, PointF[] points, float zoomInverse)
        {
            float size = HandlePx * zoomInverse, half = size / 2;
            using (var fill = new SolidBrush(Color.White))
            using (var edge = new Pen(GH_Skin.wire_selected_a, zoomInverse))
                for (int i = 1; i < points.Length - 1; i++)
                {
                    g.FillRectangle(fill, points[i].X - half, points[i].Y - half, size, size);
                    g.DrawRectangle(edge, points[i].X - half, points[i].Y - half, size, size);
                }
        }

        static RectangleF Bounds(Pt[] path)
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (var p in path)
            {
                x0 = Math.Min(x0, p.X);
                y0 = Math.Min(y0, p.Y);
                x1 = Math.Max(x1, p.X);
                y1 = Math.Max(y1, p.Y);
            }
            return RectangleF.FromLTRB((float)x0, (float)y0, (float)x1 + 1, (float)y1 + 1);
        }

        static PointF[] ToPoints(Pt[] path)
        {
            var points = new PointF[path.Length];
            for (int i = 0; i < path.Length; i++) points[i] = new PointF((float)path[i].X, (float)path[i].Y);
            return points;
        }
    }
}
