# Dragging Backwards Wires Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A backwards wire's out run, crossing and in run can each be dragged, live, and the wire stays automatic with the adjustment kept.

**Architecture:** Core gains `BackRuns` (three optional numbers) on `SceneWire`, honoured by `BackWires`, reported back as `RouteResult.Backs`, and stored as `"backs"` in the file settings. The editing widget maps a hit segment to a run, hands live runs to the scene through `WireEditor.BackPin` while dragging (so the canvas routes live, as for a lane), and stores the dragged run's routed value on release.

**Tech Stack:** C# (netstandard Core, net48/net7 Grasshopper plugin), xUnit, Rhino MCP for live checks.

**Spec:** `docs/superpowers/specs/2026-10-23-back-wire-runs-design.md`

## Global Constraints

- Out and in runs are at least `Routing.Stub` (18) from their grips.
- Crossing: inside the chosen free stretch of the gap, or at least `Routing.Clearance` (8) clear of both nodes when going around.
- Settings JSON stays version 1; new key `"backs"`, parts `"out"`, `"in"`, `"cross"`, omitted when automatic.
- Undo name for every run drag: `"Move Wire Lane"`.
- A hand-drawn wire (points) ignores its back runs.

## Review Focus

- A stored back wire whose nodes move so it runs forwards again (dx ≥ 8): it routes as a lane, its stored runs are ignored, no crash. Test in Task 1.
- A crossing stored in a gap when the nodes later overlap vertically (no gap): it goes around, clamped clear of the nodes. Test in Task 1.
- An out run dragged left past its own port (negative distance): it stops at `Stub`. Test in Task 1.
- A `"backs"` entry with nothing readable in it: dropped, the rest of the file still loads. Test in Task 2.
- A hand-drawn or curve-style wire: no entry in `RouteResult.Backs`, so the widget never offers a run drag on it. Test in Task 1.

---

### Task 1: Routing honours back runs

**Files:**
- Create: `src/StraightWire.Core/BackRuns.cs`
- Modify: `src/StraightWire.Core/Scene.cs` (SceneWire, Fingerprint), `src/StraightWire.Core/RouteContext.cs` (WireJob fields), `src/StraightWire.Core/BackWires.cs`, `src/StraightWire.Core/Paths.cs` (`Back`), `src/StraightWire.Core/RouteResult.cs`, `src/StraightWire.Core/Router.cs`, `src/StraightWire.Core/Frozen.cs`
- Test: `tests/StraightWire.Core.Tests/BackWiresTests.cs`, `tests/StraightWire.Core.Tests/SceneTests.cs`, `tests/StraightWire.Core.Tests/PathsTests.cs`, `tests/StraightWire.Core.Tests/FrozenTests.cs`, `tests/StraightWire.Core.Tests/TestScenes.cs` (`SceneBuilder.Wire` gains `BackRuns back = null`, passed to `SceneWire`)

**Interfaces:**
- Produces:
  - `public enum BackRun { None, Out, In, Cross }`
  - `public sealed class BackRuns { BackRuns(double? @out = null, double? @in = null, double? cross = null); double? Out, In, Cross; bool IsEmpty; double? this[BackRun run]; BackRuns With(BackRun run, double? value); }`
  - `SceneWire(..., IReadOnlyList<Pt> points = null, BackRuns back = null)`, property `BackRuns Back`
  - `RouteResult(paths, junctions, laneFractions = null, backs = null)`, property `IReadOnlyDictionary<string, BackRuns> Backs` (every part filled)
  - `Paths.Back(Pt a, Pt b, double outX, double inX, double y)`
  - `WireJob.OutX, InX, AutoYb` (doubles)

Scenario A in `BackWiresTests` (S at 300,200 80×60; T at 100,0 80×60; wire (380,230)→(100,30)) routes today as `(380,230) (398,230) (398,130) (82,130) (82,30) (100,30)`, band 68..192, auto y 130.

