using System;
using System.Collections.Generic;
using System.Linq;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class TidyScopeTests
    {
        /// <summary>A canvas described in a few lines: 100x40 objects, groups boxed 10 around their members, wires grip to grip.</summary>
        sealed class Canvas
        {
            public readonly List<TidyObject> Objects = new List<TidyObject>();
            public readonly List<TidyGroup> Groups = new List<TidyGroup>();
            public readonly List<TidyWire> Wires = new List<TidyWire>();

            public Canvas O(string id, double x, double y, TidyKind kind = TidyKind.Active)
            {
                Objects.Add(new TidyObject(id, Rect.FromXYWH(x, y, 100, 40), kind));
                return this;
            }

            /// <summary>Members are listed recursively, nested groups and their members included.</summary>
            public Canvas G(string id, bool selected, params string[] members)
            {
                var rects = members.Select(m => Objects.FirstOrDefault(o => o.Id == m)?.Bounds ?? Groups.First(g => g.Id == m).Bounds).ToList();
                var box = new Rect(rects.Min(r => r.X0), rects.Min(r => r.Y0), rects.Max(r => r.X1), rects.Max(r => r.Y1)).Inflate(10);
                Groups.Add(new TidyGroup(id, box, members, selected));
                return this;
            }

            public Canvas W(string source, string target)
            {
                var s = Objects.First(o => o.Id == source).Bounds;
                var t = Objects.First(o => o.Id == target).Bounds;
                Wires.Add(new TidyWire(source + ">" + target, source, s.Y0 + 20, target, t.Y0 + 20));
                return this;
            }

            public TidyPlan Build(string[] selected = null, string[] wires = null) =>
                TidyScope.Build(Objects, Groups, Wires, selected ?? Array.Empty<string>(), wires ?? Array.Empty<string>());

            public Rect Box(string group) => Groups.First(g => g.Id == group).Bounds;
        }

        static string[] Ids(TidyPlan plan) => plan.Blocks.Select(b => b.Id).ToArray();

        [Fact]
        public void Tidy_all_makes_a_block_per_loose_object_and_per_top_level_group()
        {
            var c = new Canvas().O("A", 0, 0).O("B", 200, 0).O("C", 400, 0).G("G", false, "B", "C").W("A", "B").W("B", "C");
            var plan = c.Build();
            Assert.False(plan.Selection);
            Assert.Equal(new[] { "A", "G" }, Ids(plan));
            Assert.Equal(new[] { "B", "C", "G" }, plan.Members["G"]);
            var link = Assert.Single(plan.Links);
            Assert.Equal(("A", 20.0, "G", 20.0), (link.Source, link.SourceY, link.Target, link.TargetY));
        }

        [Fact]
        public void A_selected_member_of_an_unselected_group_is_its_own_block()
        {
            var c = new Canvas().O("A", 0, 0).O("B", 200, 0).O("C", 400, 0).G("G", false, "B", "C").W("A", "B");
            var plan = c.Build(selected: new[] { "A", "B" });
            Assert.True(plan.Selection);
            Assert.Equal(new[] { "A", "B" }, Ids(plan));
            Assert.Equal(new[] { "B" }, plan.Members["B"]);
        }

        [Fact]
        public void A_selected_group_is_one_block()
        {
            var c = new Canvas().O("A", 0, 0).O("B", 200, 0).O("C", 400, 0).G("G", true, "B", "C").W("A", "B");
            var plan = c.Build(selected: new[] { "A" });
            Assert.True(plan.Selection);
            Assert.Equal(new[] { "A", "G" }, Ids(plan));
        }

        [Fact]
        public void Groups_sharing_an_object_merge()
        {
            var c = new Canvas().O("B", 0, 0).O("C", 200, 0).O("D", 400, 0).G("G1", false, "B", "C").G("G2", false, "C", "D");
            var plan = c.Build();
            var block = Assert.Single(plan.Blocks);
            Assert.Equal("G1", block.Id);
            Assert.Equal(new[] { "B", "C", "D", "G1", "G2" }, plan.Members["G1"]);
            Rect g1 = c.Box("G1"), g2 = c.Box("G2");
            Assert.Equal((Math.Min(g1.X0, g2.X0), Math.Min(g1.Y0, g2.Y0), Math.Max(g1.X1, g2.X1), Math.Max(g1.Y1, g2.Y1)),
                         (block.Bounds.X0, block.Bounds.Y0, block.Bounds.X1, block.Bounds.Y1));
        }

        [Fact]
        public void Nested_groups_belong_to_their_top_level_group()
        {
            var c = new Canvas().O("B", 0, 0).O("C", 200, 0).G("G2", false, "C").G("G1", false, "B", "G2", "C");
            var plan = c.Build();
            Assert.Equal(new[] { "G1" }, Ids(plan));
            Assert.Equal(new[] { "B", "C", "G1", "G2" }, plan.Members["G1"]);
        }

        [Fact]
        public void The_marker_and_loose_annotations_never_move_but_grouped_annotations_do()
        {
            var c = new Canvas().O("A", 0, 0).O("B", 200, 0).O("M", -200, -200, TidyKind.Marker)
                                .O("S1", 0, 300, TidyKind.Annotation).O("S2", 200, 100, TidyKind.Annotation)
                                .G("G", false, "B", "S2", "M").W("A", "B");
            var plan = c.Build();
            Assert.Equal(new[] { "A", "G" }, Ids(plan));
            Assert.Equal(new[] { "B", "G", "S2" }, plan.Members["G"]);
            Assert.False(plan.BlockOf.ContainsKey("M"));
            Assert.False(plan.BlockOf.ContainsKey("S1"));
        }

        [Fact]
        public void Selected_wire_ends_count_as_selected()
        {
            var c = new Canvas().O("A", 0, 0).O("B", 200, 0).O("C", 400, 0).W("A", "B").W("B", "C");
            var plan = c.Build(wires: new[] { "A>B" });
            Assert.True(plan.Selection);
            Assert.Equal(new[] { "A", "B" }, Ids(plan));
        }

        [Fact]
        public void Selected_wire_end_inside_selected_group_maps_to_the_group()
        {
            var c = new Canvas().O("A", 0, 0).O("B", 200, 0).O("C", 400, 0).G("G", true, "B", "C").W("A", "B");
            var plan = c.Build(wires: new[] { "A>B" });
            Assert.True(plan.Selection);
            Assert.Equal(new[] { "A", "G" }, Ids(plan));
            Assert.Equal("G", plan.BlockOf["B"]);
        }

        [Fact]
        public void One_selected_block_falls_back_to_tidy_all()
        {
            var c = new Canvas().O("A", 0, 0).O("B", 200, 0).W("A", "B");
            var plan = c.Build(selected: new[] { "A" });
            Assert.False(plan.Selection);
            Assert.Equal(new[] { "A", "B" }, Ids(plan));
        }

        [Fact]
        public void Wires_inside_a_block_or_leaving_the_scope_make_no_link()
        {
            var inside = new Canvas().O("A", 0, 0).O("B", 200, 0).O("C", 400, 0).G("G", false, "B", "C").W("B", "C");
            Assert.Empty(inside.Build().Links);

            var leaving = new Canvas().O("A", 0, 0).O("D", 200, 0).O("X", 400, 0).W("A", "X").W("A", "D");
            var plan = leaving.Build(selected: new[] { "A", "D" });
            var link = Assert.Single(plan.Links);
            Assert.Equal(("A", "D"), (link.Source, link.Target));
        }

        [Fact]
        public void Unknown_ids_are_ignored()
        {
            var c = new Canvas().O("A", 0, 0).O("B", 200, 0).W("A", "B");
            c.Groups.Add(new TidyGroup("G", Rect.FromXYWH(0, 0, 10, 10), new[] { "Gone" }, false));
            c.Wires.Add(new TidyWire("A>Gone", "A", 20, "Gone", 20));
            var plan = c.Build(selected: new[] { "Gone", "A" }, wires: new[] { "Nope" });
            Assert.False(plan.Selection);
            Assert.Single(plan.Links);
        }

        [Fact]
        public void Only_wires_between_two_moved_blocks_are_reset()
        {
            var c = new Canvas().O("A", 0, 0).O("B", 200, 0).O("C", 400, 0).O("D", 600, 0)
                                .G("G", false, "B", "C").W("A", "B").W("B", "C").W("C", "D");
            var plan = c.Build();
            Assert.Equal(new[] { "A>B" }, TidyScope.WiresToReset(plan, new[] { "A", "G" }, c.Wires));
        }
    }
}
