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

        public string Wire(string from, string to, Pt start, Pt end, WireStyle style, string group = null)
        {
            string id = "w" + _wires.Count.ToString("D3");
            _wires.Add(new SceneWire(id, group ?? from + ":0", from, to, start, end, style));
            return id;
        }

        /// <summary>From the middle of one node's right edge to the middle of another's left edge.</summary>
        public string Wire(string from, string to, WireStyle style, string group = null) =>
            Wire(from, to, Out(from, (_rects[from].Y1 - _rects[from].Y0) / 2),
                 In(to, (_rects[to].Y1 - _rects[to].Y0) / 2), style, group);

        public Scene Build() => new Scene(_nodes, _wires, Spacing);
    }
}
