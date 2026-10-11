using System.Linq;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class FrozenTests
    {
        // S feeds T1 and T2 (a shared trunk with one dot); U feeds V on its own.
        static Scene Layout(double t1Y = 280, bool withUV = true)
        {
            var b = new SceneBuilder().Node("S", -80, 80).Node("T1", 400, t1Y).Node("T2", 300, -120)
                                      .Node("U", -80, 500).Node("V", 400, 620);
            b.Wire("S", "T1", WireStyle.Orthogonal);
            b.Wire("S", "T2", WireStyle.Orthogonal);
            if (withUV) b.Wire("U", "V", WireStyle.Orthogonal);
            return b.Build();
        }

        [Fact]
        public void Nothing_moved_keeps_every_path_and_dot()
        {
            var cached = Router.Route(Layout(), null);
            var kept = Frozen.Keep(cached, Layout());
            Assert.Equal(cached.Paths.Keys.OrderBy(k => k), kept.Paths.Keys.OrderBy(k => k));
            Assert.Equal(cached.Junctions.Count, kept.Junctions.Count);
            Assert.Single(kept.Junctions);
        }

        [Fact]
        public void A_wire_whose_end_moved_is_dropped_with_its_groups_dots()
        {
            var cached = Router.Route(Layout(), null);
            var kept = Frozen.Keep(cached, Layout(t1Y: 300));
            Assert.False(kept.Paths.ContainsKey("w000"));
            Assert.True(kept.Paths.ContainsKey("w001"));
            Assert.True(kept.Paths.ContainsKey("w002"));
            Assert.Empty(kept.Junctions);
            Assert.Equal(cached.Paths["w002"], kept.Paths["w002"]);
        }

        [Fact]
        public void A_wire_no_longer_in_the_scene_is_dropped()
        {
            var cached = Router.Route(Layout(), null);
            Assert.False(Frozen.Keep(cached, Layout(withUV: false)).Paths.ContainsKey("w002"));
        }

        [Fact]
        public void Lane_fractions_pass_through()
        {
            var cached = Router.Route(Layout(), null);
            Assert.Equal(cached.LaneFractions["w002"], Frozen.Keep(cached, Layout(t1Y: 300)).LaneFractions["w002"]);
        }
    }
}
