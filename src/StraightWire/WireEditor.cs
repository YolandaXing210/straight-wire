using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Grasshopper.GUI.Canvas;
using StraightWire.Core;

namespace StraightWire
{
    /// <summary>
    /// Editing state for one canvas: which wires are selected or hovered, the drag in progress,
    /// and what was last drawn (so the mouse can be hit-tested against exactly that).
    /// </summary>
    internal sealed class WireEditor
    {
        static readonly ConditionalWeakTable<GH_Canvas, WireEditor> Editors = new ConditionalWeakTable<GH_Canvas, WireEditor>();

        public static WireEditor For(GH_Canvas canvas) => Editors.GetValue(canvas, c => new WireEditor());

        public readonly HashSet<string> Selected = new HashSet<string>(StringComparer.Ordinal);

        public string Hover;

        /// <summary>The lane being dragged, and the canvas x it is held at.</summary>
        public (string Id, double X)? Pin;

        /// <summary>A point or segment drag in progress: the wire and its live path.</summary>
        public (string Id, Pt[] Path)? Live;

        /// <summary>A backwards wire's run being dragged: the runs so far of every wire the drag moves.</summary>
        public Dictionary<string, BackRuns> BackPins;

        /// <summary>The scene and routes of the last paint.</summary>
        public SceneRead LastRead;
        public RouteResult LastRoutes;
    }
}
