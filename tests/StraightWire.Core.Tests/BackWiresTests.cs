using System.Collections.Generic;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class BackWiresTests
    {
        static Pt P(double x, double y) => new Pt(x, y);

        static WireJob Channelled(SceneBuilder b)
        {
            var ctx = new RouteContext(b.Build(), null);
            BackWires.Prepare(ctx);
            return ctx.Jobs[0];
        }

        static SceneBuilder ScenarioA()
        {
            var b = new SceneBuilder().Node("S", 300, 200, 80, 60).Node("T", 100, 0, 80, 60);
            b.Wire("S", "T", P(380, 230), P(100, 30), WireStyle.Orthogonal);
            return b;
        }

        static SceneBuilder ScenarioB()
        {
            var b = new SceneBuilder().Node("S", 140, 400, 80, 60).Node("T", 100, 0, 80, 60);
            b.Wire("S", "T", P(220, 430), P(100, 30), WireStyle.Diagonal);
            return b;
        }

        [Fact]
        public void Channel_crosses_in_the_gap_between_the_two_nodes()
        {
            var w = Channelled(ScenarioA());
            Assert.True(w.HasBand);
            Assert.Equal((68.0, 192.0, 130.0), (w.Band0, w.Band1, w.Yb));
        }

        [Fact]
        public void Channel_skips_a_node_parked_in_the_gap()
        {
            var w = Channelled(ScenarioA().Node("M", 200, 100, 60, 40));   // with clearance: 92..148
            Assert.Equal((148.0, 192.0, 170.0), (w.Band0, w.Band1, w.Yb));
        }

        [Fact]
        public void Channel_goes_around_when_the_nodes_overlap_vertically()
        {
            var b = new SceneBuilder().Node("S", 300, 0, 80, 60).Node("T", 100, 20, 80, 60);
            b.Wire("S", "T", P(380, 30), P(100, 50), WireStyle.Orthogonal);
            var w = Channelled(b);
            Assert.False(w.HasBand);
            Assert.Equal(-24, w.Yb);   // above and below cost the same; above wins
        }

        [Fact]
        public void Orthogonal_crosses_at_the_channel()
        {
            var w = Channelled(ScenarioA());
            Assert.Equal(new[] { P(380, 230), P(398, 230), P(398, 130), P(82, 130), P(82, 30), P(100, 30) },
                BackWires.Orthogonal(w));
        }

        [Fact]
        public void Diagonal_crosses_a_tall_gap_in_one_45_degree_run()
        {
            var w = Channelled(ScenarioB());
            Assert.Equal(new[] { P(220, 430), P(238, 430), P(238, 308), P(82, 152), P(82, 30), P(100, 30) },
                BackWires.Diagonal(w));
        }

        [Fact]
        public void Diagonal_is_null_when_the_gap_is_shorter_than_the_run() =>
            Assert.Null(BackWires.Diagonal(Channelled(ScenarioA())));
    }
}
