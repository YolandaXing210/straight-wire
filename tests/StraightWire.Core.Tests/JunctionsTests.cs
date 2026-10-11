using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class JunctionsTests
    {
        static Pt P(double x, double y) => new Pt(x, y);
        static readonly Pt[] Down = { P(0, 0), P(100, 0), P(100, 50) };
        static readonly Pt[] Up = { P(0, 0), P(100, 0), P(100, -50) };

        [Fact]
        public void Diverge_finds_where_two_paths_part()
        {
            var p = Junctions.Diverge(Down, Up).Value;
            Assert.Equal(100, p.X, 6);
            Assert.InRange(p.Y, 0, 1);
        }

        [Fact]
        public void Diverge_is_null_for_the_same_path() => Assert.Null(Junctions.Diverge(Down, Down));

        [Fact]
        public void Find_puts_one_dot_where_same_source_wires_split()
        {
            var dot = Assert.Single(Junctions.Find(new[] { ("g", true, Down), ("g", true, Up) }));
            Assert.Equal("g", dot.Group);
            Assert.Equal(100, dot.At.X, 6);
        }

        [Fact]
        public void Find_skips_a_split_right_at_the_port() =>
            Assert.Empty(Junctions.Find(new[]
            {
                ("g", true, new[] { P(0, 0), P(0, 50) }),
                ("g", true, new[] { P(0, 0), P(0, -50) }),
            }));

        [Fact]
        public void Find_skips_a_group_with_a_curve_wire() =>
            Assert.Empty(Junctions.Find(new[] { ("g", true, Down), ("g", false, (Pt[])null), ("g", true, Up) }));

        [Fact]
        public void Find_ignores_wires_from_different_sources() =>
            Assert.Empty(Junctions.Find(new[] { ("g1", true, Down), ("g2", true, Up) }));
    }
}
