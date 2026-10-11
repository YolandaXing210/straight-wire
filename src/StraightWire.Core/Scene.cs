using System;
using System.Collections.Generic;
using System.Linq;

namespace StraightWire.Core
{
    public enum WireStyle { Curve, Orthogonal, Diagonal }

    /// <summary>Something wires avoid: a top-level component or parameter on the canvas.</summary>
    public sealed class SceneNode
    {
        public SceneNode(string id, Rect bounds)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Bounds = bounds;
        }

        public string Id { get; }
        public Rect Bounds { get; }
    }

    /// <summary>
    /// One wire, from a source parameter's output grip (Start) to a target parameter's input grip
    /// (End). Wires with the same Group come from the same source parameter and may share a trunk.
    /// Curve wires are not routed; they are in the scene so junctions know their group is mixed.
    ///
    /// Edits: LaneFraction keeps an automatic wire's lane at that fraction of its free range;
    /// PinX holds the lane being dragged; Points makes the wire hand-drawn through those points.
    /// </summary>
    public sealed class SceneWire
    {
        public SceneWire(string id, string group, string sourceNode, string targetNode, Pt start, Pt end, WireStyle style,
                         double? laneFraction = null, double? pinX = null, IReadOnlyList<Pt> points = null)
        {
            LaneFraction = laneFraction;
            PinX = pinX;
            Points = points;
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Group = group ?? throw new ArgumentNullException(nameof(group));
            SourceNode = sourceNode ?? "";
            TargetNode = targetNode ?? "";
            Start = start;
            End = end;
            Style = style;
        }

        public string Id { get; }
        public string Group { get; }
        public string SourceNode { get; }
        public string TargetNode { get; }
        public Pt Start { get; }
        public Pt End { get; }
        public WireStyle Style { get; }
        public double? LaneFraction { get; }
        public double? PinX { get; }

        /// <summary>Interior points of a hand-drawn wire; null for an automatic one.</summary>
        public IReadOnlyList<Pt> Points { get; }
    }

    /// <summary>
    /// Everything routing reads. Nodes and wires are sorted by id, so the same canvas always
    /// routes the same way, whatever order Grasshopper lists its objects in.
    /// </summary>
    public sealed class Scene
    {
        public Scene(IEnumerable<SceneNode> nodes, IEnumerable<SceneWire> wires, double spacing)
        {
            Nodes = nodes.OrderBy(n => n.Id, StringComparer.Ordinal).ToArray();
            Wires = wires.OrderBy(w => w.Id, StringComparer.Ordinal).ToArray();
            Spacing = spacing;
        }

        public IReadOnlyList<SceneNode> Nodes { get; }
        public IReadOnlyList<SceneWire> Wires { get; }
        public double Spacing { get; }

        /// <summary>
        /// A 64-bit FNV-1a hash of everything routing depends on. The plugin re-routes only when
        /// this changes, so panning, zooming, selecting and solving cost nothing.
        /// </summary>
        public long Fingerprint()
        {
            var h = new Fnv();
            h.Add(Spacing);
            h.Add(Nodes.Count);
            foreach (var n in Nodes)
            {
                h.Add(n.Id);
                h.Add(n.Bounds);
            }
            h.Add(Wires.Count);
            foreach (var w in Wires)
            {
                h.Add(w.Id);
                h.Add(w.Group);
                h.Add(w.SourceNode);
                h.Add(w.TargetNode);
                h.Add(w.Start);
                h.Add(w.End);
                h.Add((int)w.Style);
                h.Add(w.LaneFraction ?? double.NaN);
                h.Add(w.PinX ?? double.NaN);
                h.Add(w.Points?.Count ?? -1);
                if (w.Points != null)
                    foreach (var p in w.Points) h.Add(p);
            }
            return h.Value;
        }

        sealed class Fnv
        {
            ulong _h = 14695981039346656037UL;

            public long Value => unchecked((long)_h);

            void Byte(byte b)
            {
                _h ^= b;
                _h = unchecked(_h * 1099511628211UL);
            }

            void Add(ulong v)
            {
                for (int i = 0; i < 8; i++)
                {
                    Byte((byte)v);
                    v >>= 8;
                }
            }

            public void Add(double d) => Add(unchecked((ulong)BitConverter.DoubleToInt64Bits(d)));
            public void Add(int i) => Add(unchecked((ulong)(uint)i));
            public void Add(Pt p) { Add(p.X); Add(p.Y); }
            public void Add(Rect r) { Add(r.X0); Add(r.Y0); Add(r.X1); Add(r.Y1); }

            public void Add(string s)
            {
                Add(s.Length);
                foreach (char c in s)
                {
                    Byte((byte)c);
                    Byte((byte)(c >> 8));
                }
            }
        }
    }
}
