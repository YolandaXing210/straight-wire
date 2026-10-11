# Straight Wire Phase 3: Tidy, Design

- Date: 2026-10-11
- Status: approved in conversation (approach A and the design summary); written up here for review
- Builds on: phases 1 and 2 (`main`, released as v0.2.1) and their specs in this folder
- Reference: `tidy()` and `tidyScope()` in `docs/reference/straight-wire-prototype.html`

## 1. Goal

One command lays out a Grasshopper definition, or a selected part of it, the way the prototype does:
- components in columns that follow the data flow from left to right
- each input lined up with its upstream output, so wires run dead straight
- columns stacked without gaps
- the original top-to-bottom order kept

Success criteria (checked on the test fixture):
- After Tidy All, no two moved blocks overlap.
- Every wire between moved blocks runs left to right (its source block ends before its target block starts), except the wire dropped to break a cycle between groups.
- Each block's first input wire (as defined in 4.4, step 5) is dead straight, unless the gap rule pushed that block down.
- One Undo puts every object and every cleared wire edit back exactly.
- Tidy All on 500 components takes under 100 ms, not counting the re-route that follows.

## 2. Decisions made with the user

| Question | Decision |
|---|---|
| Port magnets | Use Grasshopper's own. Dragging a single object already snaps it vertically so a wire to a neighbour runs straight (`GH_DragInteraction.AddSnapAttributes`). No magnet code in this phase. |
| Groups | A group moves as one rigid block. Its inside keeps its layout. Selecting objects inside a group, without the group itself, tidies those objects within it. |
| Movement | Instant: objects jump to their new places. No animation. |
| Algorithm | Approach A: port the prototype's column layout. Rejected: full layered layout with crossing reduction (B; reorders vertically, much more code), and straightening only (C; doesn't tidy). |

## 3. Scope

In this phase:
- The Tidy command: menu item, Alt+T (Option+T on a Mac), and a Tidy button in the canvas widget.
- Tidy All and Tidy Selected.
- Groups as blocks.
- Clearing the wire edits that the move makes meaningless.
- One undo step per tidy.

Not in this phase:
- Magnets beyond Grasshopper's own.
- Animation.
- Moving unconnected objects out of the way.

## 4. Behaviour

### 4.1 Trigger

- **Menu:** the Straight Wire menu gains one item below "Reset Selected Wires", after a separator. Its label is "Tidy Selected" when the scope (4.2) is a selection, otherwise "Tidy All". The label is updated when the menu opens.
- **Shortcut:** Alt+T, Option+T on a Mac, with the same modifier rules as Alt+1/2/3.
- **Canvas widget:** a fourth button, "Tidy", after the three style buttons, separated by a wider gap.

When there is nothing to tidy (fewer than two linked blocks), Rhino's command line says "Straight Wire: nothing to tidy." and nothing changes.

### 4.2 Scope and blocks

A **block** is what the layout moves as one rectangle:
- a top-level object (a component, or a parameter on the canvas, which includes sliders, panels and relays), or
- a group block: a top-level group, i.e. one not inside another group, with everything inside it, nested groups included.

Groups that share an object are merged into one group block.

