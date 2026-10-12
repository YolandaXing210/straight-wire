using System;
using System.Collections.Generic;
using System.Linq;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class TidyTests
    {
        static TidyBlock B(string id, double x, double y, double w, double h) => new TidyBlock(id, Rect.FromXYWH(x, y, w, h));
        static TidyLink L(string s, double sy, string t, double ty) => new TidyLink(s, sy, t, ty);

        /// <summary>Where every block ends up: its new top-left, or where it already was.</summary>
        static Dictionary<string, Pt> Final(IEnumerable<TidyBlock> blocks, IReadOnlyDictionary<string, Pt> moved) =>
            blocks.ToDictionary(b => b.Id, b => moved.TryGetValue(b.Id, out var p) ? p : new Pt(b.Bounds.X0, b.Bounds.Y0));

        // A (0,0 100x40, out@20) -> B (50,200 100x60, in@30 out@30) -> C (300,100 120x40, in@20).
        static TidyBlock[] ChainBlocks(double dx = 0, double dy = 0) =>
            new[] { B("A", dx + 0, dy + 0, 100, 40), B("B", dx + 50, dy + 200, 100, 60), B("C", dx + 300, dy + 100, 120, 40) };
        static TidyLink[] ChainLinks(double dy = 0) =>
            new[] { L("A", dy + 20, "B", dy + 230), L("B", dy + 230, "C", dy + 120) };

        [Fact]
        public void A_chain_becomes_three_columns_with_straight_wires()
        {
            var moved = Tidy.Layout(ChainBlocks(), ChainLinks(), 10);
            Assert.Equal(new Pt(0, 10), moved["A"]);
            Assert.Equal(new Pt(164, 0), moved["B"]);
            Assert.Equal(new Pt(328, 10), moved["C"]);
        }

        [Fact]
        public void A_source_sits_right_aligned_in_the_column_before_what_it_feeds()
        {
            var blocks = new[] { B("A", 0, 0, 100, 40), B("B", 200, 0, 100, 40), B("C", 400, 0, 100, 40), B("S", 0, 300, 60, 20) };
            var links = new[] { L("A", 20, "B", 20), L("B", 20, "C", 20), L("S", 310, "C", 30) };
            var f = Final(blocks, Tidy.Layout(blocks, links, 10));
            Assert.Equal(f["B"].X + 40, f["S"].X);
            Assert.True(f["A"].X < f["S"].X && f["S"].X < f["C"].X);
        }

        [Fact]
        public void An_input_fed_by_a_component_wins_over_one_fed_by_a_source()
        {
            var blocks = new[] { B("X", 0, 0, 100, 40), B("A", 200, 0, 100, 40), B("B", 400, 100, 100, 60), B("S", 200, 300, 60, 20) };
            var links = new[] { L("X", 20, "A", 20), L("A", 20, "B", 140), L("S", 310, "B", 110) };
            var f = Final(blocks, Tidy.Layout(blocks, links, 10));
            Assert.Equal(f["A"].Y + 20, f["B"].Y + 40);
        }

        [Fact]
        public void Two_blocks_wanting_the_same_place_end_up_a_gap_apart_in_their_old_order()
        {
            var blocks = new[] { B("A", 0, 0, 100, 40), B("B1", 200, 0, 100, 40), B("B2", 200, 100, 100, 40) };
            var links = new[] { L("A", 20, "B1", 20), L("A", 20, "B2", 120) };
            var f = Final(blocks, Tidy.Layout(blocks, links, 10));
            Assert.Equal(f["A"].Y, f["B1"].Y);
            Assert.Equal(52, f["B2"].Y - f["B1"].Y);
        }

        [Fact]
        public void Ties_follow_the_current_top_to_bottom_order_not_the_ids()
        {
            var blocks = new[] { B("A", 0, 0, 100, 40), B("Bz", 200, 0, 100, 40), B("Ba", 200, 100, 100, 40) };
            var links = new[] { L("A", 20, "Ba", 120), L("A", 20, "Bz", 20) };
            var f = Final(blocks, Tidy.Layout(blocks, links, 10));
            Assert.True(f["Bz"].Y < f["Ba"].Y);
        }

        [Fact]
        public void Sources_stack_tightly_and_their_target_lines_up_with_the_top_one()
        {
            var blocks = new[] { B("S1", 0, 0, 60, 20), B("S2", 0, 200, 60, 20), B("C", 300, 0, 100, 40) };
            var links = new[] { L("S1", 10, "C", 10), L("S2", 210, "C", 30) };
            var moved = Tidy.Layout(blocks, links, 10);
            Assert.False(moved.ContainsKey("S1"));
            Assert.Equal(new Pt(0, 32), moved["S2"]);
            Assert.Equal(new Pt(124, 0), moved["C"]);
        }

        [Fact]
        public void The_column_gap_grows_with_the_number_of_output_grips()
        {
            var blocks = new[] { B("A", 0, 0, 100, 40), B("B1", 200, 0, 100, 40), B("B2", 200, 60, 100, 40), B("B3", 200, 120, 100, 40) };
            var links = new[] { L("A", 10, "B1", 20), L("A", 20, "B2", 80), L("A", 30, "B3", 140) };
            var f = Final(blocks, Tidy.Layout(blocks, links, 10));
            Assert.Equal(176, f["B1"].X);
        }

        [Fact]
        public void A_cycle_between_groups_drops_the_link_that_runs_backwards_now()
        {
            var right = new[] { B("G1", 0, 0, 100, 100), B("B", 300, 0, 100, 40) };
            var links = new[] { L("G1", 20, "B", 20), L("B", 20, "G1", 80) };
            var moved = Tidy.Layout(right, links, 10);
            Assert.False(moved.ContainsKey("G1"));
            Assert.Equal(new Pt(164, 0), moved["B"]);

            var left = new[] { B("G1", 0, 0, 100, 100), B("B", -300, 0, 100, 40) };
            var f = Final(left, Tidy.Layout(left, links, 10));
            Assert.True(f["B"].X < f["G1"].X);
        }

        [Fact]
        public void The_layout_keeps_its_old_top_left_and_moves_blocks_by_whole_units()
        {
            var blocks = ChainBlocks(1000.4, 500.6);
            var moved = Tidy.Layout(blocks, ChainLinks(500.6), 10);
            var f = Final(blocks, moved);
            Assert.Equal(1000.4, f.Values.Min(p => p.X), 6);
            Assert.Equal(500.6, f.Values.Min(p => p.Y), 6);
            Assert.Equal(164, f["B"].X - f["A"].X, 6);
            AssertWholeMoves(blocks, moved);
        }

        // Grasshopper floors positions to whole units, so a tidy that moved anything by a fraction
        // would find it "moved" again next time, and a group's members would drift apart.
        static void AssertWholeMoves(IEnumerable<TidyBlock> blocks, IReadOnlyDictionary<string, Pt> moved)
        {
            foreach (var b in blocks)
                if (moved.TryGetValue(b.Id, out var p))
                {
                    Assert.Equal(Math.Round(p.X - b.Bounds.X0), p.X - b.Bounds.X0, 9);
                    Assert.Equal(Math.Round(p.Y - b.Bounds.Y0), p.Y - b.Bounds.Y0, 9);
                }
        }

        /// <summary>Lays out, applies the result, and lays out again with the grips where they now are.</summary>
        static IReadOnlyDictionary<string, Pt> Twice(TidyBlock[] blocks, double spacing, params (string S, double Out, string T, double In)[] links)
        {
            TidyLink[] Links(Dictionary<string, TidyBlock> by) =>
                links.Select(l => L(l.S, by[l.S].Bounds.Y0 + l.Out, l.T, by[l.T].Bounds.Y0 + l.In)).ToArray();
            var first = Tidy.Layout(blocks, Links(blocks.ToDictionary(b => b.Id)), spacing);
            AssertWholeMoves(blocks, first);
            var f = Final(blocks, first);
            var again = blocks.Select(b => B(b.Id, f[b.Id].X, f[b.Id].Y, b.Bounds.X1 - b.Bounds.X0, b.Bounds.Y1 - b.Bounds.Y0)).ToArray();
            return Tidy.Layout(again, Links(again.ToDictionary(b => b.Id)), spacing);
        }

        [Fact]
        public void A_fractional_column_gap_still_moves_by_whole_units_and_settles()
        {
            // Spacing 8 with three output grips makes a 68.8 gap.
            var blocks = new[] { B("A", 0, 0, 100, 60), B("B1", 200, 0, 100, 40), B("B2", 200, 100, 100, 40), B("B3", 200, 200, 100, 40), B("C", 400, 0, 100, 80) };
            Assert.Empty(Twice(blocks, 8, ("A", 10, "B1", 20), ("A", 30, "B2", 20), ("A", 50, "B3", 20),
                                          ("B1", 20, "C", 20), ("B2", 20, "C", 40), ("B3", 20, "C", 60)));
        }

        [Fact]
        public void Fractional_port_offsets_still_move_by_whole_units_and_settle()
        {
            var blocks = new[] { B("S", 0, 0, 60, 20), B("S2", 0, 100, 60, 20), B("C", 300, 0, 100, 80) };
            Assert.Empty(Twice(blocks, 10, ("S", 10, "C", 80.0 / 3), ("S2", 10, "C", 160.0 / 3)));
        }

        [Fact]
        public void Unlinked_blocks_never_move_and_a_tidy_layout_stays_put()
        {
            var blocks = ChainBlocks().Append(B("Lone", 900, 900, 50, 50)).ToArray();
            var moved = Tidy.Layout(blocks, ChainLinks(), 10);
            Assert.False(moved.ContainsKey("Lone"));

            var f = Final(blocks, moved);
            var again = blocks.Select(b => B(b.Id, f[b.Id].X, f[b.Id].Y, b.Bounds.X1 - b.Bounds.X0, b.Bounds.Y1 - b.Bounds.Y0)).ToArray();
            var links = new[] { L("A", f["A"].Y + 20, "B", f["B"].Y + 30), L("B", f["B"].Y + 30, "C", f["C"].Y + 20) };
            Assert.Empty(Tidy.Layout(again, links, 10));
        }

        [Fact]
        public void Input_order_does_not_change_the_result()
        {
            var a = Tidy.Layout(ChainBlocks(), ChainLinks(), 10);
            var b = Tidy.Layout(ChainBlocks().Reverse().ToArray(), ChainLinks().Reverse().ToArray(), 10);
            Assert.Equal(a.OrderBy(kv => kv.Key), b.OrderBy(kv => kv.Key));
        }

        [Fact]
        public void Links_to_unknown_blocks_and_to_themselves_are_ignored()
        {
            var links = ChainLinks().Append(L("A", 20, "Nowhere", 0)).Append(L("C", 120, "C", 110)).ToArray();
            Assert.Equal(Tidy.Layout(ChainBlocks(), ChainLinks(), 10).OrderBy(kv => kv.Key),
                         Tidy.Layout(ChainBlocks(), links, 10).OrderBy(kv => kv.Key));
        }
    }
}
