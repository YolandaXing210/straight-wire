# Straight Wire Phase 2 (Editing) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans (the user chose inline execution). Steps use checkbox (`- [ ]`) syntax.

**Goal:** In Grasshopper, wires can be hovered, selected, lane-slid and bent by hand. Edits persist and undo, and routing waits for component drops.

**Architecture:**
- **Core (pure, unit-tested):**
  - edit inputs on `SceneWire`: lane fraction, pin, points
  - a `Manual` wire kind
  - `RouteResult.LaneFractions`
  - `Frozen.Keep`
  - `Anchoring.Settle`
  - `FileSettings` lanes and points with JSON
  - `WireEdit` geometry and hit-testing
- **Plugin:**
  - `WireEditor` state and an `EditWidget` that claims mouse presses on wires through Grasshopper's widget dispatch
  - `SettingsUndo` for Grasshopper's undo
  - drop-only routing in `CanvasHook`
  - drawing for selection, hover, handles and live paths

**Spec:** `docs/superpowers/specs/2026-10-11-straight-wire-phase2-design.md`. Phase 1's spec and plan still hold for everything not changed here.

## Global Constraints

Everything in phase 1's Global Constraints still applies: versions, paths, GUIDs, commit trailer, MCP verification, no `yak push`. In addition:
- **Settings format:** the settings JSON stays `"v":1`. `lanes` and `points` are written only when non-empty, so phase 1's exact-format test still holds.
- **Editing constants:**
  - hover and hit tolerance: 6 screen px
  - handle hit: 7 screen px
  - drag threshold: 3 screen px
  - snap tolerance: 8 screen px
  - grid: 10 canvas units
  - pinned inverse mass: 0
  - lane-fraction inverse mass: 0.15
  - anchor tolerance: 0.01
- **Grasshopper APIs verified this session:**
  - `GH_UndoAction` (abstract `Internal_Undo/Internal_Redo(GH_Document)`, virtual `ExpiresSolution`)
  - `GH_UndoServer.PushUndoRecord(string, GH_UndoAction)`
  - `GH_Document.Undo()/Redo()/DeselectAll()`
  - `GH_ObjectResponse {Ignore, Capture, Release, Handled}`
  - `GH_CanvasMouseEvent(Point, PointF, MouseButtons, int clicks, int delta)`
  - `GH_Widget.RespondToMouseDoubleClick`
  - `GH_Canvas.ActiveWidget`
  - `GH_DragInteraction`
- **Widget dispatch order (read from IL):**
  - Mouse down: interaction → active widget → active object → *inactive widgets* (`RespondToMouseDown` on every visible widget, no `Contains` check) → inactive object → default.
  - Double-click: the same order, ending with relay insertion.
  - Mouse move over inactive widgets: `Contains` first, then `RespondToMouseMove`.

## Review Focus

1. **Presses near but not on a wire, and on wires under components:** Grasshopper must behave exactly as before. That covers box selection, dragging components, wiring from ports and relay double-clicks on curve wires. Tested in Task 6's widget check (press on empty canvas and on a component returns Ignore).
2. **Undo after an edit that also added the marker, and Undo of the very first settings write:** the file must go back to no settings, without errors. Tested in Task 5's undo check.
3. **A hand-drawn wire whose components are moved one at a time, then together, then undone:** the points follow only the together-move. Tested in Task 2 (`Settle` sequence) and in Task 5's Rhino check.
4. **A component dragged when nothing was routed before** (fresh file): it must route once and not crash. Tested in Task 5's frozen-drag check.
5. **Deleting a wire's last point:** the wire goes back to automatic, not to a straight line. Tested in Task 4 (`RemovePoint` result) and Task 6.

---

### Task 1: Edit inputs in routing (Core)

**Files:**
- Modify: `Scene.cs`, `RouteContext.cs`, `Lanes.cs`, `Diagonals.cs`, `WirePath.cs`, `Router.cs`, `RouteResult.cs`
- Test: `tests/StraightWire.Core.Tests/RoutingEditTests.cs`

