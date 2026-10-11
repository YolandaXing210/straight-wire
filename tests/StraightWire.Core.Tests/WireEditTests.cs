using System.Collections.Generic;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class WireEditTests
    {
        static Pt P(double x, double y) => new Pt(x, y);

        static readonly Pt[] Lane = { P(0, 0), P(100, 0), P(100, 100), P(200, 100) };
        static readonly Pt[] Other = { P(0, 50), P(200, 50) };

        static IEnumerable<KeyValuePair<string, Pt[]>> Paths() => new[]
        {
            new KeyValuePair<string, Pt[]>("lane", Lane),
            new KeyValuePair<string, Pt[]>("other", Other),
        };

        [Fact]
        public void HitWire_picks_the_nearest_wire_and_segment()
        {
            var hit = WireEdit.HitWire(Paths(), P(103, 30), 6).Value;
            Assert.Equal(("lane", 1), (hit.Id, hit.Segment));
            Assert.Equal(3, hit.Distance, 6);
            Assert.Equal(("other", 0), (WireEdit.HitWire(Paths(), P(60, 52), 6).Value.Id, WireEdit.HitWire(Paths(), P(60, 52), 6).Value.Segment));
        }

        [Fact]
        public void HitWire_misses_beyond_the_tolerance() => Assert.Null(WireEdit.HitWire(Paths(), P(150, 20), 6));

        [Fact]
        public void HitHandle_finds_interior_points_only()
        {
            Assert.Equal(1, WireEdit.HitHandle(Lane, P(102, 97), 7));
            Assert.Equal(0, WireEdit.HitHandle(Lane, P(99, 1), 7));
            Assert.Equal(-1, WireEdit.HitHandle(Lane, P(1, 1), 7));
            Assert.Equal(-1, WireEdit.HitHandle(Lane, P(150, 50), 7));
        }

        [Theory]
        [InlineData(true, false, 1, 4, SegmentAction.Lane)]
        [InlineData(true, false, 0, 4, SegmentAction.None)]
        [InlineData(true, false, 2, 4, SegmentAction.None)]
        [InlineData(true, false, 3, 6, SegmentAction.Lane)]
        [InlineData(false, true, 0, 3, SegmentAction.Segment)]
        [InlineData(false, true, 0, 2, SegmentAction.None)]
        [InlineData(false, false, 1, 6, SegmentAction.None)]
        public void ActionFor_follows_the_prototype(bool laneWire, bool manual, int segment, int length, SegmentAction expected) =>
            Assert.Equal(expected, WireEdit.ActionFor(laneWire, manual, segment, length));

        [Fact]
        public void PointsOf_is_the_interior_of_a_path() =>
            Assert.Equal(new[] { P(100, 0), P(100, 100) }, WireEdit.PointsOf(Lane));

        [Fact]
        public void Snap_lands_on_a_horizontal_line_through_a_neighbour() =>
            Assert.Equal(P(57, 0), WireEdit.Snap(P(57, 4), P(0, 0), P(300, 200), 8, 10, false));

        [Fact]
        public void Snap_lands_on_the_crossing_of_lines_from_both_neighbours() =>
            // horizontal through A (y = 0) meets the 45° line through B (200, 100): x = 100
            Assert.Equal(P(100, 0), WireEdit.Snap(P(103, 4), P(0, 0), P(200, 100), 8, 10, false));

        [Fact]
        public void Snap_falls_back_to_the_grid() =>
            Assert.Equal(P(60, 40), WireEdit.Snap(P(57, 43), P(0, 0), P(300, 200), 8, 10, false));

        [Fact]
        public void Snap_does_nothing_when_free() =>
            Assert.Equal(P(57.3, 4.2), WireEdit.Snap(P(57.3, 4.2), P(0, 0), P(300, 200), 8, 10, true));

        [Fact]
        public void MovePoint_replaces_one_point() =>
            Assert.Equal(new[] { P(1, 1), P(9, 9) }, WireEdit.MovePoint(new[] { P(1, 1), P(2, 2) }, 1, P(9, 9)));

        [Fact]
        public void SlideSegment_moves_both_ends_of_the_segment_perpendicular_only()
        {
            // manual path: (0,0) (100,0) (100,100) (200,100); segment 1 is the vertical (100,0)-(100,100)
            var pts = WireEdit.PointsOf(Lane);
            var slid = WireEdit.SlideSegment(Lane, pts, 1, 23, 40, 10, false);
            Assert.Equal(new[] { P(120, 0), P(120, 100) }, slid);
            Assert.Equal(new[] { P(123, 0), P(123, 100) }, WireEdit.SlideSegment(Lane, pts, 1, 23, 40, 10, true));
        }

        [Fact]
        public void SlideSegment_of_an_end_segment_moves_only_its_interior_point()
        {
            var pts = WireEdit.PointsOf(Lane);
            // segment 0 runs from the port (0,0) to (100,0): only pts[0] moves, vertically
            Assert.Equal(new[] { P(100, 30), P(100, 100) }, WireEdit.SlideSegment(Lane, pts, 0, 5, 27, 10, false));
        }

        [Fact]
        public void InsertPoint_puts_the_projection_at_the_segment_index() =>
            Assert.Equal(new[] { P(100, 0), P(100, 40), P(100, 100) }, WireEdit.InsertPoint(Lane, 1, P(104, 40)));

        [Fact]
        public void RemovePoint_of_the_last_point_leaves_none() =>
            Assert.Empty(WireEdit.RemovePoint(new[] { P(5, 5) }, 0));
    }
}
