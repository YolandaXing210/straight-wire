# Straight Wire Phase 3 (Tidy) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans (the user chose inline execution). Steps use checkbox (`- [ ]`) syntax.

**Goal:** One command lays out a Grasshopper definition, or a selection of it, in data-flow columns with straight wires. Groups move as blocks, and the whole tidy is one undo step.

**Architecture:**
- **Core (pure, unit-tested):**
  - `Tidy.Layout`: the prototype's column layout over blocks and links.
  - `TidyScope.Build`: turns objects, groups, wires and the selection into blocks and links.
- **Plugin:**
  - `Tidier`: reads the document, runs both, clears the wire edits the move invalidates, moves pivots, and pushes one undo record.
  - Triggers: a menu item, Alt+T, and a widget button.

**Tech stack:** as phases 1 and 2. Core is net7.0 (tests net10.0, xUnit); the plugin is net7.0 against Grasshopper 8.

**Spec:** `docs/superpowers/specs/2026-10-11-straight-wire-phase3-design.md`. Phase 1 and 2 specs and plans still hold for everything not changed here.

## Global Constraints

Everything in phases 1 and 2 still applies: paths, GUIDs, the commit trailer, MCP verification, never `yak push`, settings JSON `"v":1`. In addition:
- **Layout constants:**
  - vertical gap VG = 12
  - column gap `max(64, 40 + P · SP · 1.2)`; SP is the file's effective spacing
  - anchor rounded to whole units
- **Undo names:** "Tidy" for the tidy step.
- **Command line messages:** "Straight Wire: nothing to tidy." and "Straight Wire: already tidy."
- **UI text:**
  - menu labels "Tidy All" / "Tidy Selected"
  - widget button "Tidy"
  - shortcut Alt+T (Option+T on a Mac)
- **Grasshopper APIs:**
  - **Confirmed from IL this session:**
    - `GH_UndoUtil.CreatePivotEvent(string, <list of IGH_DocumentObject>) → GH_UndoRecord` (GH_DragInteraction calls it with "Drag")
    - `GH_PivotAction`
  - **To confirm at the start of Task 3** with the metadata inspector in the scratchpad (`inspect/`):
    - the exact parameter type of `CreatePivotEvent`
    - `GH_UndoRecord.AddAction`
    - `GH_UndoServer.PushUndoRecord(GH_UndoRecord)`
    - `GH_Group.ObjectIDs`
- **Rhino checks:** they need the Rhino MCP. If it is not listening, ask the user to run `mcpstart` in Rhino.

## Review Focus

1. **Tidy while a drag or a wire edit is in progress:** nothing moves, and nothing is recorded. Covered by Task 3's Rhino check (`drag:on`, then tidy).
2. **A hand-drawn wire inside a group block:** its points move with the group and are not cleared. Covered by Task 2 (`WiresToReset`) and Task 3's Rhino check.
3. **Undo and Redo of a tidy that also cleared edits:** every pivot and the settings JSON go back exactly, then forward again. Covered by Task 3's Rhino check.
4. **A selected wire whose end is inside a selected group:** the end maps to the group block, not a separate object block. Covered by Task 2's test `Selected_wire_end_inside_selected_group_maps_to_the_group`.
5. **Tidying an already tidy file:** no undo step, and "already tidy" on the command line. Covered by Task 1 (idempotence test) and Task 3's Rhino check.

---

### Task 1: Column layout (Core)

**Files:**
- Create: `src/StraightWire.Core/Tidy.cs`
- Test: `tests/StraightWire.Core.Tests/TidyTests.cs`; the performance test goes in `TidyPerformanceTests.cs` in the existing `"Performance"` collection.

**Interfaces produced:**
```csharp
public sealed class TidyBlock { public TidyBlock(string id, Rect bounds); public string Id { get; } public Rect Bounds { get; } }
public sealed class TidyLink  { public TidyLink(string source, double sourceY, string target, double targetY);
                                public string Source { get; } public double SourceY { get; } public string Target { get; } public double TargetY { get; } }
public static class Tidy
{
    public const double Gap = 12;
    /// New top-left per block that moves. Blocks without links, and blocks already in place, are absent.
    public static IReadOnlyDictionary<string, Pt> Layout(IReadOnlyList<TidyBlock> blocks, IReadOnlyList<TidyLink> links, double spacing);
}
```
`SourceY` and `TargetY` are absolute grip y values. A port offset is a grip y minus its block's `Bounds.Y0`.

