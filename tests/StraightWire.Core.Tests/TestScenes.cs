using System.Collections.Generic;
using StraightWire.Core;

namespace StraightWire.Core.Tests
{
    /// <summary>Builds small scenes by hand. Wire ids are w000, w001, … in the order added.</summary>
    internal sealed class SceneBuilder
    {
        readonly List<SceneNode> _nodes = new List<SceneNode>();
        readonly Dictionary<string, Rect> _rects = new Dictionary<string, Rect>();
        readonly List<SceneWire> _wires = new List<SceneWire>();

        public double Spacing { get; set; } = 10;

        public SceneBuilder Node(string id, double x, double y, double w = 80, double h = 40)
        {
            var r = Rect.FromXYWH(x, y, w, h);
            _rects[id] = r;
            _nodes.Add(new SceneNode(id, r));
            return this;
        }

        /// <summary>A point on a node's right edge, dy below its top.</summary>
        public Pt Out(string node, double dy) => new Pt(_rects[node].X1, _rects[node].Y0 + dy);

        /// <summary>A point on a node's left edge, dy below its top.</summary>
        public Pt In(string node, double dy) => new Pt(_rects[node].X0, _rects[node].Y0 + dy);

        public string Wire(string from, string to, Pt start, Pt end, WireStyle style, string group = null,
                           double? laneFraction = null, double? pinX = null, IReadOnlyList<Pt> points = null, BackRuns back = null)
        {
            string id = "w" + _wires.Count.ToString("D3");
            _wires.Add(new SceneWire(id, group ?? from + ":0", from, to, start, end, style, laneFraction, pinX, points, back));
            return id;
        }

        /// <summary>From the middle of one node's right edge to the middle of another's left edge.</summary>
        public string Wire(string from, string to, WireStyle style, string group = null) =>
            Wire(from, to, Out(from, (_rects[from].Y1 - _rects[from].Y0) / 2),
                 In(to, (_rects[to].Y1 - _rects[to].Y0) / 2), style, group);

        public Scene Build() => new Scene(_nodes, _wires, Spacing);
    }

    /// <summary>The routing stages in Router's order, stopping before paths are built.</summary>
    internal static class Pipeline
    {
        public static RouteContext Run(Scene scene, RouterState state = null)
        {
            var ctx = new RouteContext(scene, state);
            BackWires.Prepare(ctx);
            var lanes = Lanes.Prepare(ctx);
            Lanes.Relax(lanes, ctx.Sp);
            Diagonals.Decide(ctx);
            return ctx;
        }
    }

    internal static class TestScenes
    {
        /// <summary>
        /// A seeded grid of nodes with wires running one to three columns right (one in twenty runs
        /// one column left, as does anything starting in the last column), up to four rows up or
        /// down. Each node has three output ports, so some wires share a source.
        /// </summary>
        public static Scene Random(int seed, int columns, int rows, int wires, WireStyle style, double spacing = 10)
        {
            var rng = new System.Random(seed);
            var rects = new Rect[columns, rows];
            var nodes = new List<SceneNode>();
            for (int c = 0; c < columns; c++)
                for (int r = 0; r < rows; r++)
                {
                    rects[c, r] = Rect.FromXYWH(c * 240 + rng.Next(0, 30), r * 140 + rng.Next(0, 20),
                                                70 + rng.Next(0, 40), 40 + rng.Next(0, 60));
                    nodes.Add(new SceneNode(NodeId(c, r), rects[c, r]));
                }
            var list = new List<SceneWire>();
            for (int i = 0; i < wires; i++)
            {
                int c = rng.Next(0, columns), r = rng.Next(0, rows);
                bool back = c > 0 && rng.NextDouble() < 0.05;
                int tc = back ? c - 1 : System.Math.Min(columns - 1, c + 1 + rng.Next(0, 3));
                if (tc == c) tc = System.Math.Max(0, c - 1);
                int tr = System.Math.Clamp(r + rng.Next(-4, 5), 0, rows - 1);
                Rect a = rects[c, r], b = rects[tc, tr];
                int port = rng.Next(0, 3);
                var start = new Pt(a.X1, a.Y0 + (a.Y1 - a.Y0) * (port + 1) / 4);
                var end = new Pt(b.X0, b.Y0 + (b.Y1 - b.Y0) * (rng.Next(0, 3) + 1) / 4);
                list.Add(new SceneWire("w" + i.ToString("D4"), NodeId(c, r) + ":" + port,
                                       NodeId(c, r), NodeId(tc, tr), start, end, style));
            }
            return new Scene(nodes, list, spacing);
        }

        static string NodeId(int c, int r) => "n" + c.ToString("D2") + r.ToString("D2");
    }
}
