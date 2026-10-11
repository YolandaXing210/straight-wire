using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Grasshopper.GUI.Canvas;
using StraightWire.Core;
using StraightWire.Interop;

namespace StraightWire
{
    /// <summary>
    /// Takes over wire drawing on one canvas.
    ///
    /// Grasshopper draws every wire between CanvasPrePaintWires and CanvasPostPaintWires, with pens
    /// built from four GH_Skin colours. Setting those colours to transparent for that window hides
    /// the native wires without touching any document or setting. PostPaintWires puts them back
    /// first (our pens are built from them too) and then draws the routed wires; CanvasPaintEnd
    /// puts them back again if PostPaintWires never ran. Anything that throws sends this canvas
    /// back to Grasshopper's own wires until the user picks a style again.
    /// </summary>
    internal sealed class CanvasHook
    {
        static readonly List<CanvasHook> Hooks = new List<CanvasHook>();

        /// <summary>Set through DevHost to prove the fallback: the next takeover throws.</summary>
        internal static bool ThrowOnNextRoute;

        /// <summary>How long the last Router.Route call took, for DevHost.Describe.</summary>
        internal static double LastRouteMilliseconds;

        readonly GH_Canvas _canvas;
        bool _takenOver, _failed;
        Color _default, _empty, _selectedA, _selectedB;
        GH_Painter _painter;
        Pen _faint;
        SceneRead _read;
        RouteResult _routes;

        CanvasHook(GH_Canvas canvas)
        {
            _canvas = canvas;
        }

        public static int Count => Hooks.Count;

        public static void Attach(GH_Canvas canvas)
        {
            if (canvas == null || Hooks.Exists(h => h._canvas == canvas)) return;
            var hook = new CanvasHook(canvas);
            canvas.CanvasPrePaintWires += hook.OnPrePaintWires;
            canvas.CanvasPostPaintWires += hook.OnPostPaintWires;
            canvas.CanvasPaintEnd += hook.OnPaintEnd;
            canvas.Disposed += hook.OnDisposed;
            Hooks.Add(hook);
            canvas.Refresh();
        }

        public static void DetachAll()
        {
            foreach (var hook in Hooks.ToArray()) hook.Detach(true);
        }

        /// <summary>Lets every canvas try again after a failure. Called whenever the user changes a style.</summary>
        public static void ResetFailures()
        {
            foreach (var hook in Hooks) hook._failed = false;
        }

        public static void RefreshAll()
        {
            foreach (var hook in Hooks) hook._canvas.Refresh();
        }

        void OnDisposed(object sender, EventArgs e) => Detach(false);

        void Detach(bool refresh)
        {
            if (_takenOver) Restore();
            _canvas.CanvasPrePaintWires -= OnPrePaintWires;
            _canvas.CanvasPostPaintWires -= OnPostPaintWires;
            _canvas.CanvasPaintEnd -= OnPaintEnd;
            _canvas.Disposed -= OnDisposed;
            Hooks.Remove(this);
            if (refresh) _canvas.Refresh();
        }

        void OnPrePaintWires(GH_Canvas canvas)
        {
            _read = null;
            if (_failed || _takenOver) return;
            try
            {
                var doc = canvas.Document;
                if (doc == null) return;
                if (ThrowOnNextRoute)
                {
                    ThrowOnNextRoute = false;
                    throw new InvalidOperationException("forced failure from DevHost");
                }
                var state = DocStore.Get(doc);
                var read = SceneReader.Read(doc, state.Effective);
                if (read.RoutedCount == 0) return;   // all curves: Grasshopper draws everything
                long fingerprint = read.Scene.Fingerprint();
                if (state.Routes == null || state.Fingerprint != fingerprint)
                {
                    var watch = Stopwatch.StartNew();
                    state.Routes = Router.Route(read.Scene, state.RouterState);
                    LastRouteMilliseconds = watch.Elapsed.TotalMilliseconds;
                    state.Fingerprint = fingerprint;
                }
                NativeWires.WarmFaintPen(canvas.Painter);
                _read = read;
                _routes = state.Routes;
                Hide();
            }
            catch (Exception ex)
            {
                Fail("routing", ex);
            }
        }

        void OnPostPaintWires(GH_Canvas canvas)
        {
            if (!_takenOver) return;
            Restore();
            try
            {
                WireRenderer.Draw(canvas, _read, _routes);
            }
            catch (Exception ex)
            {
                Fail("drawing", ex);
            }
            finally
            {
                _read = null;
            }
        }

        void OnPaintEnd(GH_Canvas canvas)
        {
            if (_takenOver) Restore();
        }

        void Hide()
        {
            _default = GH_Skin.wire_default;
            _empty = GH_Skin.wire_empty;
            _selectedA = GH_Skin.wire_selected_a;
            _selectedB = GH_Skin.wire_selected_b;
            GH_Skin.wire_default = Color.Transparent;
            GH_Skin.wire_empty = Color.Transparent;
            GH_Skin.wire_selected_a = Color.Transparent;
            GH_Skin.wire_selected_b = Color.Transparent;
            _painter = _canvas.Painter;
            _faint = NativeWires.HideFaint(_painter);
            _takenOver = true;
        }

        void Restore()
        {
            GH_Skin.wire_default = _default;
            GH_Skin.wire_empty = _empty;
            GH_Skin.wire_selected_a = _selectedA;
            GH_Skin.wire_selected_b = _selectedB;
            NativeWires.RestoreFaint(_painter, _faint);
            _faint = null;
            _takenOver = false;
        }

        void Fail(string what, Exception ex)
        {
            _failed = true;
            if (_takenOver) Restore();
            Log.Once("paint-" + what, what + " failed (" + ex.Message + "). This canvas is back to Grasshopper's own wires; " +
                     "pick a wire style to try again.");
            try { _canvas.Invalidate(); }
            catch { }
        }
    }
}
