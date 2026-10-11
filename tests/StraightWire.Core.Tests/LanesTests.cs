using System;
using System.Collections.Generic;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class LanesTests
    {
        static SceneBuilder Corridor() => new SceneBuilder().Node("S", -80, 80).Node("T", 400, 280);

        static WireJob FeasibleOf(SceneBuilder b, RouterState state = null)
        {
            var ctx = new RouteContext(b.Build(), state);
            var w = ctx.Jobs[0];
            Lanes.Feasible(ctx, w, new List<int>());
            return w;
        }

        [Fact]
        public void Feasible_uses_the_whole_run_when_nothing_is_in_the_way()
        {
            var b = Corridor();
            b.Wire("S", "T", WireStyle.Orthogonal);
            var w = FeasibleOf(b);
            Assert.Equal((18.0, 382.0), (w.Lo, w.Hi));
        }

        [Fact]
        public void Feasible_picks_the_wider_free_stretch()
        {
            var b = Corridor().Node("O", 184, 150, 40, 100);   // with clearance: 176..232
            b.Wire("S", "T", WireStyle.Orthogonal);
            var w = FeasibleOf(b);
            Assert.Equal((18.0, 176.0), (w.Lo, w.Hi));
        }

        [Fact]
        public void Feasible_prefers_the_right_stretch_on_a_tie()
        {
            var b = Corridor().Node("O", 180, 150, 40, 100);   // 172..228: both stretches 154 wide
            b.Wire("S", "T", WireStyle.Orthogonal);
            var w = FeasibleOf(b);
            Assert.Equal((228.0, 382.0), (w.Lo, w.Hi));
        }

        [Fact]
        public void Feasible_keeps_the_stretch_holding_the_previous_lane()
        {
            var b = Corridor().Node("O", 180, 150, 40, 100);
            string id = b.Wire("S", "T", WireStyle.Orthogonal);
            var state = new RouterState();
            state.Replace(new[] { new KeyValuePair<string, double>(id, 95) });
            var w = FeasibleOf(b, state);
            Assert.Equal((18.0, 172.0), (w.Lo, w.Hi));
        }

        [Fact]
        public void Feasible_stops_short_of_a_node_in_front_of_the_source_port()
        {
            var b = Corridor().Node("P", 100, 90, 40, 20);     // sits on y = 100, the source's row
            b.Wire("S", "T", WireStyle.Orthogonal);
            var w = FeasibleOf(b);
            Assert.Equal((18.0, 92.0), (w.Lo, w.Hi));
        }

        [Fact]
        public void Feasible_falls_back_to_the_whole_run_when_everything_is_blocked()
        {
            var b = Corridor().Node("O", 0, 150, 400, 100);
            b.Wire("S", "T", WireStyle.Orthogonal);
            var w = FeasibleOf(b);
            Assert.Equal((18.0, 382.0), (w.Lo, w.Hi));
        }

        static (WireJob A, WireJob B) TwoStacked()
        {
            var b = new SceneBuilder().Node("S1", -80, 80).Node("T1", 400, 280)
                                      .Node("S2", -80, 130).Node("T2", 400, 330);
            b.Wire("S1", "T1", WireStyle.Orthogonal);   // 100 -> 300
            b.Wire("S2", "T2", WireStyle.Orthogonal);   // 150 -> 350
            var ctx = new RouteContext(b.Build(), null);
            var lanes = Lanes.Prepare(ctx);
            Lanes.Relax(lanes, ctx.Sp);
            return (lanes[0], lanes[1]);
        }

        [Fact]
        public void CrossCount_counts_horizontal_runs_crossing_the_other_lane()
        {
            var b = new SceneBuilder().Node("S1", -80, 80).Node("T1", 400, 280)
                                      .Node("S2", -80, 130).Node("T2", 400, 330);
            b.Wire("S1", "T1", WireStyle.Orthogonal);
            b.Wire("S2", "T2", WireStyle.Orthogonal);
            var ctx = new RouteContext(b.Build(), null);
            WireJob a = ctx.Jobs[0], c = ctx.Jobs[1];
            Assert.Equal(2, Lanes.CrossCount(a, 195, c, 205));
            Assert.Equal(0, Lanes.CrossCount(a, 205, c, 195));
        }

        [Fact]
        public void Relax_orders_lanes_to_avoid_crossings_and_keeps_the_spacing()
        {
            var (a, b) = TwoStacked();
            Assert.True(a.Lane > b.Lane, $"a {a.Lane} should be right of b {b.Lane}");
            Assert.True(a.Lane - b.Lane >= 10 - 1e-9, $"gap {a.Lane - b.Lane}");
            Assert.InRange(a.Lane, a.Lo, a.Hi);
            Assert.InRange(b.Lane, b.Lo, b.Hi);
        }

        [Fact]
        public void Relax_draws_same_source_wires_onto_one_trunk()
        {
            var b = new SceneBuilder().Node("S", -80, 80).Node("T1", 400, 280).Node("T2", 300, -120);
            b.Wire("S", "T1", WireStyle.Orthogonal);   // range 18..382, prefers 200
            b.Wire("S", "T2", WireStyle.Orthogonal);   // range 18..282, prefers 150
            var ctx = new RouteContext(b.Build(), null);
            var lanes = Lanes.Prepare(ctx);
            Lanes.Relax(lanes, ctx.Sp);
            Assert.True(Math.Abs(lanes[0].Lane - lanes[1].Lane) < 0.01);
            Assert.Equal(175, lanes[0].Lane, 2);
        }

        [Fact]
        public void Prepare_skips_curve_straight_and_backwards_wires()
        {
            var b = new SceneBuilder().Node("S", 0, 0).Node("T", 300, 0);
            b.Wire("S", "T", new Pt(80, 20), new Pt(300, 60), WireStyle.Curve);
            b.Wire("S", "T", new Pt(80, 20), new Pt(300, 20), WireStyle.Diagonal);
            b.Wire("S", "T", new Pt(80, 20), new Pt(50, 60), WireStyle.Diagonal);
            b.Wire("S", "T", new Pt(80, 20), new Pt(300, 60), WireStyle.Orthogonal);
            var lanes = Lanes.Prepare(new RouteContext(b.Build(), null));
            Assert.Equal(new[] { "w003" }, lanes.ConvertAll(l => l.Id));
        }
    }
}