- [ ] **Step 1: Write the failing tests**

```csharp
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
        public void The_layout_keeps_its_old_top_left_rounded_to_whole_units()
        {
            var blocks = ChainBlocks(1000.4, 500.6);
            var f = Final(blocks, Tidy.Layout(blocks, ChainLinks(500.6), 10));
            Assert.Equal(1000, f.Values.Min(p => p.X), 6);
            Assert.Equal(501, f.Values.Min(p => p.Y), 6);
            Assert.Equal(164, f["B"].X - f["A"].X, 6);
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
```

`TidyPerformanceTests.cs`, in `[Collection("Performance")]`:
- 500 blocks of 100×40, `Random(42)` positions in 0–5000.
- 800 links, each from the lower index to the higher; skip pairs with equal indices.
- Grip y values are `top + 20` (source) and `top + 10 + 10·(k % 3)` (target).
- Warm up twice, then take the median of 5 runs. Assert under 100 ms.

- [ ] **Step 2: Run, expect a compile failure** (`Tidy` doesn't exist)

Run: `dotnet test tests/StraightWire.Core.Tests --filter "FullyQualifiedName~Tidy" --nologo -v q`

- [ ] **Step 3: Implement `Tidy.cs`** following spec 4.4 exactly. Notes:
  - **Inputs:**
    - Keep links whose source and target are both known blocks and differ.
    - Linked blocks are the participants. Everything below works on participants only.
  - **Visit order:** sort participants by `(Bounds.X0, Bounds.Y0, Id)` with ordinal ids. Sort each block's outgoing links by `(target visit index, SourceY, TargetY)`.
  - **Cycles:** an iterative DFS over outgoing links in visit order marks back edges, i.e. a target on the current stack. Drop them from every later step.
  - **Depth:** longest path from blocks with no incoming kept links, via Kahn's algorithm in visit order.
  - **Sources:**
    - A source is a block with no incoming kept link.
    - A source with outgoing links gets `max(0, min(target depth) − 1)`.
  - **Columns:** non-empty depths in ascending order.
    - Column width is the widest block.
    - A source's x is `colX + width − w`; any other block's x is `colX`.
    - Then `colX += width + max(64, 40 + P·spacing·1.2)`, where P is the number of distinct `(block, SourceY − Y0)` output grips in the column with outgoing kept links.
  - **Initial y:** every block starts at its current `Y0`. In each column, sort the sources by `(Y0, Id)`; the first keeps its y, and each next one is `prev + h + Gap`.
  - **Port offsets:** `out = SourceY − source.Y0` and `in = TargetY − target.Y0`, both measured on the input bounds.
  - **Passes** (inputs, sources, inputs), each over the columns left to right:
    - **Inputs:** for a non-source block, pick the incoming link with the smallest `in` whose source is not a source block, ties by source id. If there is none, the smallest `in` overall. It wants `y(source) + out − in`.
    - **Sources:** for a source with outgoing links, take the links at its smallest `out`, and pick the one whose target has the smallest working y, then target id. It wants `y(target) + in − out`.
    - Blocks without a wish keep their working y.
    - **Resolve:** sort the column by `(wanted, working y before the pass, Id)`. Each block's y is `max(wanted, previous bottom + Gap)`.
  - **Anchor:**
    - `oldMin` is the minimum `(X0, Y0)` over participants.
    - `layoutMin` is the minimum of the computed `(x, y)`.
    - Final position = computed + `(round(oldMin.X) − layoutMin.X, round(oldMin.Y) − layoutMin.Y)`.
  - **Result:** include a block only when its final position differs from `(X0, Y0)` by more than 1e-9.

- [ ] **Step 4: Run the Tidy tests, then the whole suite.** Expected: all pass, and the 170 existing tests stay green.

- [ ] **Step 5: Commit** "Tidy layout in Core".

### Task 2: Blocks and links from a canvas (Core)

**Files:**
- Create: `src/StraightWire.Core/TidyScope.cs`
- Test: `tests/StraightWire.Core.Tests/TidyScopeTests.cs`

**Interfaces produced:**
```csharp
public enum TidyKind { Active, Annotation, Marker }
public sealed class TidyObject { public TidyObject(string id, Rect bounds, TidyKind kind); Id; Bounds; Kind }
/// Members: every object and group inside, recursively (nested group ids included).
public sealed class TidyGroup  { public TidyGroup(string id, Rect bounds, IReadOnlyList<string> members, bool selected); Id; Bounds; Members; Selected }
/// Source and Target are top-level object ids; the y values are the grips'.
public sealed class TidyWire   { public TidyWire(string id, string source, double sourceY, string target, double targetY); Id; Source; SourceY; Target; TargetY }
public sealed class TidyPlan
{
    public bool Selection { get; }                                        // Tidy Selected (true) or Tidy All
    public IReadOnlyList<TidyBlock> Blocks { get; }                       // sorted by id
    public IReadOnlyList<TidyLink> Links { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Members { get; } // block id -> object and group ids that move with it, sorted
    public IReadOnlyDictionary<string, string> BlockOf { get; }           // object or group id -> block id
}
public static class TidyScope
{
    public static TidyPlan Build(IReadOnlyList<TidyObject> objects, IReadOnlyList<TidyGroup> groups, IReadOnlyList<TidyWire> wires,
                                 IReadOnlyCollection<string> selectedObjects, IReadOnlyCollection<string> selectedWires);
    /// Wires whose ends are in two different moved blocks (spec 4.6).
    public static List<string> WiresToReset(TidyPlan plan, IEnumerable<string> movedBlocks, IReadOnlyList<TidyWire> wires);
}
```

**Rules** (spec 4.2):
- **Group blocks:** groups merge with union-find when they share any member. A group block's id is the smallest group id in it, ordinal. Its bounds are the union of its groups' bounds. Its members are those groups plus all their members, minus Marker objects.
- **Selection:**
  - Selected = `selectedObjects` plus the source and target of each wire in `selectedWires`. Only Active objects count.
  - Selected groups count only when they are not inside another selected group.
  - Object blocks: selected Active objects not inside a selected group block.
  - Two or more blocks make the plan a selection (`Selection = true`). Otherwise build Tidy All.
- **Tidy All:**
  - Top-level groups are those not inside any other group's members; they are merged into group blocks.
  - Every Active object outside all groups is its own block.
- **Links:** one per wire whose source and target map through `BlockOf` to two different blocks of the plan.

- [ ] **Step 1: Write the failing tests** (`TidyScopeTests.cs`). Helpers:
  - `O(id, x, y, kind = Active)`: a 100×40 object
  - `G(id, selected, params members)`: bounds are the union of its member objects inflated by 10
  - `W(src, dst)`: id `src>dst`, ys of `top + 20`

```csharp
[Fact] public void Tidy_all_makes_a_block_per_loose_object_and_per_top_level_group()
// objects A, B, C; group G{B, C}; wires A>B, B>C -> Selection false; blocks A, G; Members[G] = G, B, C; one link A->G.

[Fact] public void A_selected_member_of_an_unselected_group_is_its_own_block()
// select A and B; G{B, C} unselected -> Selection true; blocks A, B.

[Fact] public void A_selected_group_is_one_block()
// G{B, C} selected plus A selected -> blocks A, G.

[Fact] public void Groups_sharing_an_object_merge()
// G1{B, C}, G2{C, D} -> one block "G1" with members B, C, D, G1, G2; bounds = union.

[Fact] public void Nested_groups_belong_to_their_top_level_group()
// G1 members {B, G2, C}, G2 members {C} -> block G1 only; Members[G1] = B, C, G1, G2.

[Fact] public void The_marker_and_loose_annotations_never_move_but_grouped_annotations_do()
// marker M, annotation S1 loose, annotation S2 in G{B, S2} -> no block for M or S1; Members[G] contains S2, not M even if M is in G.

[Fact] public void Selected_wire_ends_count_as_selected()
// selectedWires {A>B}, no objects -> Selection true; blocks A, B.

[Fact] public void Selected_wire_end_inside_selected_group_maps_to_the_group()
// G{B, C} selected, selectedWires {A>B} -> blocks A, G (B is not a block of its own).

[Fact] public void One_selected_block_falls_back_to_tidy_all()
// select only A -> Selection false.

[Fact] public void Wires_inside_a_block_or_leaving_the_scope_make_no_link()
// G{B, C} with B>C -> no link; selection {A, D} with wire A>X (X unselected) -> no link for it.

[Fact] public void Only_wires_between_two_moved_blocks_are_reset()
// blocks A, G{B, C}, D; wires A>B, B>C, C>D; moved {A, G} -> reset [A>B] only (B>C inside G, C>D has an unmoved end).
```

- [ ] **Step 2: Run, and expect a compile failure.**
- [ ] **Step 3: Implement `TidyScope.cs`.**
  - Iterate in ordinal id order everywhere, so the output is deterministic.
  - Unknown ids in members, wires or the selection are ignored.
- [ ] **Step 4: Run the TidyScope tests and then the full suite. Expected: all green.**
- [ ] **Step 5: Commit** "Tidy scope in Core".

### Task 3: Tidier, undo and the Rhino check (plugin)

**Files:**
- Create: `src/StraightWire/Tidier.cs`, `tools/verify/tidy.csx`
- Modify:
  - `src/StraightWire/DocStore.cs`: split `Write` so that `WriteUnrecorded(doc, next) → SettingsUndo` (null when unchanged or not writable) does everything except push; `Write` calls it and pushes.
  - `src/StraightWire/SettingsUndo.cs`: add `internal void Revert(GH_Document doc) => Apply(doc, _before);`.
  - `src/StraightWire/DevHost.cs`: `Run("tidy")` returns `Tidier.Run(activeCanvas)`'s summary.

**Interfaces produced:**
- `internal static string Tidier.Run(GH_Canvas canvas)` returns one of:
  - `"moved <blocks> blocks (<objects> objects), reset <wires> wires"`
  - `"nothing to tidy"`
  - `"already tidy"`
  - `"busy"` (an interaction is active)
- `internal static string Tidier.Label(GH_Canvas canvas)` returns `"Tidy Selected"` or `"Tidy All"`.

**Steps:**
- [ ] **Step 1: Confirm the Grasshopper APIs** listed in Global Constraints with the scratchpad metadata inspector. Note the exact signatures in the ledger.
- [ ] **Step 2: Implement `Tidier.Run`**, all under `Log.Guard("tidy", …)`:
  1. **Bail out early:**
     - no document → return
     - `canvas.ActiveInteraction != null` or `WireEditor.For(canvas).Live/Pin` set → "busy"
  2. **Objects:** `doc.Objects`.
     - `GH_Group` becomes a `TidyGroup`. Members are collected recursively through `ObjectIDs`, nested groups included; `Selected` is `Attributes.Selected`.
     - `ComponentGuid == StraightWireMarker.Id` → Marker.
     - `IGH_ActiveObject` → Active.
     - Anything else with attributes → Annotation.
     - Bounds come from `Attributes.Bounds`.
  3. **Wires:** from `SceneReader.Connections(doc)`.
     - id = `SceneReader.WireId(s, t)`
     - source/target = `Attributes.GetTopLevel.DocObject.InstanceGuid`
     - y values = `s.Attributes.OutputGrip.Y` and `t.Attributes.InputGrip.Y`
  4. **Selection:**
     - selected objects = `doc.SelectedObjects()` ids, groups excluded (groups carry their own flag)
     - selected wires = `WireEditor.For(canvas).Selected`
  5. **Plan:** `TidyScope.Build`.
     - No links → print "Straight Wire: nothing to tidy." and return.
     - Otherwise `Tidy.Layout(plan.Blocks, plan.Links, DocStore.Get(doc).Effective.Spacing)`.
     - Empty result → print "Straight Wire: already tidy." and return.
  6. **What moves:** each moved block's delta = new top-left − `Bounds` top-left.
     - The objects to move are `plan.Members` of moved blocks, each found with `doc.FindObject(guid, true)`.
     - Save every original `Attributes.Pivot`.
  7. **Edits to clear:** `TidyScope.WiresToReset(...)`, filtered to wires that have a lane or points in `Effective`.
     - If any: `undoSettings = DocStore.WriteUnrecorded(doc, Effective.WithoutEdits(ids))`, which writes before moving (spec 4.7).
  8. **Undo record:** `doc.UndoUtil.CreatePivotEvent("Tidy", movedObjects)`, created before moving.
  9. **Move:** `Pivot += delta`, `ExpireLayout()`, `PerformLayout()`.
     - **On an exception:** put the saved pivots back, call `undoSettings?.Revert(doc)`, and rethrow (Log.Guard reports it).
  10. **Record:** `record.AddAction(undoSettings)` when it's not null. Then `doc.UndoServer.PushUndoRecord(record)`, `doc.Modified()`, `CanvasHook.ResetFailures()`, `CanvasHook.RefreshAll()`.
  11. **Return** the summary. Command line messages go through `Log.Info`, or `RhinoApp.WriteLine` if Log has no plain-info path; check `Log.cs`.
- [ ] **Step 3: `Tidier.Label`** builds the same plan as Run, without moving anything, and returns its label.
- [ ] **Step 4: Build with 0 errors, then reload the dev build in Rhino** (`tools/dev-reload.csx` through the MCP).
- [ ] **Step 5: Write and run `tools/verify/tidy.csx`.** It follows `edit-widget.csx`'s pattern: open `tests/fixtures/twisted-tower.gh` into a fresh document on the canvas, with settings at 45° and spacing 10.
  - **Setup:**
    - Put two linked components into a new `GH_Group`.
    - Give their connecting wire hand-drawn points (`Commands.SetPoints`).
    - Give one wire between two loose components a lane (`Commands.SetLane`).
  - **Tidy All**, via `DevHost.Run("tidy")`. Report:
    - (a) pairwise overlaps between blocks that moved; expected 0
    - (b) links whose source block's right edge is beyond the target block's left edge; expected 0
    - (c) blocks whose first input wire (spec 4.4, step 5) is straight to within 0.5 units, counted against non-source blocks
    - (d) the group's wire still has points, shifted by the group's delta (after a paint, so settling has run)
    - (e) the lane entry is gone
    - (f) `doc.UndoServer.FirstUndoName == "Tidy"`
    - (g) the two grouped components kept their offset from each other
    - render to `artifacts/verify/tidy.png`
  - **Undo:** every object's pivot equals the value before the tidy, and the settings JSON equals the value before. Redo gives the tidied pivots again.
  - **Already tidy:** a second tidy returns "already tidy" and adds no undo record.
  - **Busy:** `DevHost.Run("drag:on")`, then tidy → "busy", and no pivot changed. Then `drag:off`.
  - **Tidy Selected:** select three linked components in an untidied copy; only those three pivots change.
- [ ] **Step 6: Commit** "Tidier: tidy with one undo step". Include the verify script and the PNG only if artifacts are tracked; they are not, so skip the PNG.

### Task 4: Triggers (plugin)

**Files:**
- Modify:
  - `src/StraightWire/UI/StyleMenu.cs`: a separator and a "Tidy All" item right after "Reset Selected Wires". `DropDownOpening` sets its text to `Tidier.Label(Instances.ActiveCanvas)` and enables it when a document is open.
  - `src/StraightWire/Commands.cs`: `public static void Tidy()` runs `Tidier.Run(Instances.ActiveCanvas)` when a canvas exists.
  - `src/StraightWire/CanvasHook.cs`: in `OnKeyDown`, `Keys.T` with Alt calls `Commands.Tidy()`, under the same modifier rules as Alt+1/2/3.
  - `src/StraightWire/UI/StyleWidget.cs`:
    - a fourth button, "Tidy", 8 px after the style buttons (`TidyRect`), drawn in the off style
    - `HitButton` reports it as index 3, and a click calls `Commands.Tidy()`
    - the scope label moves to the right of the Tidy button
- Rhino check `tools/verify/tidy-triggers.csx`:
  - `Tidier.Label` is "Tidy All" with nothing selected and "Tidy Selected" with two linked components selected.
  - A widget mouse-down at the Tidy button's centre tidies the canvas: the undo name is "Tidy".
  - Invoking the canvas `OnKeyDown` with `Keys.T | Keys.Alt` (reflection, as in `edit-widget.csx`) tidies again after an Undo.
  - Render the widget corner to `artifacts/verify/tidy-widget.png`.

- [ ] Steps: implement → build (0 errors) → dev-reload → run the check → commit "Tidy menu item, Alt+T and widget button".

### Task 5: Docs, package, install, hands-on

- [ ] **README.md:** replace "Tidying the canvas comes next." with one paragraph on Tidy All / Tidy Selected, groups as blocks, and one undo step.
- [ ] **INSTALL.txt, USING IT:**
  - `Straight Wire menu > Tidy All / Tidy Selected`
  - `Alt+T` (Option on a Mac)
  - the Tidy button
  - a line saying groups move as one block
- [ ] **Version 0.3.0** in `StraightWire.csproj` and `package.sh`. Run `./package.sh`: 170 + new tests pass, and both packages are written. Install the Release `.gha` into the Libraries folder, replacing 0.2.0.
- [ ] **Commit** "Document tidy and bump to 0.3.0".
- [ ] **The user restarts Rhino and tries Tidy by hand.** Offer the final whole-branch review (phase 3).
