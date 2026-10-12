using System;
using System.Collections.Generic;
using System.Linq;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class RelaySplitTests
    {
        readonly Pt[] path = { new Pt(0,0), new Pt(20,0), new Pt(20,50), new Pt(100,50) };
        [Fact]
        public void Split_preserves_unrelated_settings_and_rekeys_only_selected_connection()
        {
            var s = new FileSettings(WireStyle.Diagonal, 16)
                .WithOverride(new[] { "old", "other" }, WireStyle.Orthogonal)
                .WithLane("other", .7)
                .WithPoints("old", new ManualPoints(path.Skip(1).Take(2).ToArray(), path[0], path[3]));
            var n = RelaySplit.Apply(s, "old", "a", "b", path, 1, path[0], new Pt(20,25), path[3]);
            Assert.Equal(s.Mode, n.Mode); Assert.Equal(s.Spacing, n.Spacing);
            Assert.Equal(WireStyle.Orthogonal, n.Overrides["a"]); Assert.Equal(n.Overrides["a"], n.Overrides["b"]);
            Assert.Equal(s.Overrides["other"], n.Overrides["other"]); Assert.Equal(.7, n.Lanes["other"]);
            Assert.False(n.Points.ContainsKey("old")); Assert.False(n.Overrides.ContainsKey("old"));
            Assert.Equal(new[] { path[1] }, n.Points["a"].Points);
            Assert.Equal(new[] { path[2] }, n.Points["b"].Points);
            Assert.Equal(n.Points["a"].TargetAt, n.Points["b"].SourceAt);
            Assert.True(s.Points.ContainsKey("old"));
        }
        [Fact]
        public void Fixed_lane_becomes_split_manual_paths_instead_of_copying_relative_fraction()
        {
            var s = new FileSettings(WireStyle.Orthogonal, 10).WithLane("old", .2);
            var n = RelaySplit.Apply(s, "old", "a", "b", path, 1, path[0], new Pt(20,25), path[3]);
            Assert.Empty(n.Lanes); Assert.Equal(2, n.Points.Count); Assert.Empty(n.Overrides);
        }
        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(2)]
        public void Every_existing_bend_survives_once_in_order(int segment)
        {
            var s = new FileSettings(WireStyle.Orthogonal, 10).WithPoints("old", new ManualPoints(path.Skip(1).Take(2).ToArray(),path[0],path[3]));
            var n = RelaySplit.Apply(s,"old","a","b",path,segment,path[0],new Pt(20,25),path[3]);
            var all = new[] { "a", "b" }.Where(n.Points.ContainsKey).SelectMany(k=>n.Points[k].Points);
            Assert.Equal(path.Skip(1).Take(2), all);
            Assert.All(n.Points.Values, p=>Assert.NotEmpty(p.Points));
        }
        [Fact]
        public void Automatic_unedited_wire_stays_automatic()
        {
            var n = RelaySplit.Apply(new FileSettings(WireStyle.Diagonal,10),"old","a","b",path,1,path[0],path[1],path[3]);
            Assert.Empty(n.Points); Assert.Empty(n.Lanes); Assert.Empty(n.Overrides);
        }
        [Fact]
        public void Invalid_split_is_rejected_before_mutation()
        {
            Assert.Throws<ArgumentOutOfRangeException>(()=>RelaySplit.Apply(new FileSettings(WireStyle.Curve,10),"o","a","b",path,3,path[0],path[1],path[3]));
        }
    }
}
