using System.Linq;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class SceneTests
    {
        static SceneNode N(string id, double x) => new SceneNode(id, Rect.FromXYWH(x, 0, 80, 40));
        static SceneWire W(string id, WireStyle style = WireStyle.Diagonal) =>
            new SceneWire(id, "a:0", "a", "b", new Pt(80, 20), new Pt(200, 60), style);

        [Fact]
        public void Nodes_and_wires_are_sorted_by_id()
        {
            var s = new Scene(new[] { N("b", 0), N("a", 0) }, new[] { W("w2"), W("w1") }, 10);
            Assert.Equal(new[] { "a", "b" }, s.Nodes.Select(n => n.Id));
            Assert.Equal(new[] { "w1", "w2" }, s.Wires.Select(w => w.Id));
        }

        [Fact]
        public void Fingerprint_ignores_input_order()
        {
            var a = new Scene(new[] { N("a", 0), N("b", 200) }, new[] { W("w1"), W("w2") }, 10);
            var b = new Scene(new[] { N("b", 200), N("a", 0) }, new[] { W("w2"), W("w1") }, 10);
            Assert.Equal(a.Fingerprint(), b.Fingerprint());
        }

        [Fact]
        public void Fingerprint_changes_with_anything_routing_reads()
        {
            long baseline = new Scene(new[] { N("a", 0) }, new[] { W("w1") }, 10).Fingerprint();
            Assert.NotEqual(baseline, new Scene(new[] { N("a", 0.5) }, new[] { W("w1") }, 10).Fingerprint());
            Assert.NotEqual(baseline, new Scene(new[] { N("a", 0) }, new[] { W("w1", WireStyle.Orthogonal) }, 10).Fingerprint());
            Assert.NotEqual(baseline, new Scene(new[] { N("a", 0) }, new[] { W("w1") }, 12).Fingerprint());
        }
    }
}