**Interfaces produced:**
- `SceneWire(…, WireStyle style, double? laneFraction = null, double? pinX = null, IReadOnlyList<Pt> points = null)`, with properties `LaneFraction`, `PinX`, `Points`.
- `WireKind.Manual`.
- `RouteResult(paths, junctions, IReadOnlyDictionary<string,double> laneFractions = null)`, with `LaneFractions` never null.

**Tests first:**
- a lane fraction of 0.25 puts the lane a quarter of the way through its free range
- pin X at 300 puts the lane at 300
- a pinned lane does not move while its neighbour is pushed away to SP
- the neighbour of a pinned lane keeps its side
- a fraction lane moves less than a free one when pushed
- `LaneFractions` reports (lane − lo)/span
- a Manual wire's path is start, points, end, in any style; it is not chamfered and not in `LaneFractions`
- a Manual wire counts as an obstacle: a 45° candidate crossing it keeps four bends where it otherwise would not
- the fingerprint changes with the fraction, the pin and the points
- the whole phase 1 suite stays green

**Implementation notes (prototype parity):**
- Preferred position: `pref = PinX ?? (LaneFraction → lo + f·span) ?? mid`.
- The `Feasible` probe uses `PinX ?? PrevLane`, and a pinned wire takes the nearest stretch at any distance.
- Inverse mass is `im = pinned ? 0 : fraction ? 0.15 : 1`:
  - same-source pairs move by `d·0.5·m/(ma+mb)`
  - gap pairs by `d·m/(ml+mr)`
  - pairs where both masses are 0 are skipped
- Pin ordering: the other lane goes right of the pin if `(PrevLane ?? P) ≥ pin.P`.
- `Diagonals`: a pinned wire tries offset 0 only.
- `WirePath`: Manual → `Dedupe([a, …points, b])`.

### Task 2: Frozen routes and anchoring (Core)

**Files:**
- Create: `Frozen.cs`, `Anchoring.cs`
- Test: `FrozenTests.cs`, `AnchoringTests.cs`

**Interfaces:**
- `Frozen.Keep(RouteResult cached, Scene scene) → RouteResult`.
- `Anchoring.Settle(FileSettings s, Func<string, (Pt Source, Pt Target)?> anchorsOf) → FileSettings`. It returns the same instance when nothing changed.

**Tests:**
- **Frozen:**
  - all endpoints unchanged → the same paths and dots
  - one wire's end moved → its path is dropped, and so are its group's dots; others are kept
  - a wire missing from the scene is dropped
  - lane fractions pass through
- **Anchoring:**
  - both ends moved +10,+5 → points shifted, anchors updated
  - only the target moved → points unchanged, anchors updated
  - the sequence (target moves, then source moves by the same amount) → points never shift
  - moved together then undone (moved back) → points back where they started
  - a wire with no current anchors (gone) → left alone

### Task 3: Lanes and points in file settings (Core)

**Files:**
- Modify: `FileSettings.cs`
- Test: `FileSettingsEditTests.cs`

**Interfaces:**
- `ManualPoints(IReadOnlyList<Pt> points, Pt sourceAt, Pt targetAt)`, with `Points`, `SourceAt`, `TargetAt`, `Shifted(dx, dy)` and `WithAnchors(s, t)`.
- `FileSettings` gains:
  - `Lanes` (`IReadOnlyDictionary<string,double>`) and `Points` (`IReadOnlyDictionary<string,ManualPoints>`)
  - `WithLane(id, f)` (clamped 0–1)
  - `WithPoints(id, ManualPoints)`, where an empty point list removes the entry
  - `WithoutEdits(ids)`
- Every existing `With…` keeps lanes and points, and `Pruned` prunes them too.
- JSON: `lanes` and `points` are written sorted, and only when non-empty.

**Tests:**
- a round trip with lanes and points
- the phase 1 exact-format string is unchanged when there are no edits
- the lane value is clamped
- an odd point count is ignored
- an empty `p` is ignored
- a non-number in `at` is ignored
- pruning drops lanes and points of dead wires
- `WithoutEdits` clears both and keeps style overrides
- `WithLane` on a wire that has points keeps the points (points win at read time)

### Task 4: Editing geometry and hit-testing (Core)

