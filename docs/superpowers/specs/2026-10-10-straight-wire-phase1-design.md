# Straight Wire Phase 1: Rendering, Design

- Date: 2026-10-10
- Status: awaiting review
- Reference prototype: `docs/reference/straight-wire-prototype.html` (the validated interaction prototype v3 and the reference implementation for routing; also published at https://claude.ai/artifact/7FCSD7oFFuuaeWbBZTSuwP)

## 1. Goal

Replace the bezier wires between components on the Grasshopper canvas with straight wires that look like PCB traces. Wires run at right angles or at 45°, sit in separate lanes, avoid components, and wires from the same output share a trunk.

Phase 1 covers only what you see: rendering the automatic routing, plus switching styles for all wires or the selected ones, and saving those settings. Editing is phase 2 and tidying is phase 3; each gets its own design later.

Success criteria:
- With the plugin installed in Grasshopper on Rhino 8 (Mac and Windows), wires are drawn straight according to the prototype's rules, and no native wire shows through.
- Routing is recomputed every frame while a component is dragged, without lag. `Router.Route` on a scene with 500 components and 800 wires has a median time under 10 ms.
- Styles are saved in the .gh file:
  - Someone else with the plugin installed who opens the same file sees the same styles.
  - Someone without it is offered the install by Rhino once the plugin is on the Package Manager.
- Whatever error the plugin hits, native wires never disappear and Grasshopper never crashes.

## 2. Decisions already made

| Question | Decision |
|---|---|
| Phases | Rendering first (this phase), then editing, then tidying |
| Platform | Rhino 8 only, Mac and Windows, a single net7.0 build |
| Sharing | Plugin plus a Straight Wire marker component in the file. Users without the plugin get an install prompt when they open the file. Published to the Rhino Package Manager (Yak) |
| How wires are drawn | Approach A: during the wire paint pass, Grasshopper's wire skin colours are set to transparent and we draw our own wires. Validated on Rhino 8 for Mac |
| Animation | None. Style changes, lanes making room and 45° wires straightening all jump straight to their final state |
| Plugin UI language | English, because Grasshopper's own UI is English and the plugin will be published |

## 3. Scope

In this phase:
- Three styles: Curve (native curves), Orthogonal (right angles) and 45°.
- Automatic routing following the prototype's rules (section 6).
- Grasshopper's visual meaning is kept:
  - item, list and tree wire types
  - the orange wire for empty data
  - selection highlight
  - the faint and hidden wire display settings
  - the user's skin colours
- A global style, per-selection overrides and a wire spacing setting. Per-selection means the wires with at least one end on a selected component.
- Settings are saved in the .gh file together with a marker component. There is also a personal default style.
- A Straight Wire menu, a switch widget in the bottom-left corner of the canvas, and keyboard shortcuts.
- Yak packaging: a manifest and a build script.

Not in this phase:
- Phase 2: clicking to select a wire, dragging a lane (push-shove), custom bend points and undo.
- Phase 3: tidying all or selected components, and port magnets.
- Animation.
- Keeping per-selection overrides on copy and paste. Pasted wires follow the global style.
- Style changes in Grasshopper's undo history (phase 2).
- Windows users who switched Rhino to the .NET Framework runtime. A net7.0 plugin does not load in that mode.

## 4. Terms

- **Node:** a top-level active object on the canvas (`IGH_ActiveObject`): components, parameters, sliders, panels, relays and clusters. Its rectangle is `Attributes.Bounds`. Groups, scribbles and markups are not nodes and wires do not avoid them.
- **Port:** the source parameter's `Attributes.OutputGrip`, or the target parameter's `Attributes.InputGrip`.
- **Wire:** for a target parameter `p`, each `s` in `p.Sources` makes one wire `s → p`. Its id is `s.InstanceGuid + ">" + p.InstanceGuid`.
- **Same-source group:** wires with the same source parameter (`a, ai` in the prototype).
- **Lane:** the x position of the vertical segment of a forward wire that has to bend.

## 5. Structure

```
straight-wire/
  src/StraightWire.Core/          netstandard2.0, pure C#, no Grasshopper reference
  src/StraightWire/               net7.0, builds StraightWire.gha
  tests/StraightWire.Core.Tests/  xUnit
  yak/                            manifest.yml, icon
  docs/
```

Dependencies point one way: StraightWire depends on StraightWire.Core. Core does not know Grasshopper exists, and everything Grasshopper-specific lives in the plugin project. The plugin references the Grasshopper 8.0.x NuGet package for compilation only, so the package is not shipped. That means the plugin loads on any Rhino 8 service release.

### 5.1 StraightWire.Core

| Unit | Responsibility |
|---|---|
| `Geometry` | Points, rectangles, `SegDist`, `SegSegDist`, `PolyPolyDist`, `SegRectHit` (Liang–Barsky), bounding boxes |
| `Scene` | The input: nodes (id, rectangle), wires (id, source node, target node, same-source group key, both endpoints, style), and the wire spacing |
| `Router` | `Route(Scene, RouterState) → RouteResult`: a polyline for each wire plus the junction dots. `RouterState` keeps each wire's previous lane x for hysteresis |
| `SpatialGrid` | A uniform grid index of nodes and of the polylines already placed, used for avoidance and collision checks |
| `Junctions` | The split points (junction dots) of each same-source group |

Each wire's style is `Curve | Orthogonal | Diagonal`. Core produces no polyline for Curve wires, which the plugin draws natively, and they take no lane.

Determinism: the same `Scene` with the same `RouterState` always gives the same result, whatever order wires and nodes are listed in. Core sorts them by id internally.

### 5.2 StraightWire (.gha)

| Unit | Responsibility |
|---|---|
| `StraightWireInfo` | `GH_AssemblyInfo`: name, version, author, and a fixed Id GUID that the Package Manager uses to find the plugin |
| `Loader` | `GH_AssemblyPriority.PriorityLoad`: subscribes to `Instances.CanvasCreated` and builds the menu |
| `CanvasHook` | One per canvas. Hooks `CanvasPrePaintWires`, `CanvasPostPaintWires` and `CanvasPaintEnd`, and manages the skin colours and the cache |
| `SceneReader` | Reads Core's `Scene` from a `GH_Document` and computes the layout fingerprint |
| `WireRenderer` | Draws a `RouteResult` onto `canvas.Graphics` with System.Drawing |
| `DocSettings` | Reads and writes the file's settings (ValueTable JSON) |
| `UserSettings` | The personal defaults (`GH_SettingsServer("StraightWire")`) |
| `Commands` | Set style, set spacing, follow global, save to file, make this my default. The menu, widget and shortcuts all call these |
| `StyleMenu` | The Straight Wire menu in Grasshopper's main menu bar |
| `StyleWidget` | A `GH_Widget` in the bottom-left corner of the canvas |
| `MarkerComponent` | The marker component |
| `DevHost` | For development: the Attach / Detach entry used when loading into an unloadable context |

## 6. Routing rules

These are ported directly from the prototype (`docs/reference/straight-wire-prototype.html`):
- the lane functions `feasible`, `relax` and `crossCount`
- the 45° decision `decideDiag` and `touches`
- backwards wires: `backChannel` and `backDiag`
- path building: `orthoPath`, `diagPath`, `chamfer`, `simplify` and `dedupe`
- drawing helpers: `offsetPath`, `diverge` and `junctions`

The branches for animation, dragging (`pin`, `dragX`) and manual points (`manual`, `pts`) are removed.

Constants, in canvas units (pixels at zoom 1):

| Name | Value | Used for |
|---|---|---|
| `STUB` | 18 | Shortest horizontal run out of a port |
| `CH` | 14 | Size of a 45° chamfer |
| `DSTUB` | 6 | Shortest horizontal run at each end of a single diagonal |
| `CLR` | 8 | Clearance kept around nodes |
| `SP` | Default 10; options 6 / 8 / 10 / 12 / 16 / 20 | Wire spacing |

Each layout runs these steps:

1. **Classify.** With `dx = bx − ax` and `dy = by − ay`:
   - `dx < 8`: a backwards wire (back).
   - `|dy| < 0.5`: a straight wire, drawn as one segment.
   - Otherwise: a lane wire.
2. **Feasible lane range (`feasible`).**
   - Start from `[ax + STUB, bx − STUB]`.
   - Cut out the nodes (expanded by `CLR`) that overlap the wire's vertical range.
   - Shrink the range when a node blocks the source or target port.
   - If several free stretches remain, prefer the one holding the previous lane (distance ≤ 1, the hysteresis). Otherwise take the widest, and the rightmost when widths tie.
   - If everything is blocked, fall back to the whole range.
3. **Spread the lanes (`relax`).**
   - Each wire's preferred position is the middle of its range.
   - Two wires form a pair when their vertical ranges (expanded by `SP`) overlap and their lane ranges could clash. The wires of a same-source group always pair with each other, whatever their vertical ranges.
   - The left-right order of a pair is the one with fewer crossings (`crossCount`). Ties break on previous lane position, then preferred position, then id.
   - Same-source wires attract each other, so they share a trunk.
   - It runs 32 iterations. Each one pushes apart any pair closer than `SP` and clamps every lane back into its range.
4. **Orthogonal style:** `[[ax,ay],[x,ay],[x,by],[bx,by]]`.
5. **45° style (`decideDiag`).**
   - The default has four bends, with `CH` chamfers at the lane (`diagPath(x, CH)`).
   - Wires are processed from smallest to largest `|dy|`. Each tries a single diagonal with two bends (`diagPath(x, h)`, where `h = |dy|/2`).
   - The diagonal's x tries offsets from the lane: 0, ±SP, ±2SP … ±8SP, then the two ends of the range. It must stay within `[ax + DSTUB + h, bx − DSTUB − h]`.
   - The diagonal is kept only if it touches no more than the four-bend version does. Touches count:
     - other wires closer than `0.6·SP`
     - nodes it crosses, expanded by 3

     Wires in the same source group do not count against each other.
   - When `h ≤ CH`, the single diagonal is used directly.
6. **Backwards wires (`backChannel`).**
   - When the two nodes are offset vertically with a gap of at least `2·CLR`, the crossing runs through that gap. It skips other nodes sitting in the gap whose x range overlaps, and uses the centre line of the largest free stretch.
   - Only when there is no gap does the wire go around, above or below at 24 from the nodes, on the side with less total vertical travel.
   - In 45° style, if the gap is tall enough the crossing becomes one diagonal (`backDiag`). Otherwise it uses the right-angle route and chamfers its corners.
7. **Chamfers.** In 45° style every right-angle corner gets a `CH` chamfer. `chamfer` cuts only corners of about 90°; corners that are already 45° are left alone.
8. **Clean up.** `dedupe` removes repeated points and `simplify` merges collinear segments.
9. **Junction dots.**
   - For each pair of wires in a same-source group, find where they split (`diverge`): the point where the two polylines get more than 0.6 apart, refined by bisection.
   - Drop dots closer than 3 to the port, and merge dots closer than 1.5 to each other.
   - A group containing a Curve wire gets no dots.

## 7. Drawing

### 7.1 Flow on each repaint

`CanvasPrePaintWires(canvas)`:
1. Get `canvas.Document`. If there isn't one, return.
2. Resolve each wire's style: its per-selection override, else the file's global style, else the personal default.
3. If every wire is Curve, do nothing. Grasshopper draws native wires and the plugin costs nothing.
4. `SceneReader` reads the scene and computes the fingerprint from:
   - node ids and rectangles
   - wire ids and endpoints
   - each wire's style
   - `SP`

   `Router.Route` runs only when the fingerprint differs from the cached one. `RouterState` is stored with the cache.
5. Save the four wire colours (`wire_default`, `wire_empty`, `wire_selected_a`, `wire_selected_b`), set them to transparent, and mark the canvas as taken over.

`CanvasPostPaintWires(canvas)`:
1. Restore the four colours in a `finally` block. They must be restored before we draw, because our pens use them.
2. Draw our wires (7.2).

`CanvasPaintEnd(canvas)`: if the canvas is still marked as taken over (Post did not run), restore the colours.

Selection changes only colour and is not part of the fingerprint, so clicking to select a component does not re-route.

### 7.2 How wires are drawn

- **Colour and gradient.** Use Grasshopper's own `canvas.Painter.GenerateWirePen_Fill(a, b, asel, bsel, empty)`:
  - `asel` and `bsel` say whether the source and target top-level objects are selected.
  - `empty` means the source parameter has no data.

  This matches the native selection highlight, the orange empty-data wire and the user's skin colours.
- **Wire type.** `GH_Painter.DetermineWireType(...)` decides item, list or tree from the source parameter's `VolatileData`; check the argument against native behaviour during implementation.
  - Item is one line.
  - List is two parallel lines (`offsetPath` ±d).
  - Tree is two parallel dashed lines.

  Line width, offset and dash pattern are taken from a native render at zoom 1 during implementation.
- **Faint display** (target parameter `WireDisplay == faint`): the wire is routed and drawn with the width and transparency of native faint wires, measured from a native render. When one end is selected it draws normally.
- **Hidden display:** the wire is not routed. Whatever native Grasshopper shows for a hidden wire when one of its ends is selected, we show the same thing, drawn along the native curve from `GH_Painter.ConnectionPath`. Native behaviour is checked against a native render during implementation.
- **Curve-style wires** (the global style is straight but this wire is overridden to Curve): not routed, drawn as native curves with `GH_Painter.ConnectionPath`.
- **Junction dots:** filled circles of radius 2.6, in the group's wire colour.
- **Viewport culling:** only wires and dots whose bounding box intersects `canvas.Viewport.VisibleRegion` are drawn.
- **Zoomed out:** below zoom 0.5, junction dots are not drawn.

## 8. Styles and actions

Style resolution: a wire's style is its per-selection override, else the file's global style, else the personal default (initially 45°). Spacing is the file's spacing, else the personal default (initially 10).

| Action | With components selected | With nothing selected |
|---|---|---|
| Curve / Orthogonal / 45° | Sets an override on the wires with at least one end on a selected component | Sets the file's global style |
| Wire spacing ▸ 6…20 | Sets the file's spacing | Sets the file's spacing |
| Follow global | Clears the override on those wires | Disabled |
| Save to this file | Writes the settings in effect into the file | Same |
| Make this my default | Makes the current file's global style and spacing the personal default | Same |

Entry points:
- **Menu.** A new "Straight Wire" menu in Grasshopper's main menu bar (`DocumentEditor.MainMenuStrip` is confirmed to work on Mac). When components are selected, the style items show the scope, for example "Orthogonal (6 selected wires)". The current style is checked.
- **Canvas widget.**
  - Three small buttons in the bottom-left corner (Curve / Ortho / 45°), with the scope written next to them (All / 6 selected).
  - It is a `GH_Widget`, so it can be hidden from Grasshopper's widget menu. Its visibility is stored in the personal settings.
- **Shortcuts.** With the canvas focused, Alt+1 / 2 / 3 (Option on Mac) pick the three styles. If Option+digit does not reach the canvas's KeyDown on Mac, use Ctrl+Alt+digit instead and update this section.

## 9. Saving and sharing

### 9.1 File settings

Stored under `"StraightWire.v1"` in `GH_Document.ValueTable`. A save-and-reopen round trip through a .gh file is already validated.

```json
{
  "v": 1,
  "mode": "diag",
  "spacing": 10,
  "wires": { "<source param InstanceGuid>><target param InstanceGuid>": "ortho" }
}
```

- `mode` is `"bezier"`, `"ortho"` or `"diag"`.
- `wires` lists only wires that have an override. Wires that no longer exist are removed on write.
- **No key in the file:** use the personal defaults and write nothing to the file. Opening the file does not mark it as modified.
- **First change in a file:** the first time you change the style, spacing or a per-selection override, or click Save to this file:
  - the settings are written
  - a marker component is added if there isn't one
  - `doc.Modified()` marks the file as modified
- **Unreadable (broken JSON):** use the personal defaults and say so once on the command line. The next change overwrites it with the new settings.
- **Version newer than 1:** use the personal defaults, say so once on the command line, and never overwrite it. Style actions in that file tell you to update the plugin.

### 9.2 Personal defaults

Stored in `GH_SettingsServer("StraightWire")` with the keys `DefaultMode`, `DefaultSpacing` and `WidgetVisible`.

### 9.3 Marker component

- **What it is:**
  - a `GH_Component` with no inputs or outputs
  - name Straight Wire, nickname SW, category Params / Util
  - a fixed GUID
  - exposure hidden, so it is not on the toolbar and only the plugin adds it
- **What it shows:**
  - `Message` shows the current settings, for example `45° · 10`.
  - Its description explains that it gives people without the plugin an install prompt when they open the file, and that deleting it does not affect routing.
- **Placement:** 40 above the top-left corner of the bounding box of all nodes, or at the canvas origin in an empty document.
- **Adding it:** it is checked for and added only when settings are written. If the user deletes it, it is not added back straight away, only on the next settings write.
- **Solving:** it takes no part in the solution; `SolveInstance` is empty.

### 9.4 When someone else opens the file

- **With the plugin:** they read the file's settings and see the same routing.
- **Without it:**
  - They see native curves.
  - Grasshopper reports the missing plugin. The marker component in the file lets the Rhino 8 Package Manager find Straight Wire and offer to install it.
  - This only works after the plugin is published to Yak. After publishing, verify it once in an environment without the plugin.

### 9.5 Packaging and publishing

- `dotnet build -c Release` produces `StraightWire.gha` and `StraightWire.Core.dll`.
- `yak/manifest.yml` gives the name `straight-wire`, a version matching `GH_AssemblyInfo`, the author, description, icon and project link.
- Rhino's bundled `yak` (`/Applications/Rhino 8.app/Contents/Resources/bin/yak`) runs `yak build` to produce the package, for the single `any` platform.
- You run the final `yak login` and `yak push` with your own McNeel account.

## 10. Safety and error handling

- **Native wires always come back.**
  - The colours are restored in the `finally` of PostPaintWires, with PaintEnd as a second safety net.
  - Pre saves the colours only when the canvas is not already taken over, so transparent is never saved as the original colour.
  - The colours change only in memory and are never written to Grasshopper's skin file.
- **Routing or drawing throws an exception:**
  - The exception is caught and reported once on the command line.
  - That canvas goes back to native wires and stops taking over until the user picks a style again, which retries. Grasshopper does not crash.
- **Broken or too-new file settings:** see 9.1.
- **Plugin uninstalled or disabled:** the ValueTable entry and the marker component in the file are inert and do not affect solving.

## 11. Performance

- **Fingerprint cache:** when the layout has not changed (only panning, zooming, selecting or solving), the cached polylines are reused without routing.
- **Dragging:** the fingerprint changes every frame and routing runs again.
- **Target:**
  - The test scene is generated with 500 nodes and 800 wires, most of them needing bends.
  - The median time of `Router.Route` on it must be under 10 ms, in Release on an M-series Mac.
- **`SpatialGrid`:** the node lookups in `feasible` and the collision checks in `touches` go from scanning everything to scanning nearby cells.
- **`relax` pairing:**
  - Pairs come from a sweep over wires sorted by y, so only wires with overlapping vertical ranges are paired, plus the wires within each same-source group. There is no full n² pairing.
- **Drawing:** viewport culling, and junction dots skipped when zoomed out.
- **Curve only:** when every wire is Curve, the plugin costs nothing.

## 12. Testing

Core (xUnit, tests written first):
- **Endpoints:** every wire's first and last points are exactly its two ports.
- **Angles:**
  - in Orthogonal style, every segment is horizontal or vertical
  - in 45° style, every segment is at 0°, 45° or 90°
- **Spacing:** lanes whose vertical ranges overlap are at least `SP` apart, when their ranges allow it.
- **Avoidance:** when there is room, a lane never crosses a node (expanded by `CLR`).
- **45° with nothing in the way:** only two bends.
- **Backwards wires:** when the two nodes are offset vertically, the crossing lies in the gap between them. In 45°, if the gap is tall enough, the crossing is one diagonal.
- **Shared trunk:** wires from the same source share a trunk, with a junction dot where they split.
- **Determinism:** shuffling the input order gives the same result.
- **Hysteresis:** moving a node by 1 unit does not make a lane jump to another free stretch.
- **Performance:** the section 11 scene runs in under 10 ms.

Plugin (in Rhino, through the MCP):
- **Render check:**
  - Build test definitions through the MCP, including the twisted tower from the prototype.
  - Render the canvas to an image with `GenerateHiResImageTile`.
  - Check that no native wire is left, and that wire type, colour, selection, empty data, faint and hidden are all correct.
- **Skin colours:** after rendering, the four skin colours match their originals.
- **Settings round trip:** settings written, saved and reopened stay the same, and a file without settings is not modified after opening.
- **Failure fallback:** make routing throw on purpose and confirm the canvas goes back to native wires.
- **Hands-on:** finally, you install the plugin and try it yourself.

## 13. Development loop

- **Hot reload:** after each build, load the new `StraightWire.dll` build into the running Rhino through the MCP, using an unloadable `AssemblyLoadContext`, and call `DevHost.Attach()`.
  - Before the next load, call `Detach()` and unload. Detach removes the paint hooks, the menu and the widget.
  - Grasshopper, GH_IO and RhinoCommon resolve from the default context, so their types are shared.
  - No Rhino restart is needed.
- **What hot reload covers:** only canvas drawing, the menu and the widget. It does not register the marker component, because Grasshopper's component registration cannot be undone.
- **Real install testing:** the marker component and the Package Manager flow are tested with a real install. Copy the .gha to `~/Library/Application Support/McNeel/Rhinoceros/8.0/Plug-ins/Grasshopper/Libraries/` and restart Rhino.

## 14. Left for later phases

- Phase 2: clicking to select a wire, dragging a lane (push-shove, `pin`), custom bend points (`manual`), style changes in undo, and overrides kept on copy and paste.
- Phase 3: tidying all or selected components, and port magnets.
