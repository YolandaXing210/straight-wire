using System;
using System.Collections.Generic;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class RoutingEditTests
    {
        static Pt P(double x, double y) => new Pt(x, y);

        // S's output is (0, 100), T's input is (400, 300): the lane's free range is 18..382.
        static SceneBuilder Corridor() => new SceneBuilder().Node("S", -80, 80).Node("T", 400, 280);

        static double LaneOf(RouteResult r, string id) => r.Paths[id][1].X;

        [Fact]
        public void A_lane_fraction_places_the_lane_within_its_free_range()
        {
            var b = Corridor();
            string id = b.Wire("S", "T", P(0, 100), P(400, 300), WireStyle.Orthogonal, laneFraction: 0.25);
            Assert.Equal(109, LaneOf(Router.Route(b.Build(), null), id), 6);
        }

        [Fact]
        public void A_pinned_lane_sits_where_it_is_dragged()
        {
            var b = Corridor();
            string id = b.Wire("S", "T", P(0, 100), P(400, 300), WireStyle.Orthogonal, pinX: 300);
            Assert.Equal(300, LaneOf(Router.Route(b.Build(), null), id), 6);
        }

        static (string A, string B, SceneBuilder Builder) Stacked(double? aFraction = null, double? aPin = null)
        {
            var b = new SceneBuilder().Node("S1", -80, 80).Node("T1", 400, 280).Node("S2", -80, 130).Node("T2", 400, 330);
            string a = b.Wire("S1", "T1", P(0, 100), P(400, 300), WireStyle.Orthogonal, laneFraction: aFraction, pinX: aPin);
            string c = b.Wire("S2", "T2", P(0, 150), P(400, 350), WireStyle.Orthogonal);
            return (a, c, b);
        }

        [Fact]
        public void A_pinned_lane_holds_and_its_neighbour_keeps_its_side_and_moves_away()
        {
            var (a, c, b) = Stacked(aPin: 200);
            var r = Router.Route(b.Build(), null);
            Assert.Equal(200, LaneOf(r, a), 6);
            Assert.Equal(210, LaneOf(r, c), 6);
        }

        [Fact]
        public void A_lane_with_a_stored_fraction_gives_way_less_than_a_free_one()
        {
            var (a, c, b) = Stacked(aFraction: 0.5);
            var r = Router.Route(b.Build(), null);
            double la = LaneOf(r, a), lc = LaneOf(r, c);
            Assert.True(Math.Abs(la - 200) < Math.Abs(lc - 200), $"fraction lane {la}, free lane {lc}");
            Assert.True(Math.Abs(la - lc) >= 10 - 1e-6, $"gap {Math.Abs(la - lc)}");
        }

        [Fact]
        public void LaneFractions_report_where_each_lane_sits_in_its_range()
        {
            var b = Corridor();
            string free = b.Wire("S", "T", P(0, 100), P(400, 300), WireStyle.Orthogonal, "f:0");
            var r = Router.Route(b.Build(), null);
            Assert.Equal(0.5, r.LaneFractions[free], 6);

            var b2 = Corridor();
            string quarter = b2.Wire("S", "T", P(0, 100), P(400, 300), WireStyle.Orthogonal, laneFraction: 0.25);
            Assert.Equal(0.25, Router.Route(b2.Build(), null).LaneFractions[quarter], 6);
        }

        [Fact]
        public void A_hand_drawn_wire_follows_its_points_unchamfered_and_takes_no_lane()
        {
            var b = Corridor();
            string id = b.Wire("S", "T", P(0, 100), P(400, 300), WireStyle.Diagonal,
                               points: new[] { P(150, 100), P(150, 250), P(300, 300) });
            var r = Router.Route(b.Build(), null);
            Assert.Equal(new[] { P(0, 100), P(150, 100), P(150, 250), P(300, 300), P(400, 300) }, r.Paths[id]);
            Assert.False(r.LaneFractions.ContainsKey(id));
        }

        [Fact]
        public void A_hand_drawn_wire_is_an_obstacle_for_45_degree_decisions()
        {
            var b = new SceneBuilder().Node("S", -80, -20).Node("T", 200, 80);
            string id = b.Wire("S", "T", WireStyle.Diagonal);
            var manual = new[] { P(110, 70), P(125, 70), P(140, 70) };
            b.Wire("M", "N", manual[0], manual[2], WireStyle.Orthogonal, "M:0", points: new[] { manual[1] });
            var path = Router.Route(b.Build(), null).Paths[id];
            Assert.NotEqual(new[] { P(0, 0), P(50, 0), P(150, 100), P(200, 100) }, path);
            Assert.False(Geo.PolyPolyWithin(path, manual, 6), string.Join(" ", path));
        }

        [Fact]
        public void The_fingerprint_changes_with_every_edit_input()
        {
            Scene With(double? f, double? pin, IReadOnlyList<Pt> pts)
            {
                var b = Corridor();
                b.Wire("S", "T", P(0, 100), P(400, 300), WireStyle.Orthogonal, laneFraction: f, pinX: pin, points: pts);
                return b.Build();
            }
            long plain = With(null, null, null).Fingerprint();
            Assert.NotEqual(plain, With(0.3, null, null).Fingerprint());
            Assert.NotEqual(plain, With(null, 250, null).Fingerprint());
            Assert.NotEqual(plain, With(null, null, new[] { P(200, 100) }).Fingerprint());
            Assert.NotEqual(With(null, null, new[] { P(200, 100) }).Fingerprint(), With(null, null, new[] { P(201, 100) }).Fingerprint());
        }
    }
}