- [ ] **Step 1: Write the failing tests** in `BackWiresTests` (helper `Routed(SceneBuilder b) => Router.Route(b.Build(), null)`; scenario builders take an optional `BackRuns`):
  - `Out_and_in_runs_move_the_verticals`: `back: new BackRuns(@out: 40, @in: 30)` → path `(380,230) (420,230) (420,130) (70,130) (70,30) (100,30)`.
  - `Cross_moves_the_crossing_within_the_gap`: `cross: -40` → crossing at y 90.
  - `Runs_stop_at_their_limits`: `@out: -50, @in: 0, cross: 500` → out x 398, in x 82, crossing y 192.
  - `The_gap_search_spans_the_moved_verticals`: Scenario A plus node `M` at (400,100) 40×40 (right of the stub, inside a moved out run) and `@out: 100` → band 148..192 (M with clearance covers 92..148), crossing 170.
  - `Cross_can_be_dragged_into_another_free_stretch`: Scenario A plus node `M` at (200,100) 60×40 (free stretches 68..92 and 148..192), `cross: -55` (wants y 115 from auto 170, nearer 68..92) → crossing y 92.
  - `Around_cross_stays_clear_of_the_nodes`: the existing "nodes overlap" scene (auto −24) with `cross: -16` → y −40; with `cross: +30` → y −8 (top of the higher node 0, minus clearance 8).
  - `A_stored_gap_cross_goes_around_when_the_gap_closes`: overlap scene with `cross: 60` → y −8.
  - `Back_wires_from_one_output_share_a_stored_out_run`: two back wires from S, same group, to T and to a second target `U` at (100,300) 80×60 input (100,330); the first has `@out: 40`, the second none → both out verticals at x 420.
  - `The_first_stored_out_run_of_an_output_wins`: as above, second has `@out: 25` → both at 420.
  - `A_set_vertical_steps_past_a_component`: Scenario A plus node `N` at (430,200) 40×20 (with clearance x 422..478, y 192..228, inside the out vertical's span 130..230): `@out: 50` (x 430) → out x 422; `@out: 75` (x 455) → 478. Without a set run the out vertical stays at 398.
  - `A_set_in_run_steps_past_a_component`: Scenario A plus node `N` at (20,40) 40×40 (with clearance x 12..68): `@in: 50` (x 50) → in x 68 (`path[3].X`).
  - `Diagonal_uses_the_moved_verticals`: Scenario B with `@out: 30` → diagonal starts at x 250: `(220,430) (250,430) (250,y) …` with run 168 (= 250 − 82); assert `path[1] == P(250, 430)` and the diagonal's dx/dy magnitudes equal.
  - `A_forward_wire_ignores_back_runs`: a lane wire (S at −80,80 → T at 400,280 as in `RoutingEditTests.Corridor`) with `@out: 100` routes with its lane at 200 (middle of 18..382), as without.
  - `Backs_report_every_routed_back_wire_with_all_parts`: Scenario A automatic → `Backs[id]` = out 18, in 18, cross 0; with `cross: 500` → cross 62 (192 − 130); a curve-style back wire and a back wire with points have no entry.
  - `SceneTests`: `Fingerprint_changes_with_back_runs` (null vs `new BackRuns(cross: 1)` vs `new BackRuns(cross: 2)` all differ).
  - `PathsTests` line 72: call becomes `Paths.Back(P(380, 230), P(100, 30), 398, 82, 130)`, same expected path.
  - `FrozenTests`: `Keep_carries_the_back_runs` (cached `Backs` instance is passed through).

- [ ] **Step 2: Run** `dotnet test tests/StraightWire.Core.Tests --filter "FullyQualifiedName~BackWires|FullyQualifiedName~SceneTests|FullyQualifiedName~PathsTests|FullyQualifiedName~FrozenTests"` — expected: compile errors (no `BackRuns`).

- [ ] **Step 3: Implement.**
  - `BackWires.Prepare`: first pass collects, per group, the `Out` of the first routed back wire (scene order) that has one; second pass sets `OutX = Ax + max(Stub, group Out ?? Stub)`, `InX = Bx − max(Stub, In ?? Stub)`, then `Channel`, then `Clear` for each vertical whose run is set: nodes (not the wire's own) overlapping the vertical's span from its port y to `Yb`, each padded by `Clearance`, cut the allowed range (out: `[Ax + Stub, ∞)`, in: `(−∞, Bx − Stub]`, using `Intervals.Cut`); the vertical moves to the nearest point of the nearest remaining stretch.
  - `Channel`: `xl = InX − C`, `xr = OutX + C`. Gap: free stretches as today; the widest (≥ 4, as today) gives `AutoYb` = its middle. With `Cross`: wanted = `AutoYb + Cross`; band = the stretch (≥ 4) nearest wanted (distance 0 inside; first on a tie); `Yb = clamp(wanted, band)`. Around: `AutoYb` = above or below as today; with `Cross`, `Yb = min(wanted, min(a.Y0, b.Y0) − C)` above, `max(wanted, max(a.Y1, b.Y1) + C)` below.
  - `Diagonal` uses `OutX`/`InX` for `x1`/`x2`; `Orthogonal` calls `Paths.Back(a, b, w.OutX, w.InX, w.Yb)`.
  - `Router`: for each routed job with `Kind == Back`, `backs[id] = new BackRuns(OutX − Ax, Bx − InX, Yb − AutoYb)`.
  - `Frozen.Keep` passes `cached.Backs`.

- [ ] **Step 4: Run** `dotnet test tests/StraightWire.Core.Tests` — expected: all pass.

- [ ] **Step 5: Commit** "Routing honours a backwards wire's runs".

### Task 2: Back runs in the file settings

**Files:**
- Modify: `src/StraightWire.Core/FileSettings.cs`
- Test: `tests/StraightWire.Core.Tests/FileSettingsEditTests.cs`

**Interfaces:**
- Consumes: `BackRuns`, `BackRun` (Task 1).
- Produces: `FileSettings(..., IReadOnlyDictionary<string, ManualPoints> points = null, IReadOnlyDictionary<string, BackRuns> backs = null)`, `IReadOnlyDictionary<string, BackRuns> Backs`, `FileSettings WithBack(string wireId, BackRuns runs)` (null or empty removes).

- [ ] **Step 1: Write the failing tests:**
  - `Back_runs_are_written_in_the_spec_format`: `new FileSettings(WireStyle.Orthogonal, 12).WithBack("a>b", new BackRuns(@out: 40, cross: -12.5))` serializes to `{"v":1,"mode":"ortho","spacing":12,"wires":{},"backs":{"a>b":{"out":40,"cross":-12.5}}}`.
  - `Back_runs_round_trip`: out 40, in null, cross −12.5 after Serialize → Parse.
  - `Unreadable_back_entries_are_ignored` (Theory): `{"out":"x"}` and `{}` and `[1]` → `Backs` empty; `{"out":"x","in":20}` → in 20 only.
  - `WithBack_of_empty_runs_removes_the_entry`.
  - Extend `Pruning_drops_edits_of_wires_that_are_gone` and the `WithoutEdits` test to cover `Backs`; `WithMode`, `WithSpacing`, `WithOverride`, `WithLane`, `WithPoints` keep `Backs` (one test, `Other_changes_keep_back_runs`).

- [ ] **Step 2: Run** `dotnet test tests/StraightWire.Core.Tests --filter FullyQualifiedName~FileSettings` — expected: compile errors.

- [ ] **Step 3: Implement** `Backs` through every constructor call, `WithBack`, `WithoutEdits`, `Pruned`, serialize after `"points"` (only when non-empty; parts in the order out, in, cross), parse (`ReadBack(JsonElement)` returning null when no part is a number).

- [ ] **Step 4: Run** `dotnet test tests/StraightWire.Core.Tests` — expected: all pass.

- [ ] **Step 5: Commit** "Back runs are saved in the file".

### Task 3: Which run a segment is, and the drag

**Files:**
- Modify: `src/StraightWire.Core/WireEdit.cs`
- Test: `tests/StraightWire.Core.Tests/WireEditTests.cs`

**Interfaces:**
- Produces:
  - `WireEdit.BackRunFor(Pt[] path, BackRuns routed, int segment) -> BackRun`: the first and last segments → None; a vertical segment (|dx| < 0.01) at the out run's x (`path[0].X + routed.Out`) → Out, at the in run's x (`path[^1].X − routed.In`) → In; a non-vertical segment strictly between the out vertical and the in vertical → Cross; anything else → None.
  - `WireEdit.DragBack(BackRuns stored, BackRuns routed, BackRun run, double dx, double dy) -> BackRuns`: `stored.With(run, routed[run] + (Out: dx, In: −dx, Cross: dy))`.

- [ ] **Step 1: Write the failing tests:**
  - `BackRunFor_maps_an_orthogonal_back_wire`: path `(380,230) (398,230) (398,130) (82,130) (82,30) (100,30)`, routed out 18 / in 18 / cross 0 → segments 0..4 give None, Out, Cross, In, None.
  - `BackRunFor_maps_a_chamfered_back_wire`: the same path through `Paths.Chamfer(…, 14)` (internal; test via `InternalsVisibleTo` as other tests do) → the two verticals give Out and In, the crossing and its two corner chamfers give Cross, the stubs and their chamfers give None.
  - `DragBack_moves_only_the_dragged_part`: stored `(null, 25, null)`, routed `(18, 25, 0)`: Out by dx 10 → `(28, 25, null)`; In by dx 10 → `(null, 15, null)`; Cross by dy −7 → `(null, 25, −7)`.

- [ ] **Step 2: Run** `dotnet test tests/StraightWire.Core.Tests --filter FullyQualifiedName~WireEditTests` — expected: compile errors.

- [ ] **Step 3: Implement** both in `WireEdit`.

- [ ] **Step 4: Run** `dotnet test tests/StraightWire.Core.Tests` — expected: all pass.

- [ ] **Step 5: Commit** "Back wire runs can be hit and dragged (geometry)".

### Task 4: The widget drags back runs

**Files:**
- Modify: `src/StraightWire/WireEditor.cs`, `src/StraightWire/SceneReader.cs`, `src/StraightWire/UI/EditWidget.cs`, `src/StraightWire/Commands.cs`, `src/StraightWire/Tidier.cs`, `src/StraightWire/DevHost.cs`, `README.md`
- Create: `tools/verify/back-drag.csx`

**Interfaces:**
- Consumes: `RouteResult.Backs`, `FileSettings.Backs`/`WithBack`, `WireEdit.BackRunFor`/`DragBack`, `SceneWire(…, back)`.
- Produces: `WireEditor.BackPins : Dictionary<string, BackRuns>` (null when no run is dragged); `Commands.SetBack(GH_Document doc, IReadOnlyCollection<string> wireIds, BackRun run, double value)` (one undo step "Move Wire Lane", each wire's stored runs `With(run, value)`); `DevHost.Paths` reports kind `back` for wires in `Backs`; `DevHost.Describe` ends with ` backs=<count of stored entries>`.

- [ ] **Step 1: Write the Rhino check** `tools/verify/back-drag.csx` (helpers as in `edit-widget.csx`). On the fixture in ortho, the one back wire (kind `back`): for each of out (press the middle of the out vertical, drag +30 x), in (in vertical, −30 x) and cross (crossing, +20 y): press, move 1 then the full distance, release; report the moved coordinate before → after, the stored `backs` count, and the undo name. Then Delete with that wire selected → path back to the original, `backs=0`; Undo → the edited path again. Print PASS when out x +30, in x −30, crossing y +20 (or clamped, reported), each undo is "Move Wire Lane", and Delete/Undo behave.

- [ ] **Step 2: Build and run it against the current build** (`dotnet build src/StraightWire`, send `tools/dev-reload.csx`, then the check) — expected: FAIL (presses on the back wire only select; kind is `other`).

- [ ] **Step 3: Implement.**
  - `SceneReader`: a wire without points gets its `editor.BackPins` entry when it has one, else its stored `Backs` entry.
  - `EditWidget`: `Kind.Run`; `Target` carries the `BackRun`. In `Find`, a non-manual hit with `ActionFor == None` and an entry in `routes.Backs` uses `BackRunFor`; Run targets get `Cursors.SizeNS` for Cross, `Cursors.SizeWE` otherwise. Press notes the moved wires (for Out: every id in `routes.Backs` whose `WireDraw.Source` in `LastRead.Wires` is the hit wire's source; otherwise the hit wire), each one's stored runs (`DocStore.Get(doc).Effective.Backs`, or empty), and the hit wire's routed runs; Drag sets `editor.BackPins[id] = DragBack(stored[id], routedHit, run, dx, dy)` for each, with dx, dy the canvas movement since the press; MouseUp clears `BackPins`; Commit calls `Commands.SetBack(doc, moved, run, LastRoutes.Backs[hitId][run])`.
  - `Tidier` resets wires with a `Backs` entry too. `DevHost` as in Interfaces.
  - `README.md`, the editing sentence: lanes can be dragged sideways, and on a wire that runs backwards both verticals sideways and the crossing up and down.

- [ ] **Step 4: Rebuild, reload, run** `back-drag.csx`, `edit-widget.csx`, `grip-press.csx` — expected: all PASS / unchanged; `dotnet test tests/StraightWire.Core.Tests` all pass.

- [ ] **Step 5: Commit** "Drag a backwards wire's verticals and crossing".