**Files:**
- Create: `WireEdit.cs`
- Test: `WireEditTests.cs`

**Interfaces** (`public static class WireEdit`):
- `WireHit? HitWire(IEnumerable<KeyValuePair<string, Pt[]>> paths, Pt p, double tol)`, where `WireHit` holds `Id`, `Segment` and `Distance`; the nearest wins.
- `int HitHandle(Pt[] path, Pt p, double tol)`: an interior point index (0-based among interior points), or -1.
- `SegmentAction ActionFor(bool laneWire, bool manual, int segment, int pathLength)`:
  - Lane for a lane wire when `1 ≤ seg ≤ len−3`
  - Segment for a manual wire when `len > 2`
  - None otherwise
- `List<Pt> PointsOf(Pt[] path)`.
- `Pt Snap(Pt raw, Pt a, Pt b, double tol, double grid, bool free)`: the prototype's `snapPoint`.
- `List<Pt> MovePoint(IReadOnlyList<Pt> pts, int i, Pt p)`.
- `List<Pt> SlideSegment(Pt[] path0, IReadOnlyList<Pt> pts0, int seg, double dx, double dy, double grid, bool free)`: the prototype's `moveSeg`.
- `List<Pt> InsertPoint(Pt[] path, int seg, Pt at)`: the projection of `at` onto that segment, inserted at interior index `seg`.
- `List<Pt> RemovePoint(IReadOnlyList<Pt> pts, int i)`.

**Tests:**
- the hit picks the nearest wire and segment, and misses beyond tol
- a handle hit finds interior points only, never endpoints
- the action table
- snap:
  - onto a horizontal line through a neighbour
  - onto the 45° crossing of lines from both neighbours
  - onto the grid when far from lines
  - unchanged when free
- slide moves the two points of the dragged segment perpendicular only, grid-rounded for axis segments
- insert puts the projection at the right index
- removing the last point gives an empty list

### Task 5: Drop-only routing, settling, undo (plugin)

**Files:**
- Modify: `SceneReader.cs` (lanes, settled points, live pin; `Anchors(doc)`), `CanvasHook.cs` (drag detection, `Frozen.Keep`, settle), `DocStore.cs` (`Reload`, `Settle`, `Write(doc, next, undoName)`), `Commands.cs` (undo names)
- Create: `SettingsUndo.cs`, `WireEditor.cs` (state only: `For(canvas)`, `Selected`, `Hover`, `Pin`, `LivePath`, `LastRead`, `LastRoutes`)
- Rhino checks: `tools/verify/drop-and-undo.csx`:
  1. fixture at 45°: route
  2. pretend a drag (DevHost `drag:on`), move one component, render → its wires are curves and every other path is unchanged
  3. `drag:off`, render → routed
  4. style change → Undo → no settings key → Redo → back
  5. points on a wire: move both ends together → points follow; Undo the move → they come back

### Task 6: Editing widget, drawing, keys, menu (plugin)

**Files:**
- Create: `UI/EditWidget.cs`
- Modify: `WireRenderer.cs` (selection, hover, handles, live path), `CanvasHook.cs` (register the widget; Delete, Backspace and Esc), `Commands.cs` (scope includes selected wires; `SetLane`, `SetPoints`, `ResetSelected`), `UI/StyleMenu.cs` ("Reset Selected Wires"), `UserSettings.cs` (`EditingEnabled`), `DevHost.cs` (`Describe` adds selected and hover)
- Rhino check: `tools/verify/edit-widget.csx` drives the widget with constructed mouse events:
  - press on empty canvas → Ignore, selection cleared
  - press on a component → Ignore
  - click a wire → selected
  - drag its middle run 40 px → lane fraction stored, one undo step
  - drag a handle → points stored
  - double-click a segment → point added
  - double-click a handle → removed
  - Delete → reset
  - Undo restores each step
  - render the selection and handles to a PNG

### Task 7: Docs, package, install, hands-on

- Update `README.md` and `INSTALL.txt` (USING IT: editing gestures).
- Run `./package.sh` and install the Release build.
- The user restarts Rhino and tries editing by hand.
- Final whole-branch review (phase 1 and 2) if the user wants it.