**What gets tidied:**
- **Selection:**
  - the selected groups (top-level among the selected ones, merged where they overlap)
  - the selected top-level objects not inside one of those groups
  - the source and target objects of the selected wires (phase 2's wire selection)

  If this gives two or more blocks, the tidy is **Tidy Selected** and only these blocks move. A selected object inside an unselected group is its own block; its group's box stretches to follow it.
- **Otherwise, Tidy All:** every top-level group becomes a group block, and every top-level object outside all groups becomes a block of its own.

**Never moved:**
- The Straight Wire marker component.
- Scribbles and sketches outside groups (inside a group block they move with it).
- Any block with no link (4.3) to another block in the scope. It stays where it is.

### 4.3 Links

- **A link** is a wire whose source and target top-level objects belong to two different blocks in the scope.
  - It carries the source block, the y of the output grip, the target block, and the y of the input grip.
- **Ignored:**
  - wires inside one block
  - wires to anything outside the scope
- Wire display (hidden, faint) and style (curve or straight) don't matter.

### 4.4 Layout (the prototype's `tidy`)

Notation:
- A **source block** has no incoming links.
- **Port offsets** are grip y minus the block's top.
- Constants: VG = 12 (vertical gap); the column gap uses the file's wire spacing SP.

1. **Depth.** Depth is the longest path from a source block.
   - Blocks are visited in order of current left edge, then top, then id.
   - A link that would close a cycle is dropped for the rest of the layout. Cycles can only form between group blocks.
2. **Sources move right.** A source block with outgoing links gets depth `max(0, min(depth of its targets) − 1)`. It sits just before the first block it feeds.
3. **Columns.** One column per depth; empty columns are skipped.
   - Column width is the widest block in it.
   - Source blocks are right-aligned in their column; other blocks are left-aligned.
   - The gap after a column is `max(64, 40 + P · SP · 1.2)`, where P is the number of distinct output grips with outgoing links in that column.
4. **Initial y.** Every block starts at its current top.
   - In each column, the source blocks are stacked by current top. The topmost stays put and the rest follow it VG apart.
5. **Alignment passes:** inputs, then sources, then inputs again. Each pass goes through the columns left to right.
   - **Inputs pass:** each non-source block wants the y that puts its first input grip level with the output grip feeding it.
     - "First" means the input link with the smallest port offset whose source is not a source block. If every source is a source block, the smallest port offset overall.
   - **Sources pass:** each source block with outgoing links wants the y that puts its first output grip (smallest offset) level with that link's input grip.
   - **Resolving a column:** sort the column by wanted y, ties by current order. Each block takes `max(wanted, previous bottom + VG)`.
6. **Anchor.** Translate the whole layout so its top-left corner lands on the top-left of the moved blocks' current bounding box, rounded to whole units.

The result is a new top-left for every moved block; a block whose position doesn't change is left out.

### 4.5 Moving

- **Each block moves by** its new top-left minus its current top-left.
- **Objects:** every object in the block gets that delta added to its `Attributes.Pivot`, followed by `ExpireLayout`. For a group block that is every object in the group, recursively, scribbles and sketches included. The groups themselves follow their members.
- **Routing:** the canvas then refreshes, and routing runs once on the next paint, since a tidy is not a drag.

### 4.6 Wire edits

- **Both ends moved:** lane positions and hand-drawn points of wires whose source and target objects both moved are cleared. These wires go back to automatic, as in the prototype.
- **One end moved:** hand-drawn wires keep phase 2's rule. Their points stay, the end segment stretches, and settling updates their anchors on the next paint.

### 4.7 Undo

- **One step:** a tidy is one undo step named "Tidy", held in a single `GH_UndoRecord`:
  - the pivot actions from `GH_UndoUtil.CreatePivotEvent` for every moved object
  - when edits were cleared, a `SettingsUndo` with the settings before and after
- **Order:** the settings are written before objects move, so that the next paint's settling sees the cleared edits.
- **Modified:** the document is marked modified.

## 5. Structure

| Unit | Responsibility |
|---|---|
| `Core/Tidy.cs` | Pure layout. `Tidy.Layout(IReadOnlyList<TidyBlock> blocks, IReadOnlyList<TidyLink> links, double spacing) → IReadOnlyDictionary<string, Pt>` (new top-left per moved block). `TidyBlock(id, Rect bounds)`; `TidyLink(sourceBlock, sourceY, targetBlock, targetY)`. |
| `Core/TidyScope.cs` | Pure block building: from objects (id, bounds, top-level, is marker, is annotation), groups (id, member ids recursive, selected), the selection and the wire list, it builds the blocks, the object-to-block map and the links. |
| `Tidier.cs` (plugin) | Reads the document into `TidyScope`'s input, runs `Tidy.Layout`, clears wire edits, moves objects, pushes the undo record. Runs under `Log.Guard`. |
| `DocStore` | Gains a write that returns its `SettingsUndo` instead of pushing it, so the tidy can put it into its own record. |
| `UI/StyleMenu.cs`, `UI/StyleWidget.cs`, `CanvasHook.cs` | Menu item, Tidy button, Alt+T. |

## 6. Errors and safety

- **Failure before moving:** if `Tidier` throws before any object moved, it reports once through `Log` and changes nothing.
- **Failure while moving:** all new positions are computed before the first object moves. If moving still fails, objects already moved go back to their recorded pivots and the settings write is reverted.
- **Unknown objects:** the plugin skips anything `TidyScope` can't classify (an object with no attributes).
- **Which document:** a tidy acts only on the active canvas's document.

## 7. Testing

**Core unit tests (written first):**
- **Columns:**
  - a chain A→B→C makes three columns, left to right, gaps as in 4.4.3
  - a source feeding a depth-3 block sits in column 2, right-aligned
- **Alignment:**
  - after tidy, each block's first input grip is level with its source grip
  - two blocks wanting the same y end up VG apart, in their old order
  - sources stack VG apart from the topmost source's top
- **Order:** blocks in one column keep their current top-to-bottom order when nothing forces otherwise.
- **Cycles:** group blocks G1→B→G1 produce a layout, with one link dropped.
- **Anchor:** the layout's top-left lands on the old bounding box's top-left.
- **Unmoved blocks:** blocks without links are absent from the result.
- **Determinism:** the same input always gives the same output.
- **Performance:** 500 blocks with 800 links lay out in under 100 ms.
- **`TidyScope`:**
  - selection rules
  - a selected member of an unselected group becomes its own block
  - overlapping groups merge
  - the marker and loose scribbles never move
  - links between blocks only
  - fewer than two blocks with a selection falls back to Tidy All

**Rhino checks through the MCP** (`tools/verify/tidy.csx` on the fixture):
- **After Tidy All:**
  - no overlaps
  - every link left to right
  - count of straight first-input wires
  - render to `artifacts/verify/tidy.png`
- **Undo:** every pivot and the settings JSON are back exactly; Redo repeats the tidy.
- **Tidy Selected** of three components moves only those three.
- **A group block** keeps its members' relative positions.

**Finally the user's hands-on try.**

## 8. Known limits

- Objects that stay put (no links in the scope) can end up under tidied blocks.
- Links to objects outside the scope don't influence the layout.
- A group block is laid out by its outer box. Ports deep inside a tall group align with their grips, so a tall group can push its column down.
