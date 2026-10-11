using System;
using System.Collections.Generic;

namespace StraightWire.Core
{
    internal enum WireKind { Straight, Lane, Back }

    /// <summary>
    /// Working state for one wire during a single Route call (the prototype's `w`). Each routing
    /// stage fills in its own group of fields.
    /// </summary>
    internal sealed class WireJob
    {
        public WireJob(int index, SceneWire wire, int aNode, int bNode, Rect a, Rect b)
        {
            Index = index;
            Wire = wire;
            ANode = aNode;
            BNode = bNode;
            A = a;
            B = b;
            Ax = wire.Start.X;
            Ay = wire.Start.Y;
            Bx = wire.End.X;
            By = wire.End.Y;
            double dx = Bx - Ax, dy = By - Ay;
            Kind = dx < Routing.BackwardBelow ? WireKind.Back
                 : Math.Abs(dy) < Routing.StraightBelow ? WireKind.Straight
                 : WireKind.Lane;
        }

        public int Index { get; }
        public SceneWire Wire { get; }
        public string Id => Wire.Id;
        public string Group => Wire.Group;
        public WireStyle Style => Wire.Style;
        public bool Routed => Wire.Style != WireStyle.Curve;

        /// <summary>Index of the source / target node in RouteContext.NodeRects, or -1.</summary>
        public int ANode { get; }
        public int BNode { get; }

        /// <summary>Source and target node rectangles (a point at the port when the node is unknown).</summary>
        public Rect A { get; }
        public Rect B { get; }

        public double Ax { get; }
        public double Ay { get; }
        public double Bx { get; }
        public double By { get; }
        public WireKind Kind { get; }

        /// <summary>The lane x this wire had in the previous Route call, if it had one.</summary>
        public double? PrevLane;

        // Lane wires (Lanes)
        public double Lo, Hi, YLo, YHi, Pref, P, Lane;

        // Backwards wires (BackWires)
        public double Yb;
        public bool HasBand;
        public double Band0, Band1;

        // 45° decision (Diagonals)
        public bool Diag;
        public double Offset;
        public List<Pt> Target;
        public Rect TargetBox;
    }

    internal sealed class RouteContext
    {
        public RouteContext(Scene scene, RouterState state)
        {
            Scene = scene;
            Sp = scene.Spacing;
            NodeRects = new Rect[scene.Nodes.Count];
            NodeGrid = new SpatialGrid(Routing.GridCell);
            var byId = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < scene.Nodes.Count; i++)
            {
                NodeRects[i] = scene.Nodes[i].Bounds;
                NodeGrid.Add(i, NodeRects[i]);
                byId[scene.Nodes[i].Id] = i;
            }
            Jobs = new List<WireJob>(scene.Wires.Count);
            for (int i = 0; i < scene.Wires.Count; i++)
            {
                var w = scene.Wires[i];
                int a = byId.TryGetValue(w.SourceNode, out int ai) ? ai : -1;
                int b = byId.TryGetValue(w.TargetNode, out int bi) ? bi : -1;
                var job = new WireJob(i, w, a, b,
                    a >= 0 ? NodeRects[a] : new Rect(w.Start.X, w.Start.Y, w.Start.X, w.Start.Y),
                    b >= 0 ? NodeRects[b] : new Rect(w.End.X, w.End.Y, w.End.X, w.End.Y));
                if (state != null && state.TryGetLane(w.Id, out double x)) job.PrevLane = x;
                Jobs.Add(job);
            }
        }

        public Scene Scene { get; }
        public double Sp { get; }
        public Rect[] NodeRects { get; }
        public SpatialGrid NodeGrid { get; }

        /// <summary>One job per scene wire, in scene order: Jobs[i].Index == i.</summary>
        public List<WireJob> Jobs { get; }
    }
}
