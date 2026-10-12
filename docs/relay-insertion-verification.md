# Visible-path Relay insertion fix

## Implementation

- `VisibleWires` captures completed paint geometry and source/recipient pairs. Routed
  polylines are the ones supplied to drawing. Curves use public `GH_Painter.ConnectionPath`,
  flattened to a quarter screen pixel. Hit tolerance is six screen pixels at every zoom.
- `RelayWidget` handles only left double-clicks while Wire Edit Mode is OFF. Single presses,
  moves and releases return Ignore. Component bodies, grips and active interactions retain
  priority. Ambiguous hits show a connection menu; cancellation does not edit the document.
- Invisible superseded native curves are intercepted before Grasshopper's native insertion;
  a ghost-only hit opens the public component search box. Visible candidates take priority.
- `RelayInsertion` creates `GH_Relay` through public APIs. It replaces exactly one source in
  the target's ordered source list, preserving other source/recipient connections and the
  recipient's processing settings. It does not copy, flatten or cast data itself.
- One Undo record contains native object/wire actions and the before/after Straight Wire
  JSON. Settings restoration does not synchronously repaint halfway through graph undo.
- `RelaySplit` rekeys only the affected connection. Style overrides are inherited; hand-edited
  bends are split in order. Fixed lanes become manual pieces, rather than copying a relative
  lane fraction into a different geometric span. Unedited wires continue automatic routing.
- Stale documents, changed connections, changed settings/zoom and unreadable/newer settings
  are rejected before insertion. A failed graph transaction restores the prior sources and
  settings. Successful insertion is not rolled back if subsequent solution evaluation fails.
- No private Grasshopper methods, runtime patches or replacement document/canvas types are
  used by production code. The old installed Phase 1 plugin was not replaced.

## Verified on 2026-10-12

- Release build: 0 reported warnings, 0 errors. Existing suppressed warning codes remain
  NU1701, NU1603, NETSDK1138, CS8632, CA1416 and MSB3277.
- Core suite: **177 passed**, including 7 route/settings migration tests.
- Rhino 8.30.26103.11002, isolated documents: **180 checks passed** across Curve,
  Orthogonal, 45°, custom routes, fixed lanes, horizontal segments, 50% and 200% zoom.
  Checks cover native Relay type, placement within native layout rounding, ordered multiple
  inputs, fan-out preservation, numeric data-tree paths/values, single Undo, exact JSON
  restoration, Redo with the same Relay ID, stale-event protection and Edit Mode gating.
- Existing Wire Edit Mode isolated-canvas suite: **26 checks passed**.
- Real mouse test in a disposable independent canvas: visible Orthogonal insertion produced
  exactly one Relay with an `Insert Wire Relay` Undo record. The ghost-path check displayed
  component search and left the test document with no Relay.
- The final ambiguity-menu cancellation/selection scenario is **not verified**. A rerun
  after reconnecting MCP on October 12 crashed Rhino at 15:47:50 local time. The macOS
  report `Rhinoceros-2026-10-12-154754.ips` records SIGABRT on the main thread through
  `xamarin_process_nsexception_using_mode`; it does not identify the managed failing call.
  Before this run, Grasshopper had no active document and loaded the installed Phase 1
  assembly (MVID `3d8a9dcf-6479-4952-86bd-9fd71a86e5f3`). No installation was changed.
  An unparented popup owner is a hypothesis, not a confirmed cause. The verification
  harness now writes a persistent temporary trace, defaults to the nine non-menu scenarios,
  and isolates the opt-in menu scenario on a Form-hosted canvas. These harness changes have
  not yet been run. No production code was changed in response to the crash. Installation
  of this fix should wait until the crash is isolated and the menu scenario passes.
- A focus change during real mouse testing caused one accidental native Relay insertion in
  the user's Nozzle document. Its exact Undo record was identified and undone, restoring
  the previous graph. A redo entry may remain. No user document was saved. The disposable
  test window was closed; the installed plugin was not unloaded or replaced.

## Remaining acceptance checks

1. After an approved installation/restart, confirm the new build is loaded from Libraries.
   Assembly version remains 0.2.1, so compare the installed SHA-256 to the Release file;
   do not use the version string alone as proof.
2. OFF: real mouse double-click Curve, Orthogonal horizontal/vertical, 45° diagonal/horizontal,
   and a saved manually edited route. Exactly one real Relay should appear at the click
   (Grasshopper rounds its layout to canvas coordinates).
3. Ghost-only Curve location: search opens, no Relay is created. Ordinary empty-space search,
   parameter menus, panel editing, component movement, panning and zooming still work.
4. Shared/overlapping segment: menu identifies each source/recipient; cancel changes nothing;
   select one and verify only that connection changes. Also test changing document or mode
   while a choice is pending. Never choose an arbitrary first connection.
5. ON: existing routed-wire hover, selection, point insertion and point/segment dragging
   continue. The Relay bridge is disabled. This fix does not add control-point editing for
   pure Curve-style wires, which the existing editor does not support.
6. Undo/Redo insertion repeatedly, save/reopen a disposable fixture, and inspect custom bends,
   inherited styles, source ordering and downstream tree paths/data types.
7. After Rhino is reachable, first run `tools/verify/relay-insertion.csx` with its default
   `testAmbiguousMenu = false`. Inspect `StraightWire-relay-verification.log` under
   `Path.GetTempPath()` before any further attempt. Then separately enable the menu scenario
   on the hosted disposable canvas. Do not repeat the earlier unhosted popup test.
   `tools/verify/relay-ui.csx` can open a standalone disposable test
   window. Confirm that window is frontmost before every click; search can shift focus back
   to the normal Grasshopper editor.

## Limits

- Component/grip interaction zones intentionally retain native behavior, including native
  multi-wire grip insertion. The bridge operates on wire segments outside those zones.
- Near an inserted Relay, its actual input/output grips can change the route locally;
  automatic routes can recompute after the new object becomes an obstacle.
- The connection menu uses short IDs to distinguish duplicate labels and full IDs in tooltips.
- Insertion is refused for settings that cannot safely be read/written; it does not silently
  discard them. When drawing falls back to native curves, native geometry remains authoritative.

## Build and installation

Release file: `src/StraightWire/bin/Release/StraightWire.gha`.
Only the GHA is needed, following the repository's existing manual macOS installation.

1. Obtain approval before replacing the current working installation.
2. Save work and fully quit Rhino. Back up the currently installed Phase 1 GHA to a new
   timestamped folder outside Grasshopper Libraries (the earlier backup predates Phase 1).
3. Copy the new Release GHA to the confirmed Libraries location and compare SHA-256 hashes.
4. Start Rhino manually, open Grasshopper, and run the acceptance checks on a copy of a file.
5. To roll back, quit Rhino and restore the just-created Phase 1 backup to the same path.

No commit, push, branch switch, merge, PR or installation was performed for this fix.
