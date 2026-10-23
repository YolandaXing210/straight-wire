# Straight Wire: Dragging Backwards Wires, Design

- Date: 2026-10-23
- Status: requested by the user in conversation; every decision below not quoted from them is Claude's call (the user works without approval stops). A reviewer's three questions (shared trunk, verticals through components, the crossing changing side) are settled in section 2
- Builds on: phase 2 editing (`docs/superpowers/specs/2026-10-11-straight-wire-phase2-design.md`), which made backwards wires "select only"

## 1. Goal

The user, looking at a backwards wire (a slider's output feeding an input to its upper left: out, up, back left, up, in):

> 当线条变成这样的时候竖线和横线都应该可以调整宽距
> (When a wire takes this shape, both the vertical and the horizontal lines should be adjustable.)

A backwards wire has three runs between its two stubs:
- the **out run**: the vertical next to the source, `Stub` (18) right of the output grip
- the **crossing**: the run back to the left, through the gap between the two nodes or around them
- the **in run**: the vertical next to the target, `Stub` left of the input grip

Each of them can be dragged: the two verticals sideways, the crossing up and down.

Success:
- Dragging any of the three moves it live, the way a lane drag does, and the wire keeps that shape after release.
- The wire stays automatic (Claude's call, see section 2): moving either component re-routes it with the adjustment kept, and it never turns into a hand-drawn wire with slanted ends.
- One undo step per drag; saved in the file; Delete resets it.

## 2. Decisions

| Question | Decision |
|---|---|
| Does a dragged backwards wire stay automatic or become hand-drawn? | Automatic, like a lane drag. Hand-drawn wires keep their points when one component moves, which leaves a slanted end segment; a lane-style setting keeps the wire square however the nodes move (Claude's call) |
| What is stored for each run | Out run: its distance from the output grip. In run: its distance from the input grip. Crossing: its offset from where routing would put it. All relative, so they travel with the nodes (Claude's call) |
| Limits | The out and in runs stay at least `Stub` from their grips, so a vertical never folds back over its own node. A set out or in run takes the free stretch nearest where it was put, beside any component its vertical would cross (with `Clearance`), as a dragged lane does; an automatic vertical is placed as today. The crossing stays inside the free part of the gap (any free stretch of it, see section 3), or, going around, at least `Clearance` clear of both nodes on the side routing chose (Claude's call) |
| Other back wires from the same output | The out run belongs to the output: all back wires from one output share it, so the trunk out of the port stays one line, as it does for lanes. Dragging it moves it for all of them and stores the value on each. Routing takes it from the first of them (scene order) that has one stored (Claude's call) |
| The crossing after the routing changes side | The stored offset is applied as it is, and clamped. If a component move flips the crossing from above to below (or from a gap to around), an offset set on one side can leave the crossing hugging the nodes on the other; dragging it again or Delete fixes it. A known limit, chosen over storing the side in the file (Claude's call) |
| The two stubs | Still select only |
| 45° style | Same three runs. When the crossing is one diagonal, dragging the diagonal moves the crossing |
| Snapping | None, as for lane drags |
| Undo name | "Move Wire Lane", the same as a lane drag: to the user they are the same kind of edit |

## 3. Routing (Core)

`BackRuns`: three optional numbers, `Out`, `In`, `Cross`. A part that is null is automatic.

`SceneWire` gains `Back` (`BackRuns`, may be null). A hand-drawn wire ignores it. The scene fingerprint includes it.

`BackWires`:
- Out run: the `Out` of the first back wire (in scene order) from the same group that has one, else `Stub`. At least `Stub`. Its x is `Ax + out`.
- In run: `In`, else `Stub`. At least `Stub`. Its x is `Bx − in`.
- The gap search uses those two x values instead of the stub ends, so a node parked between the moved verticals is avoided.
- Then, for a vertical whose run is set (not automatic): every node other than the wire's own two that overlaps the vertical's span (port y to crossing y) with `Clearance` forbids its x range with `Clearance`. The vertical takes the free stretch nearest its x (out: right of `Ax + Stub`; in: left of `Bx − Stub`) and is clamped into it. The gap is not searched again.
- The automatic crossing y is what routing picks today (the middle of the widest free stretch, or the cheaper of above and below). With `Cross` set:
  - In a gap: the wanted y is `auto + Cross`. The free stretch nearest it is used (so the crossing can be dragged past a node parked in the gap), and the y is clamped into that stretch.
  - Around: the wanted y is clamped to stay at least `Clearance` above the higher node top (going above) or below the lower node bottom (going below).
- The 45° single diagonal uses the moved verticals and the crossing y, as it uses the stub ends and the automatic y today.

`RouteResult` gains `Backs`: for every routed automatic back wire, its runs as routed, with all three parts filled: out and in distances after the limits, and `Cross = y − auto`. This is what a finished drag stores, as `LaneFractions` is for lanes. `Frozen.Keep` carries it along.

## 4. Editing

- Hit-testing a back wire's segment (Core, `WireEdit`): the first and last segments are the stubs (select only). A level segment, or one running from the out run's x to the in run's x (the single 45° crossing), is the crossing. Any other segment touching the out run's x is the out run, touching the in run's x the in run: in 45° style that takes in the corners, which are all that is left of a vertical shorter than two chamfers.
- Cursor: sideways arrows over the verticals, up-down arrows over the crossing.
- Drag: on press the widget notes the wires it moves (for an out run, every back wire from that output; otherwise the one hit), their stored runs, and the hit wire's routed runs. While dragging, each moved wire's live runs are its stored ones with the dragged part replaced by the hit wire's routed value plus the mouse movement (x for out, −x for in, y for the crossing). The editor hands those to the scene, and the canvas routes live, as for a lane drag.
- Release: the hit wire's routed value of the dragged part (after the limits) is stored on every moved wire, merged into its stored runs. One undo step, "Move Wire Lane".
- Delete/Backspace reset clears it. Resetting a back wire also clears the out run its output's back wires share, so the trunk goes back too. Tidy clears the runs of the wires it resets, as it does lanes, and waits while a run is being dragged.
- When the crossing is a single 45° diagonal, routing keeps the diagonal's middle where it fits, and that is the crossing y it reports, so a drag past the limit stores no hidden offset.
- Grip presses (see the grip fix on this branch): a selected or hovered wire's handle is still a handle near a grip; only a plain wire hit gives way to the grip.

## 5. Persistence

Settings JSON stays version 1 and gains an optional object:

```json
"backs": { "<id>": { "out": 40, "in": 18, "cross": -12 } }
```

- Parts are omitted when automatic. Non-number parts are skipped; an entry with no parts is dropped.
- Pruned with the other per-wire entries; dropped by `WithoutEdits`.
- An older Straight Wire reads past it, and drops it if it writes the file.

## 6. Testing

- Core unit tests first: each run moved and limited, a vertical beside a component, the parked-node and other-stretch cases, around above and below, trunk sharing, the 45° diagonal, `RouteResult.Backs`, fingerprint, JSON round trip and bad entries, `WithBack`, `WithoutEdits`, pruning, the segment-to-run mapping.
- Rhino check through the MCP (`tools/verify/back-drag.csx`): on the fixture, drag each of the three runs of a back wire and check the routed path, the stored setting, the undo name, and that Delete puts it back.
- Then the user's hands-on try.
