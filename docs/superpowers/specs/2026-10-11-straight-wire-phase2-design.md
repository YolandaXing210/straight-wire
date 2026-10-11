# Straight Wire Phase 2: Editing, Design

- Date: 2026-10-11
- Status: approved in conversation (approach A and the interaction section); the remaining sections were written straight into this document at the user's request
- Builds on: `docs/superpowers/specs/2026-10-10-straight-wire-phase1-design.md` and the phase 1 code on branch `phase1-rendering`
- Reference: the editing interactions in `docs/reference/straight-wire-prototype.html` (`hitWire`, `hitHandle`, `segAction`, `ensureManual`, `snapPoint`, `movePoint`, `moveSeg`, the `dblclick` and `keydown` handlers)

## 1. Goal

Let people shape individual wires by hand inside Grasshopper, the way the prototype does:
- select a wire
- slide its lane
- drag, add and remove bend points
- reset it to automatic

Edits are saved in the .gh file and can be undone. Routing also stops chasing a component while it is dragged: wires re-route once, when it is dropped.

Success criteria:
- A routed wire can be hovered, selected and edited with the mouse, without disturbing Grasshopper's own selection, dragging, wiring or relay behaviour anywhere else.
- Every finished edit is one step in Grasshopper's Undo/Redo, and survives save and reopen.
- Dragging a component costs no routing at all. One full route runs on drop.

## 2. Decisions made with the user

| Question | Decision |
|---|---|
| Wires attached to a component while it is dragged | Drawn as Grasshopper's own curves until the drop; every other wire keeps its last route |
| Hand-edited wire whose components move | If both ends moved by the same amount in one step, its points move with them. If only one end moved, the points stay put and the end segment stretches |
| Delete with wires selected | Resets them to automatic; never disconnects |
| How editing hooks into Grasshopper | A canvas widget (approach A). Grasshopper offers mouse presses and double-clicks to widgets before components and before its relay insertion. Nothing is patched |
| Interaction details | As in section 4, approved |

## 3. Scope

In this phase:
- Drop-only routing for component drags.
- Hover, select (click and Shift-click), clear (click elsewhere or Esc).
- Lane slide on automatic wires; point drag with snapping; segment slide on hand-drawn wires.
- Double-click to add or remove a point; Delete or Backspace to reset.
- The style menu, buttons and shortcuts act on selected wires as well as wires touching selected components. The menu gains "Reset Selected Wires".
- Persistence of lane positions and points, with anchors (section 6).
- Undo for edits and for style and spacing changes.

Not in this phase (phase 3): tidying all or selected components, and port magnets.

## 4. Interaction

- **Hover:**
  - Within 6 screen pixels of a routed wire, and not over a component, the wire highlights.
  - The cursor shows what a drag does: a sideways arrow on an automatic wire's middle run, a move cursor on a hand-drawn segment or on a handle.
- **Selection:**
  - Clicking a wire selects only it and clears Grasshopper's component selection. Shift-click toggles a wire in the selection.
  - Clicking empty canvas or a component, or pressing Esc, clears the wire selection; Grasshopper then behaves as usual.
  - Selected wires are drawn in Grasshopper's selection colour, with square handles on every bend point.
- **Drags** (they start after the mouse moves 3 screen pixels; a shorter press is a click):
  - **Middle run of an automatic lane wire:** the lane follows the mouse sideways, live. Other lanes make room as they would for any lane. On release the wire keeps that lane position and stays automatic.
  - **A handle:**
    - It moves that point, and the wire becomes hand-drawn: its current bends become its points.
    - Snapping: within 8 screen pixels of a 0°, 45° or 90° line through either neighbour, the point lands on that line, or on the crossing of one line from each neighbour. Otherwise it lands on a 10-unit grid.
    - Option (Alt on Windows) disables snapping.
  - **A segment of a hand-drawn wire:** slides perpendicular to itself and its neighbours stretch. Movement snaps to the 10-unit grid for horizontal and vertical segments, unless Option is held.
  - **The stub out of a port, a straight wire or a backwards wire:** selects only.
- **Double-click:**
  - On a wire: adds a point at the nearest place on that segment, and the wire becomes hand-drawn.
  - On a handle: removes that point. Removing the last point returns the wire to automatic.

  Double-clicks on our wires do not insert a relay; on curve-style wires the native relay insert still works.
- **Keys:**
  - Delete or Backspace with wires selected resets them to automatic.
  - Esc clears the wire selection.
- **Not editable:** curve-style wires and hidden-display wires. They are not hit-tested at all.
- **Live routing:** only while a lane is dragged. Point and segment drags redraw just the wire being edited, from its live points, and route once on release.

## 5. Routing changes (Core)

A `SceneWire` gains three optional inputs:
- `LaneFraction` (double, 0–1): where the lane sits within the wire's free range. The preferred position becomes `lo + fraction·span`, and in push-and-shove the lane counts as heavy: inverse mass 0.15 instead of 1, as in the prototype.
- `PinX` (double): set only for the lane being dragged.
  - Its preferred position is `PinX`.
  - The free stretch nearest `PinX` is chosen, however far it is.
  - It has inverse mass 0, so others move around it and it never moves.
  - In left/right ordering against a pinned lane, the other lane goes on whichever side of the pin it was on before, as in the prototype.
  - In 45° style a pinned wire only tries its own position (offset 0).
- `Points` (list of points): a hand-drawn wire. Its kind is `Manual`. Its path is start, the points, end (deduplicated), whatever the style.
  - It takes no lane and is not chamfered.
  - It still counts as an obstacle for other wires' 45° decisions, and takes part in junction dots.

The scene fingerprint includes all three.

`RouteResult` gains `LaneFractions`: for every lane wire, `(lane − lo) / span`. The fraction is 0.5 when the span is under 0.5. This is what a finished lane drag stores.

