using System.Collections.Generic;
using System.Linq;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class FileSettingsEditTests
    {
        static Pt P(double x, double y) => new Pt(x, y);

        static FileSettings Edited() => new FileSettings(WireStyle.Orthogonal, 12)
            .WithLane("a>b", 0.25)
            .WithPoints("c>d", new ManualPoints(new[] { P(10, 20), P(30, 40) }, P(0, 0), P(100, 50)))
            .WithBack("e>f", new BackRuns(@out: 40, cross: -12.5));

        [Fact]
        public void Edits_are_written_in_the_spec_format()
        {
            Assert.Equal("{\"v\":1,\"mode\":\"ortho\",\"spacing\":12,\"wires\":{},\"lanes\":{\"a>b\":0.25}," +
                         "\"points\":{\"c>d\":{\"at\":[0,0,100,50],\"p\":[10,20,30,40]}}," +
                         "\"backs\":{\"e>f\":{\"out\":40,\"cross\":-12.5}}}",
                SettingsJson.Serialize(Edited()));
        }

        [Fact]
        public void Edits_round_trip()
        {
            Assert.Equal(SettingsStatus.Ok, SettingsJson.Parse(SettingsJson.Serialize(Edited()), out var back));
            Assert.Equal(0.25, back.Lanes["a>b"]);
            var m = back.Points["c>d"];
            Assert.Equal(new[] { P(10, 20), P(30, 40) }, m.Points);
            Assert.Equal((P(0, 0), P(100, 50)), (m.SourceAt, m.TargetAt));
            var r = back.Backs["e>f"];
            Assert.Equal((40.0, (double?)null, -12.5), (r.Out.Value, r.In, r.Cross.Value));
        }

        [Theory]
        [InlineData("{\"out\":\"x\"}")]
        [InlineData("{}")]
        [InlineData("[1]")]
        [InlineData("12")]
        public void Unreadable_back_entries_are_ignored(string entry)
        {
            Assert.Equal(SettingsStatus.Ok, SettingsJson.Parse("{\"v\":1,\"mode\":\"diag\",\"backs\":{\"a>b\":" + entry + "}}", out var s));
            Assert.Empty(s.Backs);
        }

        [Fact]
        public void Readable_parts_of_a_back_entry_are_kept()
        {
            SettingsJson.Parse("{\"v\":1,\"mode\":\"diag\",\"backs\":{\"a>b\":{\"out\":\"x\",\"in\":20}}}", out var s);
            Assert.Equal((null, 20.0, null), (s.Backs["a>b"].Out, s.Backs["a>b"].In.Value, s.Backs["a>b"].Cross));
        }

        [Fact]
        public void Empty_back_runs_remove_the_entry()
        {
            Assert.False(Edited().WithBack("e>f", new BackRuns()).Backs.ContainsKey("e>f"));
            Assert.False(Edited().WithBack("e>f", null).Backs.ContainsKey("e>f"));
        }

        [Fact]
        public void Lane_fractions_are_clamped_to_the_range()
        {
            SettingsJson.Parse("{\"v\":1,\"mode\":\"diag\",\"lanes\":{\"a>b\":1.7,\"c>d\":-3}}", out var s);
            Assert.Equal(1, s.Lanes["a>b"]);
            Assert.Equal(0, s.Lanes["c>d"]);
            Assert.Equal(0, new FileSettings(WireStyle.Diagonal, 10).WithLane("x>y", -0.2).Lanes["x>y"]);
        }

        [Theory]
        [InlineData("{\"at\":[0,0,1,1],\"p\":[10,20,30]}")]
        [InlineData("{\"at\":[0,0,1,1],\"p\":[]}")]
        [InlineData("{\"at\":[0,\"x\",1,1],\"p\":[10,20]}")]
        [InlineData("{\"at\":[0,0,1],\"p\":[10,20]}")]
        [InlineData("[10,20]")]
        public void Unreadable_point_entries_are_ignored(string entry)
        {
            Assert.Equal(SettingsStatus.Ok, SettingsJson.Parse("{\"v\":1,\"mode\":\"diag\",\"points\":{\"a>b\":" + entry + "}}", out var s));
            Assert.Empty(s.Points);
        }

        [Fact]
        public void Pruning_drops_edits_of_wires_that_are_gone()
        {
            var s = Edited().Pruned(new HashSet<string> { "a>b" });
            Assert.Equal(new[] { "a>b" }, s.Lanes.Keys);
            Assert.Empty(s.Points);
            Assert.Empty(s.Backs);
        }

        [Fact]
        public void WithoutEdits_clears_lanes_points_and_back_runs_but_keeps_styles()
        {
            var s = Edited().WithOverride(new[] { "a>b" }, WireStyle.Curve).WithoutEdits(new[] { "a>b", "c>d", "e>f" });
            Assert.Empty(s.Lanes);
            Assert.Empty(s.Points);
            Assert.Empty(s.Backs);
            Assert.Equal(WireStyle.Curve, s.StyleOf("a>b"));
        }

        [Fact]
        public void Every_other_change_keeps_the_edits()
        {
            var s = Edited().WithMode(WireStyle.Diagonal).WithSpacing(16).WithOverride(new[] { "q>r" }, WireStyle.Curve).WithoutOverride(new[] { "q>r" })
                            .WithLane("s>t", 0.5).WithPoints("u>v", new ManualPoints(new[] { P(1, 1) }, P(0, 0), P(0, 0)));
            Assert.Equal(0.25, s.Lanes["a>b"]);
            Assert.True(s.Points.ContainsKey("c>d"));
            Assert.Equal(40, s.Backs["e>f"].Out);
        }

        [Fact]
        public void An_empty_point_list_removes_the_hand_drawn_entry()
        {
            var s = Edited().WithPoints("c>d", new ManualPoints(new Pt[0], P(0, 0), P(0, 0)));
            Assert.False(s.Points.ContainsKey("c>d"));
        }

        [Fact]
        public void ManualPoints_shift_and_reanchor()
        {
            var m = new ManualPoints(new[] { P(10, 20) }, P(0, 0), P(100, 50));
            var moved = m.Shifted(5, -5);
            Assert.Equal(new[] { P(15, 15) }, moved.Points);
            Assert.Equal((P(5, -5), P(105, 45)), (moved.SourceAt, moved.TargetAt));
            var re = m.WithAnchors(P(1, 1), P(2, 2));
            Assert.Equal(new[] { P(10, 20) }, re.Points);
            Assert.Equal((P(1, 1), P(2, 2)), (re.SourceAt, re.TargetAt));
        }
    }
}
