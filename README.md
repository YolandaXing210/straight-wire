# Straight Wire

Grasshopper wires as straight, circuit-board style traces — right-angle or 45°, each in its
own lane, around components, with wires from the same output sharing a trunk. Rhino 8,
Windows and macOS.

The interaction was designed in a browser prototype first:
[`docs/reference/straight-wire-prototype.html`](docs/reference/straight-wire-prototype.html).
This first release covers drawing; editing wires by hand and tidying the canvas come next.

## How it works

Grasshopper draws every wire between two canvas paint events, with pens built from four skin
colours. Straight Wire makes those colours transparent for that moment, routes the wires
itself (`src/StraightWire.Core`, no Grasshopper dependency, unit-tested), and draws the routes
with Grasshopper's own pens, so list, tree, empty, faint and selected wires look the way they
always do. Routes are cached against a fingerprint of the layout, so panning, zooming and
solving cost nothing; dragging a component re-routes every frame.

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
Dev builds refuse to load while an installed `StraightWire.gha` is present.

## Packaging

`./package.sh` writes `dist/straightwire-<version>-rh8_0-any.yak` and
`dist/StraightWire-<version>.zip`. Publishing to the Package Manager (`yak login`, then
`yak push`) is public and one-way, so it is deliberately not part of the script.
