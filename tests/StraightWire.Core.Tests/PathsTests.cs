using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class PathsTests
    {
        static Pt P(double x, double y) => new Pt(x, y);

        [Fact]
        public void Dedupe_drops_points_within_a_hundredth() =>
            Assert.Equal(new[] { P(0, 0), P(10, 0) }, Paths.Dedupe(new[] { P(0, 0), P(0.005, 0), P(10, 0) }));

        [Fact]
        public void Dedupe_keeps_the_exact_last_point() =>
            Assert.Equal(new[] { P(0, 0), P(10.005, 0) }, Paths.Dedupe(new[] { P(0, 0), P(10, 0), P(10.005, 0) }));

        [Fact]
        public void Dedupe_of_one_repeated_point_gives_two_points() =>
            Assert.Equal(new[] { P(1, 1), P(1, 1) }, Paths.Dedupe(new[] { P(1, 1), P(1, 1) }));

        [Fact]
        public void Simplify_merges_collinear_runs() =>
            Assert.Equal(new[] { P(0, 0), P(10, 0), P(10, 5) }, Paths.Simplify(new[] { P(0, 0), P(5, 0), P(10, 0), P(10, 5) }));

        [Fact]
        public void Simplify_keeps_a_reversal() =>
            Assert.Equal(new[] { P(0, 0), P(10, 0), P(5, 0) }, Paths.Simplify(new[] { P(0, 0), P(10, 0), P(5, 0) }));

        [Fact]
        public void Chamfer_cuts_a_right_angle() =>
            Assert.Equal(new[] { P(0, 0), P(36, 0), P(50, 14), P(50, 50) },
                Paths.Chamfer(new[] { P(0, 0), P(50, 0), P(50, 50) }, 14));

        [Fact]
        public void Chamfer_leaves_a_45_degree_corner() =>
            Assert.Equal(new[] { P(0, 0), P(20, 0), P(40, 20) },
                Paths.Chamfer(new[] { P(0, 0), P(20, 0), P(40, 20) }, 14));

        [Fact]
        public void Chamfer_is_limited_by_short_legs() =>
            Assert.Equal(new[] { P(0, 0), P(8, 0), P(10, 2), P(12, 4), P(30, 4) },
                Paths.Dedupe(Paths.Chamfer(new[] { P(0, 0), P(10, 0), P(10, 4), P(30, 4) }, 14)));

        [Fact]
        public void Diagonal_with_a_half_height_cut_is_one_straight_diagonal() =>
            Assert.Equal(new[] { P(0, 0), P(30, 0), P(70, 40), P(100, 40) },
                Paths.Simplify(Paths.Diagonal(0, 0, 100, 40, 50, 20)));

        [Fact]
        public void Diagonal_with_a_small_cut_keeps_a_vertical_run() =>
            Assert.Equal(new[] { P(0, 0), P(36, 0), P(50, 14), P(50, 26), P(64, 40), P(100, 40) },
                Paths.Diagonal(0, 0, 100, 40, 50, 14));

        [Fact]
        public void Diagonal_cut_shrinks_near_a_port() =>
            Assert.Equal(new[] { P(0, 0), P(2, 0), P(10, 8), P(10, 32), P(18, 40), P(100, 40) },
                Paths.Diagonal(0, 0, 100, 40, 10, 20));

        [Fact]
        public void Diagonal_going_up() =>
            Assert.Equal(new[] { P(0, 40), P(30, 40), P(50, 20), P(70, 0), P(100, 0) },
                Paths.Diagonal(0, 40, 100, 0, 50, 20));

        [Fact]
        public void Lane_runs_out_down_and_in() =>
            Assert.Equal(new[] { P(0, 0), P(50, 0), P(50, 30), P(100, 30) }, Paths.Lane(P(0, 0), P(100, 30), 50));

        [Fact]
        public void Back_steps_out_crosses_at_y_and_steps_in() =>
            Assert.Equal(new[] { P(380, 230), P(398, 230), P(398, 130), P(82, 130), P(82, 30), P(100, 30) },
                Paths.Back(P(380, 230), P(100, 30), 130));
    }
}
