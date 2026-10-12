# Wire Edit Mode verification and delivery

This records the initial Phase 1 build. The subsequent visible-path Relay fix supersedes
the native hit-area limitation below; see [Relay fix verification](relay-insertion-verification.md).

## Verified

- Branch: `feature/wire-switch_wire-editing-mode`; no commit, push, merge or PR.
- Release build: succeeded, 0 reported warnings and 0 errors.
- Existing warning suppression remains unchanged: NU1701, NU1603, NETSDK1138,
  CS8632, CA1416, MSB3277. Zero reported warnings does not mean these were audited.
- Core tests: 170 passed, 0 failed, 0 skipped. The initial sandbox run could not
  bind the test runner's local port; the permitted retry passed.
- Rhino host: 8.30.26103.11002 on macOS. `tools/verify/wire-edit-mode.csx`
  passed 26 assertions using a separate assembly and two isolated canvases.
- Covered: default OFF, rendering while OFF, mouse response bypass, menu position
  and checked state, shared visibility, hover/select/drag while ON, cancellation,
  late mouse-up protection, foreign-widget preservation, saved control points,
  document modified state, Undo preservation, and repeated toggling.
- Active user document and running plugin were not replaced. The running build
  reports assembly context `StraightWire.Verification` with an empty Location:
  it is loaded from memory, so its on-disk installation path is not established.

## Files and behavior

The new `WireEditMode` owns the session switch. Menu and editing-widget visibility
share it. Canvas hooks gate hover and editing keys; widget handlers gate every
mouse entry point. Disabling clears transient state and cancels only this widget's
capture. Rendering and command targeting ignore custom selections while OFF.
The old persistent EditingEnabled preference is ignored; saved document schemas,
router algorithms, component selection behavior and style shortcuts are unchanged.
DevHost adds `edit-mode:on` and `edit-mode:off`, and reports the state in Describe.
README, INSTALL and verification scripts describe the new behavior.

## Manual A–E acceptance still required

These checks require loading the updated plugin and using real mouse input;
constructed widget events do not validate native Grasshopper event dispatch.

1. **A — Startup:** restart Rhino after an approved installation, open an existing
   styled file, confirm Wire Edit Mode is unchecked and routes still render.
2. **B — OFF:** hover/click wires: no custom glow, handles or wire selection.
   Double-click a native Curve wire to insert a Relay. Also test visible routed
   wires and record any hit-area mismatch; this phase passes events through and
   does not implement custom Relay insertion.
3. **C — ON:** enable the menu item, test hover, click/Shift-click, double-click to
   add/remove points, lane/point/segment dragging, Option snapping override,
   selected-wire style changes and Undo.
4. **D — Disable:** save a custom point, select/hover the wire, disable mode, and
   confirm the committed shape remains while handles and custom selection vanish.
   Re-enable and edit again. Toggle during a drag through DevHost if necessary;
   no pending edit should commit on mouse-up.
5. **E — Regression:** check Curve/Orthogonal/45°, Wire Spacing, existing saved
   settings after reopening, component selection and movement, pan/zoom, right-click
   menus, Delete/Backspace/Escape, and Option+1/2/3.

## Build, installation and rollback

Build output: `src/StraightWire/bin/Release/StraightWire.gha` (single plugin file).

```sh
dotnet build src/StraightWire -c Release --no-restore --nologo
dotnet test tests/StraightWire.Core.Tests -c Release --no-restore --nologo
```

No installation was performed. Before installation, identify the actual `.gha`
location using Grasshopper's File > Special Folders > Components Folder and its
loaded-assembly information. Save work and exit Rhino. Back up the old file outside
Libraries, replace only the confirmed copy, then restart Rhino. Restore the backup
with Rhino closed to roll back. Do not leave duplicate plugin copies in Libraries.
Replacing the working installation requires the user's separate approval.
