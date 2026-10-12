# Straight Wire

Grasshopper wires as straight, circuit-board style traces — right-angle or 45°, each in its
own lane, around components, with wires from the same output sharing a trunk. Rhino 8,
Windows and macOS.

The interaction was designed in a browser prototype first:
[`docs/reference/straight-wire-prototype.html`](docs/reference/straight-wire-prototype.html).
Enable **Straight Wire > Wire Edit Mode** to edit wires by hand: click to select, drag a lane sideways, drag or double-click to
add and remove bend points, Delete to reset. Every edit is one Grasshopper undo step and is
saved in the file. Wire Edit Mode starts OFF for each plugin session and is never saved.
When OFF, saved routes still render, but custom hover, selection, handles and editing keys
are inactive. Switching OFF cancels uncommitted drags without deleting saved edits.
When OFF, double-clicking a visible wire inserts a native Grasshopper Relay. If several
connections overlap within the hit tolerance, choose the intended connection from the menu.
Double-clicking only an invisible, superseded curve opens search instead of inserting a Relay.
Components and parameter grips retain their native gestures.

Tidy lays a definition out the same way the prototype does: components in columns along the
data flow, each input lined up with the output feeding it so wires run straight, columns
stacked without gaps, top-to-bottom order kept. With two or more components or groups
selected it tidies only those. A group moves as one block and keeps its inside as it is.
A tidy is one undo step.

## How it works

Grasshopper draws every wire between two canvas paint events, with pens built from four skin
colours. Straight Wire makes those colours transparent for that moment, routes the wires
itself (`src/StraightWire.Core`, no Grasshopper dependency, unit-tested), and draws the routes
with Grasshopper's own pens, so list, tree, empty, faint and selected wires look the way they
always do. Routes are cached against a fingerprint of the layout, so panning, zooming and
solving cost nothing. While a component is dragged its wires are drawn as Grasshopper curves,
and everything re-routes once when it is dropped.

Wire styles live in the `.gh` file (`GH_Document.ValueTable`, key `StraightWire.v1`) along
with a small marker component that makes Grasshopper list Straight Wire as a required plugin.

## Build and test

```bash
dotnet test tests/StraightWire.Core.Tests          # routing, settings
dotnet build src/StraightWire                       # -> src/StraightWire/bin/Debug/StraightWire.gha
```

If Rhino is not in the default location, pass `-p:RhinoSystemDir=<Rhino's System folder>`.

## Developing against a running Rhino

With the Rhino MCP connected, send `tools/dev-reload.csx` to `execute_rhinocommon_csharp_code`
after each build: it unloads the previous build and loads the new one, no restart.
`tools/dev-call.csx` calls `DevHost.Describe` / `DevHost.Run`, and `tools/verify/*.csx`
render the fixture in `tests/fixtures/` and check settings, fallbacks and wire looks.
If an installed `StraightWire.gha` is loaded, the reload detaches it and the dev build takes
over until Rhino restarts.

## Packaging

`./package.sh` writes `dist/straightwire-<version>-rh8_0-any.yak` and
`dist/StraightWire-<version>.zip`. Publishing to the Package Manager (`yak login`, then
`yak push`) is public and one-way, so it is deliberately not part of the script.

## Wire Edit Mode verification

`tools/verify/wire-edit-mode.csx` checks the Release build in Rhino on isolated canvases,
without replacing the active plugin or document. It covers event bypass, session state,
menu toggling, cancellation, multiple canvases and saved-data preservation. Real mouse
Relay insertion still requires manual testing. See `docs/wire-edit-mode-verification.md`.

## Visible-wire Relay insertion

The Relay bridge uses completed paint paths, a six-screen-pixel hit tolerance, and public
Grasshopper widget/document APIs. It replaces only the chosen source/recipient connection,
keeps source order, and records graph and wire-setting changes in one Undo step. Existing
hand edits are split between the new connections; an edited lane becomes two manual route
pieces. Relay grips can change the immediate geometry around the insertion point.

`tools/verify/relay-insertion.csx` tests isolated documents without replacing the installed
plugin. See `docs/relay-insertion-verification.md` for results, remaining manual checks and
safe installation instructions.
