using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class DiagonalsTests
    {
        static Pt P(double x, double y) => new Pt(x, y);

        // S's output is (0, 0); T's input is (200, 100).
        static SceneBuilder Open() => new SceneBuilder().Node("S", -80, -20).Node("T", 200, 80);

        static Pt[] PathOf(SceneBuilder b) => WirePath.For(Pipeline.Run(b.Build()).Jobs[0]).ToArray();

        [Fact]
        public void An_unobstructed_45_degree_wire_bends_twice()
        {
            var b = Open();
            b.Wire("S", "T", WireStyle.Diagonal);
            Assert.Equal(new[] { P(0, 0), P(50, 0), P(150, 100), P(200, 100) }, PathOf(b));
        }

        [Fact]
        public void A_shallow_wire_is_one_diagonal_without_a_search()
        {
            var b = new SceneBuilder().Node("S", -80, -20).Node("T", 200, 0);   // input (200, 20)
            b.Wire("S", "T", WireStyle.Diagonal);
            Assert.Equal(new[] { P(0, 0), P(90, 0), P(110, 20), P(200, 20) }, PathOf(b));
        }

        [Fact]
        public void A_diagonal_that_would_hit_nodes_keeps_four_bends()
        {
            // Two nodes leave only a narrow free stretch at x 88..112. Every single diagonal
            // within reach crosses one of them; the four-bend route down x = 100 touches neither.
            var b = Open().Node("N1", 20, 30, 60, 40).Node("N2", 120, 30, 60, 40);
            b.Wire("S", "T", WireStyle.Diagonal);
            Assert.Equal(new[] { P(0, 0), P(86, 0), P(100, 14), P(100, 86), P(114, 100), P(200, 100) }, PathOf(b));
        }

        [Fact]
        public void Orthogonal_lane_wires_bend_at_right_angles()
        {
            var b = Open();
            b.Wire("S", "T", WireStyle.Orthogonal);
            Assert.Equal(new[] { P(0, 0), P(100, 0), P(100, 100), P(200, 100) }, PathOf(b));
        }

        [Fact]
        public void A_straight_wire_is_one_segment()
        {
            var b = Open();
            b.Wire("S", "T", P(0, 0), P(200, 0.3), WireStyle.Orthogonal);
            Assert.Equal(new[] { P(0, 0), P(200, 0.3) }, PathOf(b));
        }

        [Fact]
        public void A_45_degree_backwards_wire_chamfers_its_square_corners()
        {
            var b = new SceneBuilder().Node("S", 300, 200, 80, 60).Node("T", 100, 0, 80, 60);
            b.Wire("S", "T", P(380, 230), P(100, 30), WireStyle.Diagonal);
            Assert.Equal(new[]
            {
                P(380, 230), P(384, 230), P(398, 216), P(398, 144), P(384, 130),
                P(96, 130), P(82, 116), P(82, 44), P(96, 30), P(100, 30),
            }, PathOf(b));
        }
    }
}
