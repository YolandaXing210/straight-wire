using System.Collections.Generic;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class GeometryTests
    {
        [Fact]
        public void SegDist_is_the_perpendicular_distance_inside_the_segment() =>
            Assert.Equal(5, Geo.SegDist(new Pt(5, 5), new Pt(0, 0), new Pt(10, 0)), 9);

        [Fact]
        public void SegDist_clamps_to_the_nearest_end() =>
            Assert.Equal(5, Geo.SegDist(new Pt(13, 4), new Pt(0, 0), new Pt(10, 0)), 9);

        [Fact]
        public void SegDist_of_a_degenerate_segment_is_point_distance() =>
            Assert.Equal(5, Geo.SegDist(new Pt(3, 4), new Pt(0, 0), new Pt(0, 0)), 9);

        [Fact]
        public void PolyDist_takes_the_nearest_segment() =>
            Assert.Equal(2, Geo.PolyDist(new Pt(12, 5), new[] { new Pt(0, 0), new Pt(10, 0), new Pt(10, 10) }), 9);

        [Fact]
        public void SegSegDist_is_zero_when_segments_cross() =>
            Assert.Equal(0, Geo.SegSegDist(new Pt(0, 0), new Pt(10, 10), new Pt(0, 10), new Pt(10, 0)));

        [Fact]
        public void SegSegDist_of_parallel_segments() =>
            Assert.Equal(3, Geo.SegSegDist(new Pt(0, 0), new Pt(10, 0), new Pt(0, 3), new Pt(10, 3)), 9);

        [Fact]
        public void PolyPolyWithin_is_true_when_some_segments_come_closer_than_d()
        {
            var a = new[] { new Pt(0, 0), new Pt(10, 0) };
            var b = new[] { new Pt(0, 7), new Pt(5, 4), new Pt(10, 7) };
            Assert.True(Geo.PolyPolyWithin(a, b, 4.5));
            Assert.False(Geo.PolyPolyWithin(a, b, 4));
        }

        [Fact]
        public void PolyPolyWithin_is_false_for_far_apart_paths() =>
            Assert.False(Geo.PolyPolyWithin(
                new[] { new Pt(0, 0), new Pt(10, 0) },
                new[] { new Pt(100, 100), new Pt(110, 100) }, 6));

        [Theory]
        [InlineData(-5, 5, 15, 5, true)]    // passes straight through
        [InlineData(-5, -5, -1, -1, false)] // stops short
        [InlineData(2, 2, 3, 3, true)]      // entirely inside
        [InlineData(-5, 0, 15, 0, true)]    // runs along the top edge
        [InlineData(-5, 11, 15, 11, false)] // runs just below
        public void SegRectHit(double ax, double ay, double bx, double by, bool hit) =>
            Assert.Equal(hit, Geo.SegRectHit(new Pt(ax, ay), new Pt(bx, by), new Rect(0, 0, 10, 10)));

        [Fact]
        public void BBox_pads_every_side()
        {
            var r = Geo.BBox(new[] { new Pt(1, 2), new Pt(5, -3) }, 1);
            Assert.Equal((0.0, -4.0, 6.0, 3.0), (r.X0, r.Y0, r.X1, r.Y1));
        }

        [Fact]
        public void Clamp_matches_the_prototype_when_the_range_is_empty()
        {
            Assert.Equal(5, Geo.Clamp(4, 5, 3));
            Assert.Equal(3, Geo.Clamp(7, 5, 3));
        }

        [Fact]
        public void Rect_intersects_when_edges_touch() =>
            Assert.True(new Rect(0, 0, 10, 10).Intersects(new Rect(10, 0, 20, 10)));

        [Fact]
        public void Cut_splits_an_interval_around_the_hole() =>
            Assert.Equal(new[] { (0.0, 3.0), (5.0, 10.0) },
                Intervals.Cut(new List<(double Lo, double Hi)> { (0, 10) }, 3, 5));

        [Fact]
        public void Cut_keeps_intervals_the_hole_misses_and_drops_covered_ones() =>
            Assert.Equal(new[] { (0.0, 2.0), (8.0, 9.0) },
                Intervals.Cut(new List<(double Lo, double Hi)> { (0, 2), (3, 4), (6, 9) }, 2, 8));
    }
}
