using System;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class AnchoringTests
    {
        static Pt P(double x, double y) => new Pt(x, y);

        static FileSettings WithPoints() => new FileSettings(WireStyle.Orthogonal, 10)
            .WithPoints("w", new ManualPoints(new[] { P(50, 20), P(50, 80) }, P(0, 0), P(100, 0)));

        static Func<string, (Pt Source, Pt Target)?> At(Pt source, Pt target) => id => (source, target);

        [Fact]
        public void Both_ends_moved_together_carry_the_points()
        {
            var s = Anchoring.Settle(WithPoints(), At(P(10, 5), P(110, 5)));
            var m = s.Points["w"];
            Assert.Equal(new[] { P(60, 25), P(60, 85) }, m.Points);
            Assert.Equal((P(10, 5), P(110, 5)), (m.SourceAt, m.TargetAt));
        }

        [Fact]
        public void One_end_moved_leaves_the_points_and_updates_the_anchors()
        {
            var s = Anchoring.Settle(WithPoints(), At(P(0, 0), P(130, 40)));
            var m = s.Points["w"];
            Assert.Equal(new[] { P(50, 20), P(50, 80) }, m.Points);
            Assert.Equal((P(0, 0), P(130, 40)), (m.SourceAt, m.TargetAt));
        }

        [Fact]
        public void Ends_moved_one_after_the_other_never_carry_the_points()
        {
            var s = Anchoring.Settle(WithPoints(), At(P(0, 0), P(110, 0)));
            s = Anchoring.Settle(s, At(P(10, 0), P(110, 0)));
            Assert.Equal(new[] { P(50, 20), P(50, 80) }, s.Points["w"].Points);
        }

        [Fact]
        public void Moving_together_and_back_returns_the_points()
        {
            var s = Anchoring.Settle(WithPoints(), At(P(30, 30), P(130, 30)));
            s = Anchoring.Settle(s, At(P(0, 0), P(100, 0)));
            Assert.Equal(new[] { P(50, 20), P(50, 80) }, s.Points["w"].Points);
        }

        [Fact]
        public void A_wire_with_no_current_anchors_is_left_alone()
        {
            var before = WithPoints();
            var s = Anchoring.Settle(before, id => null);
            Assert.Same(before, s);
        }

        [Fact]
        public void Nothing_moved_returns_the_same_settings()
        {
            var before = WithPoints();
            Assert.Same(before, Anchoring.Settle(before, At(P(0, 0), P(100, 0))));
        }
    }
}
