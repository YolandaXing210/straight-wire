using System.Collections.Generic;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class FileSettingsTests
    {
        static FileSettings Sample() => new FileSettings(WireStyle.Orthogonal, 12,
            new Dictionary<string, WireStyle> { ["b>c"] = WireStyle.Curve, ["a>b"] = WireStyle.Diagonal });

        [Fact]
        public void Serialize_then_parse_round_trips()
        {
            Assert.Equal(SettingsStatus.Ok, SettingsJson.Parse(SettingsJson.Serialize(Sample()), out var back));
            Assert.Equal(WireStyle.Orthogonal, back.Mode);
            Assert.Equal(12, back.Spacing);
            Assert.Equal(2, back.Overrides.Count);
            Assert.Equal(WireStyle.Curve, back.StyleOf("b>c"));
            Assert.Equal(WireStyle.Diagonal, back.StyleOf("a>b"));
        }

        [Fact]
        public void Serialize_writes_the_spec_format_with_overrides_sorted() =>
            Assert.Equal("{\"v\":1,\"mode\":\"ortho\",\"spacing\":12,\"wires\":{\"a>b\":\"diag\",\"b>c\":\"bezier\"}}",
                SettingsJson.Serialize(Sample()));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Missing_when_the_file_has_no_settings(string json)
        {
            Assert.Equal(SettingsStatus.Missing, SettingsJson.Parse(json, out var s));
            Assert.Null(s);
        }

        // Review focus 3: settings this plugin cannot read.
        [Theory]
        [InlineData("{broken")]
        [InlineData("[]")]
        [InlineData("{\"mode\":\"ortho\"}")]
        [InlineData("{\"v\":1,\"mode\":\"zigzag\"}")]
        [InlineData("{\"v\":1,\"mode\":\"ortho\",\"spacing\":\"wide\"}")]
        [InlineData("{\"v\":0,\"mode\":\"ortho\"}")]
        public void Broken_for_text_that_is_not_our_settings(string json)
        {
            Assert.Equal(SettingsStatus.Broken, SettingsJson.Parse(json, out var s));
            Assert.Null(s);
        }

        // Review focus 3: written by a newer plugin. Never parsed further, never overwritten.
        [Fact]
        public void TooNew_for_a_newer_version_whatever_else_it_says()
        {
            Assert.Equal(SettingsStatus.TooNew, SettingsJson.Parse("{\"v\":2,\"mode\":\"spiral\"}", out var s));
            Assert.Null(s);
        }

        [Fact]
        public void Overrides_with_unknown_styles_are_dropped()
        {
            SettingsJson.Parse("{\"v\":1,\"mode\":\"diag\",\"spacing\":10,\"wires\":{\"a>b\":\"zigzag\",\"c>d\":\"ortho\"}}", out var s);
            Assert.Equal(new[] { "c>d" }, s.Overrides.Keys);
        }

        [Theory]
        [InlineData(50, 20)]
        [InlineData(2, 6)]
        [InlineData(12.5, 12.5)]
        public void Spacing_is_clamped_to_the_supported_range(double written, double read)
        {
            SettingsJson.Parse("{\"v\":1,\"mode\":\"diag\",\"spacing\":" + written.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}", out var s);
            Assert.Equal(read, s.Spacing);
        }

        [Fact]
        public void StyleOf_prefers_the_wire_override()
        {
            Assert.Equal(WireStyle.Curve, Sample().StyleOf("b>c"));
            Assert.Equal(WireStyle.Orthogonal, Sample().StyleOf("x>y"));
        }

        [Fact]
        public void Changes_return_new_settings_and_leave_the_original_alone()
        {
            var s = Sample();
            var t = s.WithOverride(new[] { "x>y" }, WireStyle.Curve).WithoutOverride(new[] { "a>b" }).WithMode(WireStyle.Diagonal).WithSpacing(16);
            Assert.Equal(WireStyle.Curve, t.StyleOf("x>y"));
            Assert.Equal(WireStyle.Diagonal, t.StyleOf("a>b"));
            Assert.Equal(16, t.Spacing);
            Assert.Equal(2, s.Overrides.Count);
            Assert.False(s.Overrides.ContainsKey("x>y"));
            Assert.Equal(WireStyle.Orthogonal, s.Mode);
        }

        // Review focus 3: overrides for wires that were deleted.
        [Fact]
        public void Pruned_drops_overrides_for_wires_that_are_gone() =>
            Assert.Equal(new[] { "a>b" }, Sample().Pruned(new HashSet<string> { "a>b", "q>r" }).Overrides.Keys);

        [Fact]
        public void Summary_reads_like_the_marker_message()
        {
            Assert.Equal("45° · 10", StyleNames.Summary(new FileSettings(WireStyle.Diagonal, 10)));
            Assert.Equal("Orthogonal · 12.5", StyleNames.Summary(new FileSettings(WireStyle.Orthogonal, 12.5)));
        }
    }
}