New pure helpers:
- **`Frozen.Keep(cached, scene)`:**
  - Returns the cached result minus every path whose first or last point no longer matches its wire's current start or end (the wire is attached to something moving).
  - It also drops the junction dots of those wires' groups.
- **Editing geometry (`WireEdit`):**
  - hit-testing a wire (nearest segment within a tolerance) and its handles
  - turning a displayed path into points
  - moving a point with snapping
  - sliding a segment
  - inserting and removing a point

## 6. Data and persistence

The file settings JSON stays version 1; nothing has been published yet, so it is extended in place. Two optional objects are added:

```json
{
  "v": 1, "mode": "diag", "spacing": 10,
  "wires":  { "<id>": "ortho" },
  "lanes":  { "<id>": 0.42 },
  "points": { "<id>": { "at": [sx, sy, tx, ty], "p": [x1, y1, x2, y2] } }
}
```

- **Fields:**
  - `lanes` values are clamped to 0–1.
  - `points.p` is the flat list of interior points. An entry with an odd count or no points is ignored.
  - `points.at` holds the source and target components' top-left corners when the points were last settled.
  - A wire with points ignores its lane entry.
- **Pruning:** `Pruned` drops `lanes` and `points` of wires that no longer exist, as it already does for style overrides.
- **Settling** (pure function in Core; the plugin runs it at paint time whenever no component drag is in progress):
  - For each wire with points, compare its anchors with where its components are now.
  - Both moved by the same amount (within 0.01) and by more than 0.01: shift the points by that amount and store the new anchors.
  - Otherwise, if either moved: keep the points and store the new anchors.
  - The result is written to the file silently: no Modified call (the move already marked the file modified) and no undo step. Undoing a move then settles the points straight back.
- **Edits:** every edit writes the new settings through the same path as style changes:
  - prune
  - write the JSON
  - ensure the marker
  - mark the document modified
  - push an undo step

## 7. Undo

- **The undo action:** `SettingsUndo`, a `GH_UndoAction`, holds the settings JSON from before and after the change (null meaning "no settings").
  - Undo writes the before JSON back (or deletes the key when it was null), drops the document's cached state so it is read again, and refreshes the canvas.
  - Redo does the same with after.
  - It does not expire the solution.
- **What gets an undo step:** every settings change made by the user is one undo step, named after the action:
  - styles, spacing and Follow Global
  - Save to This File
  - lane, point and segment drags
  - adding and removing points
  - reset
- **What doesn't:** settling and the first marker insertion are not undo steps. The marker stays after an undo; it is inert.

## 8. Drawing

- **Selected wires:** drawn with Grasshopper's pen as if both ends were selected (solid selection colour).
- **Hovered wire** (when not selected): drawn again on top in the selection colour at about half opacity, 2 units wider than its pen.
- **Handles:** squares on every interior point of each selected wire's displayed path: 7 screen pixels, white fill, outlined in the selection colour.
- **Wire being dragged:**
  - Point or segment drag: the wire is drawn from its live points.
  - Lane drag: the live route.
- **Component drag:** wires that `Frozen.Keep` removed are drawn as Grasshopper curves, as for curve-style wires.

## 9. Plugin structure

| Unit | Responsibility |
|---|---|
| `WireEditor` | Per-canvas editing state: selected wire ids, hovered wire id, the active drag (kind, wire, live points or pin x), and the last route result used for hit-testing |
| `EditWidget` | `GH_Widget` that does the mouse work through `Contains`, `RespondToMouseDown/Move/Up/DoubleClick`, and sets the cursor. It claims a press only on a wire or handle, never over a component |
| `SettingsUndo` | The undo action |
| `DocStore` | Gains `Reload(doc)`, and `Write(doc, next, undoName)`, which pushes the undo step |
| `SceneReader` | Passes lanes, settled points and the editor's live state (pin x, live points) into the scene |
| `CanvasHook` | Detects component drags (`ActiveInteraction is GH_DragInteraction`) and uses `Frozen.Keep`; settles anchors; keeps the editor's last route result current; handles Delete, Backspace and Esc |
| `WireRenderer` | Draws selection, hover, handles and live paths |
| `Commands` | Scope = selected wires plus wires touching selected components; adds `ResetSelected` |
| `StyleMenu` | Adds "Reset Selected Wires" |

The editing widget can be hidden from Grasshopper's widget menu like any other widget; hiding it turns editing off. Its visibility is stored in the personal settings (`EditingEnabled`, default on).

## 10. Errors and safety

- Everything in the widget and the key handler runs under `Log.Guard`. A failure reports once, ends the drag, and leaves Grasshopper's own handling of that press untouched.
- A drag that ends any other way (the canvas loses the mouse, the document changes) is dropped without writing anything.
- Hit-testing ignores anything under a component: a wire passing under a component belongs to the component as far as the mouse is concerned.

## 11. Testing

- **Core unit tests (written first):**
  - lane fraction and pin behaviour in routing
  - manual wires: path, no lane, obstacle, junctions
  - fingerprint
  - `LaneFractions`
  - `Frozen.Keep`
  - settling
  - JSON lanes/points round trip, clamping, ignoring bad entries, pruning
  - every editing geometry helper
  - hit-testing
- **Rhino checks through the MCP:**
  - drive the widget with constructed `GH_CanvasMouseEvent`s: hover, click-select, lane drag, handle drag, segment drag, double-click add and remove, Delete
  - Undo and Redo
  - save and reopen
  - a component drag frozen and then routed on drop
- **Finally the user's hands-on try.**

## 12. Known limits

- Copy and paste does not carry wire edits; pasted wires are automatic.
- Hand-drawn wires do not avoid components that later move onto them.
