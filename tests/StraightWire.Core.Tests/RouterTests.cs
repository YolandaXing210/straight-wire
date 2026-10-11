using System;
using System.Collections.Generic;
using System.Linq;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class RouterTests
    {
        static Pt P(double x, double y) => new Pt(x, y);

        public static IEnumerable<object[]> RandomScenes() =>
            from seed in new[] { 1, 2, 3 }
            from style in new[] { WireStyle.Orthogonal, WireStyle.Diagonal }
            select new object[] { seed, style };

        static void AssertSane(Scene scene, RouteResult result)
        {
            foreach (var w in scene.Wires)
            {
                if (w.Style == WireStyle.Curve)
                {
                    Assert.False(result.Paths.ContainsKey(w.Id));
                    continue;
                }
                var p = result.Paths[w.Id];
                Assert.True(p.Length >= 2, w.Id + " has " + p.Length + " points");
                Assert.Equal(w.Start, p[0]);
                Assert.Equal(w.End, p[p.Length - 1]);
                Assert.All(p, q => Assert.True(double.IsFinite(q.X) && double.IsFinite(q.Y), w.Id + " has " + q));
            }
        }

        [Theory, MemberData(nameof(RandomScenes))]
        public void Every_path_starts_and_ends_exactly_on_its_ports(int seed, WireStyle style)
        {
            var scene = TestScenes.Random(seed, 6, 5, 60, style);
            AssertSane(scene, Router.Route(scene, new RouterState()));
        }

        [Theory, MemberData(nameof(RandomScenes))]
        public void Segments_keep_to_the_style_angles(int seed, WireStyle style)
        {
            var scene = TestScenes.Random(seed, 6, 5, 60, style);
            foreach (var path in Router.Route(scene, new RouterState()).Paths.Values)
                for (int i = 0; i < path.Length - 1; i++)
                {
                    double dx = Math.Abs(path[i + 1].X - path[i].X), dy = Math.Abs(path[i + 1].Y - path[i].Y);
                    bool flat = dy < Routing.StraightBelow;   // a straight wire may lean by under half a unit
                    bool upright = dx < 1e-9;
                    bool diagonal = Math.Abs(dx - dy) < 0.02;
                    bool ok = style == WireStyle.Orthogonal ? flat || upright : flat || upright || diagonal;
                    Assert.True(ok, $"segment {path[i]} -> {path[i + 1]}");
                }
        }

        [Fact]
        public void Overlapping_lanes_keep_the_spacing()
        {
            var b = new SceneBuilder();
            for (int i = 0; i < 4; i++) b.Node("S" + i, -80, 40 * i - 20).Node("T" + i, 400, 180 + 40 * i);
            for (int i = 0; i < 4; i++) b.Wire("S" + i, "T" + i, WireStyle.Orthogonal);
            var lanes = Router.Route(b.Build(), new RouterState()).Paths.Values.Select(p => p[1].X).OrderBy(x => x).ToArray();
            for (int i = 1; i < lanes.Length; i++)
                Assert.True(lanes[i] - lanes[i - 1] >= 10 - 0.01, string.Join(", ", lanes));
        }

        [Fact]
        public void Lanes_avoid_a_node_when_there_is_room()
        {
            var b = new SceneBuilder().Node("S", -80, 80).Node("T", 400, 280).Node("O", 180, 150, 40, 100);
            string id = b.Wire("S", "T", WireStyle.Orthogonal);
            var path = Router.Route(b.Build(), new RouterState()).Paths[id];
            var keepOut = new Rect(180, 150, 220, 250).Inflate(Routing.Clearance - 0.01);
            for (int i = 0; i < path.Length - 1; i++)
                Assert.False(Geo.SegRectHit(path[i], path[i + 1], keepOut), $"segment {path[i]} -> {path[i + 1]}");
        }

        [Fact]
        public void An_unobstructed_45_degree_wire_bends_twice()
        {
            var b = new SceneBuilder().Node("S", -80, -20).Node("T", 200, 80);
            string id = b.Wire("S", "T", WireStyle.Diagonal);
            Assert.Equal(new[] { P(0, 0), P(50, 0), P(150, 100), P(200, 100) }, Router.Route(b.Build(), null).Paths[id]);
        }

        [Fact]
        public void A_backwards_wire_crosses_between_its_nodes()
        {
            var b = new SceneBuilder().Node("S", 300, 200, 80, 60).Node("T", 100, 0, 80, 60);
            string id = b.Wire("S", "T", P(380, 230), P(100, 30), WireStyle.Orthogonal);
            Assert.Equal(new[] { P(380, 230), P(398, 230), P(398, 130), P(82, 130), P(82, 30), P(100, 30) },
                Router.Route(b.Build(), null).Paths[id]);
        }

        [Fact]
        public void A_tall_gap_takes_a_single_45_degree_crossing()
        {
            var b = new SceneBuilder().Node("S", 140, 400, 80, 60).Node("T", 100, 0, 80, 60);
            string id = b.Wire("S", "T", P(220, 430), P(100, 30), WireStyle.Diagonal);
            var path = Router.Route(b.Build(), null).Paths[id];
            Assert.Contains((P(238, 308), P(82, 152)), path.Zip(path.Skip(1), (p, q) => (p, q)));
        }

        [Fact]
        public void Same_source_wires_share_a_trunk_with_a_dot_where_they_split()
        {
            var b = new SceneBuilder().Node("S", -80, 80).Node("T1", 400, 280).Node("T2", 300, -120);
            string down = b.Wire("S", "T1", WireStyle.Orthogonal);
            string up = b.Wire("S", "T2", WireStyle.Orthogonal);
            var r = Router.Route(b.Build(), new RouterState());
            Assert.Equal(r.Paths[down][1].X, r.Paths[up][1].X, 2);
            var dot = Assert.Single(r.Junctions);
            Assert.Equal("S:0", dot.Group);
            Assert.Equal(175, dot.At.X, 2);
            Assert.InRange(dot.At.Y, 100, 101);
        }

        [Fact]
        public void The_same_canvas_in_any_order_routes_the_same()
        {
            var scene = TestScenes.Random(4, 6, 5, 60, WireStyle.Diagonal);
            var rng = new Random(99);
            var shuffled = new Scene(scene.Nodes.OrderBy(_ => rng.Next()).ToList(),
                                     scene.Wires.OrderBy(_ => rng.Next()).ToList(), scene.Spacing);
            var a = Router.Route(scene, new RouterState());
            var b = Router.Route(shuffled, new RouterState());
            Assert.Equal(a.Paths.Keys.OrderBy(k => k, StringComparer.Ordinal), b.Paths.Keys.OrderBy(k => k, StringComparer.Ordinal));
            foreach (var k in a.Paths.Keys) Assert.Equal(a.Paths[k], b.Paths[k]);
            Assert.Equal(a.Junctions.Select(j => (j.Group, j.At)), b.Junctions.Select(j => (j.Group, j.At)));
        }

        [Fact]
        public void A_lane_does_not_jump_when_a_node_moves_by_one()
        {
            Scene Corridor(double obstacleX)
            {
                var b = new SceneBuilder().Node("S", -80, 80).Node("T", 400, 280).Node("O", obstacleX, 150, 40, 100);
                b.Wire("S", "T", WireStyle.Orthogonal);
                return b.Build();
            }
            var state = new RouterState();
            double before = Router.Route(Corridor(181), state).Paths["w000"][1].X;   // left stretch is wider
            double after = Router.Route(Corridor(180), state).Paths["w000"][1].X;    // now a tie
            double fresh = Router.Route(Corridor(180), new RouterState()).Paths["w000"][1].X;
            Assert.True(before < 173, $"before {before}");
            Assert.True(after < 172, $"after {after}");
            Assert.True(fresh > 228, $"fresh {fresh}");
        }

        [Fact]
        public void Curve_wires_get_no_path_and_take_no_lane()
        {
            var b = new SceneBuilder().Node("S", -80, 80).Node("T", 400, 280);
            string curve = b.Wire("S", "T", WireStyle.Curve, "c:0");
            string ortho = b.Wire("S", "T", P(0, 110), P(400, 310), WireStyle.Orthogonal, "o:0");
            var state = new RouterState();
            var r = Router.Route(b.Build(), state);
            Assert.False(r.Paths.ContainsKey(curve));
            Assert.Equal(200, r.Paths[ortho][1].X, 6);   // alone: the middle of 18..382
            Assert.Equal(1, state.Count);
        }

        // Review focus 1: components dropped on top of each other, ports inside other nodes.
        [Theory]
        [InlineData(WireStyle.Orthogonal)]
        [InlineData(WireStyle.Diagonal)]
        public void Survives_overlapping_nodes_and_ports_inside_nodes(WireStyle style)
        {
            var b = new SceneBuilder().Node("S", -80, -20).Node("T", 200, 80)
                                      .Node("Big", -40, -60, 320, 220).Node("T2", 210, 90);
            b.Wire("S", "T", style);
            b.Wire("S", "T2", style);
            b.Wire("T", "S", style);   // backwards, both ends inside Big
            var scene = b.Build();
            AssertSane(scene, Router.Route(scene, new RouterState()));
        }

        // Review focus 2: an output wired back into its own component.
        [Fact]
        public void Routes_a_self_loop_around_its_node()
        {
            var b = new SceneBuilder().Node("N", 0, 0);
            string id = b.Wire("N", "N", P(80, 20), P(0, 20), WireStyle.Orthogonal);
            Assert.Equal(new[] { P(80, 20), P(98, 20), P(98, -24), P(-18, -24), P(-18, 20), P(0, 20) },
                Router.Route(b.Build(), null).Paths[id]);
        }

        // Review focus 2: both grips on the same point.
        [Theory]
        [InlineData(WireStyle.Orthogonal)]
        [InlineData(WireStyle.Diagonal)]
        public void Survives_a_zero_length_wire(WireStyle style)
        {
            var b = new SceneBuilder().Node("N", 0, 0);
            b.Wire("N", "N", P(50, 50), P(50, 50), style);
            var scene = b.Build();
            AssertSane(scene, Router.Route(scene, null));
        }
    }
}
