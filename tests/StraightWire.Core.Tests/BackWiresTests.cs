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

        static SceneBuilder ScenarioA(BackRuns back = null)
        {
            var b = new SceneBuilder().Node("S", 300, 200, 80, 60).Node("T", 100, 0, 80, 60);
            b.Wire("S", "T", P(380, 230), P(100, 30), WireStyle.Orthogonal, back: back);
            return b;
        }

        static SceneBuilder ScenarioB(BackRuns back = null)
        {
            var b = new SceneBuilder().Node("S", 140, 400, 80, 60).Node("T", 100, 0, 80, 60);
            b.Wire("S", "T", P(220, 430), P(100, 30), WireStyle.Diagonal, back: back);
            return b;
        }

        /// <summary>S and T overlap vertically, so the wire goes around: above, at -24, by default.</summary>
        static SceneBuilder Overlapping(BackRuns back = null)
        {
            var b = new SceneBuilder().Node("S", 300, 0, 80, 60).Node("T", 100, 20, 80, 60);
            b.Wire("S", "T", P(380, 30), P(100, 50), WireStyle.Orthogonal, back: back);
            return b;
        }

        static Pt[] Routed(SceneBuilder b, string id = "w000") => Router.Route(b.Build(), null).Paths[id];

        /// <summary>The y of the run back to the left: the path's fourth point in right-angle style.</summary>
        static double CrossingY(SceneBuilder b) => Routed(b)[3].Y;

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

        [Fact]
        public void Out_and_in_runs_move_the_verticals() =>
            Assert.Equal(new[] { P(380, 230), P(420, 230), P(420, 130), P(70, 130), P(70, 30), P(100, 30) },
                Routed(ScenarioA(new BackRuns(@out: 40, @in: 30))));

        [Fact]
        public void Cross_moves_the_crossing_within_the_gap() =>
            Assert.Equal(90, CrossingY(ScenarioA(new BackRuns(cross: -40))));

        [Fact]
        public void Runs_stop_at_their_limits() =>
            Assert.Equal(new[] { P(380, 230), P(398, 230), P(398, 192), P(82, 192), P(82, 30), P(100, 30) },
                Routed(ScenarioA(new BackRuns(@out: -50, @in: 0, cross: 500))));

        [Fact]
        public void The_gap_search_spans_the_moved_verticals()
        {
            // M (422 with clearance) is past the stub's reach (406) but inside a 100-long out run.
            Assert.Equal(130, CrossingY(ScenarioA().Node("M", 430, 100, 40, 40)));
            var w = Channelled(ScenarioA(new BackRuns(@out: 100)).Node("M", 430, 100, 40, 40));
            Assert.Equal((148.0, 192.0, 170.0), (w.Band0, w.Band1, w.Yb));
        }

        [Fact]
        public void Cross_can_be_dragged_into_another_free_stretch()
        {
            // M leaves 68..92 and 148..192; routing picks the wider at 170. 115 is nearer the other.
            var w = Channelled(ScenarioA(new BackRuns(cross: -55)).Node("M", 200, 100, 60, 40));
            Assert.Equal((68.0, 92.0, 92.0), (w.Band0, w.Band1, w.Yb));
        }

        [Fact]
        public void Around_cross_stays_clear_of_the_nodes()
        {
            Assert.Equal(-40, CrossingY(Overlapping(new BackRuns(cross: -16))));
            Assert.Equal(-8, CrossingY(Overlapping(new BackRuns(cross: 30))));
        }

        [Fact]
        public void A_stored_gap_cross_goes_around_when_the_gap_closes() =>
            Assert.Equal(-8, CrossingY(Overlapping(new BackRuns(cross: 60))));

        static SceneBuilder TwoBackWires(BackRuns first, BackRuns second)
        {
            var b = ScenarioA(first).Node("U", 100, 300, 80, 60);
            b.Wire("S", "U", P(380, 230), P(100, 330), WireStyle.Orthogonal, back: second);
            return b;
        }

        [Fact]
        public void Back_wires_from_one_output_share_a_stored_out_run()
        {
            var r = Router.Route(TwoBackWires(new BackRuns(@out: 40), null).Build(), null);
            Assert.Equal(420, r.Paths["w000"][1].X);
            Assert.Equal(420, r.Paths["w001"][1].X);
        }

        [Fact]
        public void The_first_stored_out_run_of_an_output_wins()
        {
            var r = Router.Route(TwoBackWires(new BackRuns(@out: 40), new BackRuns(@out: 25)).Build(), null);
            Assert.Equal(420, r.Paths["w000"][1].X);
            Assert.Equal(420, r.Paths["w001"][1].X);
        }

        [Fact]
        public void A_set_vertical_steps_past_a_component()
        {
            // N with clearance covers x 422..478 and y 192..228, across the out vertical's 130..230.
            SceneBuilder With(BackRuns back) => ScenarioA(back).Node("N", 430, 200, 40, 20);
            Assert.Equal(422, Routed(With(new BackRuns(@out: 50)))[1].X);
            Assert.Equal(478, Routed(With(new BackRuns(@out: 75)))[1].X);
            Assert.Equal(398, Routed(With(null))[1].X);
        }

        [Fact]
        public void A_set_in_run_steps_past_a_component() =>
            Assert.Equal(68, Routed(ScenarioA(new BackRuns(@in: 50)).Node("N", 20, 40, 40, 40))[3].X);

        [Fact]
        public void Diagonal_uses_the_moved_verticals() =>
            Assert.Equal(new[] { P(220, 430), P(250, 430), P(250, 314), P(82, 146), P(82, 30), P(100, 30) },
                BackWires.Diagonal(Channelled(ScenarioB(new BackRuns(@out: 30)))));

        [Fact]
        public void A_forward_wire_ignores_back_runs()
        {
            var b = new SceneBuilder().Node("S", -80, 80).Node("T", 400, 280);
            b.Wire("S", "T", P(0, 100), P(400, 300), WireStyle.Orthogonal, back: new BackRuns(@out: 100, cross: 50));
            var r = Router.Route(b.Build(), null);
            Assert.Equal(200, r.Paths["w000"][1].X, 6);
            Assert.Empty(r.Backs);
        }

        [Fact]
        public void Backs_report_every_routed_back_wire_with_all_parts()
        {
            var auto = Router.Route(ScenarioA().Build(), null).Backs["w000"];
            Assert.Equal((18.0, 18.0, 0.0), (auto.Out.Value, auto.In.Value, auto.Cross.Value));
            var clamped = Router.Route(ScenarioA(new BackRuns(cross: 500)).Build(), null).Backs["w000"];
            Assert.Equal(62, clamped.Cross);

            var b = ScenarioA();
            b.Wire("S", "T", P(380, 240), P(100, 40), WireStyle.Curve);
            b.Wire("S", "T", P(380, 250), P(100, 50), WireStyle.Orthogonal, points: new[] { P(400, 250), P(400, 50) });
            Assert.Equal(new[] { "w000" }, Router.Route(b.Build(), null).Backs.Keys);
        }
    }
}
