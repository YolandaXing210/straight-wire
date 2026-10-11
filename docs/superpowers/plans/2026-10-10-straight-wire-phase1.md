# Straight Wire Phase 1 (Rendering) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A Grasshopper plugin for Rhino 8 that hides Grasshopper's bezier wires and draws them as routed right-angle or 45° traces. It covers global and per-selection styles, a wire spacing setting, settings stored in the .gh file, and a marker component so files carry the plugin requirement.

**Architecture:** `StraightWire.Core` is a pure C# routing library with no Grasshopper references, ported from the validated prototype and unit-tested with xUnit. The plugin (`StraightWire.gha`) compiles those Core sources in. It reads the canvas into a Core `Scene` and caches routes behind a layout fingerprint. It hides native wires by making the four wire skin colours transparent between `CanvasPrePaintWires` and `CanvasPostPaintWires`. Our polylines are drawn with Grasshopper's own wire pens, so every wire type and selection state looks native.

**Tech Stack:** C# (LangVersion latest), .NET 7 (`net7.0`) for Core and plugin, .NET 10 for tests, Grasshopper 1 SDK (NuGet `Grasshopper` 8.0.23304.9001), System.Drawing and WinForms as shipped with Rhino, xUnit 2.9.2, System.Text.Json, Rhino's `yak` CLI.

**Spec:** `docs/superpowers/specs/2026-10-10-straight-wire-phase1-design.md`. The routing reference is `docs/reference/straight-wire-prototype.html`, the JavaScript functions named in spec section 6.

## Global Constraints

- **Platform:**
  - Rhino 8 only, Mac and Windows.
  - The plugin targets `net7.0`. It references Grasshopper via NuGet `Grasshopper` `8.0.23304.9001` with `ExcludeAssets="runtime"`.
  - It references Rhino's own `System.Windows.Forms.dll`, `System.Windows.Forms.Primitives.dll` and `System.Drawing.Common.dll` with `Private=false`, using the same csproj pattern as `~/Documents/GitHub/wire-drop/src/WireDrop/WireDrop.csproj`.
- **Core:**
  - `src/StraightWire.Core` targets `net7.0` and references nothing but the BCL. No Grasshopper, Rhino, System.Drawing or WinForms.
  - Tests target `net10.0`, the only runtime installed. They use `Microsoft.NET.Test.Sdk` 17.11.1, `xunit` 2.9.2 and `xunit.runner.visualstudio` 2.8.2, all already in the local NuGet cache.
- **One shipped file:** the plugin compiles `src/StraightWire.Core/**/*.cs` into `StraightWire.gha`. No `StraightWire.Core.dll` ships.
- **Routing constants:** these are fixed; they are the spec's values.
  - `Stub` 18, `Chamfer` 14, `DiagonalStub` 6, `Clearance` 8.
  - Spacing defaults to 10; the options are 6/8/10/12/16/20, clamped to [6, 20].
  - A wire is backwards if `dx < 8` and straight if `|dy| < 0.5`.
  - Wires go around at 24 from their nodes. The node touch pad is 3 and the touch threshold is `0.6·spacing`.
  - Relax runs 32 iterations.
  - Junctions: split distance 0.6, dots dropped closer than 3 to the port, dots merged closer than 1.5, dot radius 2.6, dots hidden below zoom 0.5.
- **No animation** anywhere.
- **Wire id:** `source.InstanceGuid + ">" + target.InstanceGuid`, using Guid `ToString()` ("D" format). The group key is `source.InstanceGuid.ToString()`. A node id is the top-level object's `InstanceGuid.ToString()`.
- **File settings:**
  - `GH_Document.ValueTable` key `"StraightWire.v1"`.
  - JSON `{"v":1,"mode":<token>,"spacing":<number>,"wires":{<wireId>:<token>}}`, where a token is `"bezier" | "ortho" | "diag"`.
  - Overrides are written sorted by key.
- **Personal defaults:** `new GH_SettingsServer("StraightWire", true)`, keys `DefaultMode` (token, default `"diag"`), `DefaultSpacing` (default 10) and `WidgetVisible` (default true).
- **UI strings:**
  - The UI is in English.
  - Style labels are `Curve`, `Orthogonal` and `45°`; the widget uses `Curve` / `Ortho` / `45°`.
  - Marker message: `<Label> · <spacing>`, e.g. `45° · 10`.
- **Native wires always come back:**
  - The four skin colours are saved, set transparent, and restored in the same paint.
  - They are never written to Grasshopper's skin file.
  - Any exception in our paint hooks is caught, reported once via `Log`, and that canvas falls back to native wires.
- **Fixed GUIDs:** assembly `6D1CB607-5D76-4B1D-AC6D-E9FA882192FE`; marker component `8E05DF3D-B91F-431C-ABDB-C638AE1934E4`.
- **Package metadata:**
  - Version `0.1.0`, author `Yolanda Xing`, URL `https://github.com/YolandaXing210/straight-wire`.
  - Yak package name `StraightWire`. Never run `yak push`; the user publishes.
- **Commit messages:** imperative subject line. End every commit message with exactly these two lines:
  ```
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01GKJGcqmxvBcFYsUcoRsYRS
  ```
- **Rhino MCP verification:**
  - Plugin tasks are verified inside the user's running Rhino through the Rhino MCP tool `mcp__rhinomcp__execute_rhinocommon_csharp_code`: read the named `.csx` file and send its full text as `code`.
  - Never install `StraightWire.gha` into Grasshopper's Libraries folder before Task 18, because dev builds refuse to load next to an installed copy.
  - Scripts that swap a document into the canvas always restore the user's document in a `finally`.
- **Paths:**
  - Repo: `/Users/yolandaxing/Documents/GitHub/straight-wire`.
  - Rhino: `/Applications/Rhino 8.app`.
  - Grasshopper assemblies: `/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/{Grasshopper,GH_IO}.dll`.
  - Grasshopper Libraries folder on macOS: `~/Library/Application Support/McNeel/Rhinoceros/8.0/Plug-ins/Grasshopper (b45a29b1-4343-4035-989e-044e8580d9cf)/Libraries/`.

### Facts verified in the user's Rhino while planning (do not re-derive)

- **Wire pens:** `GH_Painter.GenerateWirePen(PointF a, PointF b, bool asel, bool bsel, GH_WireType type)` is private and returns a `Pen`.
  - item: width 3.
  - list: width 5, compound `{0, .4, .6, 1}`.
  - tree: width 5, compound `{0, .35, .65, 1}`, dash `{3, .5}`.
  - faint: width 1.5, `FromArgb(50, wire_default)`.
  - wireless: width 4, dotted.
  - null (empty data): `wire_empty` orange.
  - When one end is selected, the pen is a gradient from `wire_selected_a` at the selected end to `wire_selected_b` (or to `wire_empty` for empty data) at the other.
- **Faint pen cache:** the faint pen is cached per `GH_Painter` in a VB static local the first time it is built (fields whose names contain `GenerateWirePen_Static_Faint`). It is built from whatever `GH_Skin.wire_default` is at that moment.
- **Native wire rules** (`GH_Attributes.RenderIncomingWires`):
  - `type = CentralSettings.CanvasFancyWires ? GH_Painter.DetermineWireType(source.VolatileData) : GH_WireType.generic`.
  - faint display with neither end selected → `GH_WireType.faint`.
  - hidden display with neither end selected → no wire, just a small arc symbol at the input. That symbol does not use the wire skin colours, so it survives our transparent window.
  - faint or hidden with an end selected → a normal wire with the selection gradient.
- **Public members the plan relies on:**
  - `GH_Painter.DrawConnection(PointF, PointF, GH_WireDirection, GH_WireDirection, bool, bool, GH_WireType)` and `GH_Painter.DetermineWireType(IGH_Structure)` (static).
  - `GH_Canvas` events `CanvasPrePaintWires`, `CanvasPostPaintWires` and `CanvasPaintEnd`, each `(GH_Canvas sender)`.
  - `GH_Canvas.WidgetListCreated (object, GH_CanvasWidgetListEventArgs)` with `e.AddWidget(IGH_Widget)`, and `GH_Canvas.Widgets` (`List<IGH_Widget>`).
  - `GH_Viewport.VisibleRegion`, `Zoom`, `MidPoint` (the canvas point at the centre of the viewport) and the copy constructor `new GH_Viewport(GH_Viewport)`.
  - `GH_CanvasMouseEvent.ControlLocation` and `.Button`.
  - `GH_Document.Modified()`, which sets `IsModified` true. `ValueTable.SetValue` alone does not.
  - `GH_Document.SelectedObjects()` and `GH_Document.BoundingBox(bool)`.
  - `GH_ComponentServer.EmitObjectProxy(Guid)`, which returns null when the GUID is unregistered.
  - `GH_SettingsServer(string, bool)` with `GetValue` / `SetValue` overloads for string, bool and double, plus `WritePersistentSettings()`.
  - `Instances.DocumentEditor.MainMenuStrip` accepts new top-level items on Mac.
- **Component GUIDs for the test fixture:**
  - Number Slider `57da07bd-ecab-415d-9d86-af36d7073abc`
  - Series `e64c5fb1-845c-4ab1-8911-5f338516ba67`
  - Unit Z `9103c240-a6a9-4223-9b42-dbd19bf38e2b`
  - Polygon `845527a6-5cea-4ae9-a667-96ae1667a4e8` (inputs P, R, S, Rf)
  - Multiplication `ce46b74e-00c9-43c4-805a-193b69ea4a11`
  - Move `e9eb1dcf-92f6-4d4d-84ae-96222d60f56b`
  - Rotate (Transform) `b7798b74-037e-4f0c-8ac7-dc1043d093e0` (inputs G, A, P)
  - Loft `a7a41d0a-2188-4f7a-82cc-1a2c4e4ec850`
  - Divide Curve `2162e72e-72fc-4bf8-9459-d4d82fa8aa14`
  - Flip Matrix `41aa4112-9c9b-42f4-847e-503b9d90e4c7`
  - Interpolate `2b2a4145-3dff-41d4-a8de-1ea9d29eef33`
  - Area `86b28a7e-94d9-4791-8306-e13e10d5f8d5`
  - Panel `59e0b89a-e487-49f8-bab8-b5bab16be14c`

### Where this plan deliberately differs from the spec (the spec is updated in Task 1)

1. **Core target:** Core targets `net7.0`, not netstandard2.0. That gives `HashCode`, value-tuple patterns and System.Text.Json without polyfills.
2. **Packaging:** Core is compiled into the `.gha` (one file to install and hot-load) rather than shipped as `StraightWire.Core.dll`.
3. **Drawing:**
   - Routed wires use the private `GenerateWirePen` through a cached delegate. The fallback is to not take over at all.
   - Unrouted wires (Curve style, hidden-with-selection) are replayed with the public `DrawConnection`.
   - Hidden wires with nothing selected are left entirely to Grasshopper.
4. **Faint pen warm-up:** before every takeover, the faint pen is requested once while the colours are still real. A shared faint pen is never disposed.
5. **Fancy wires:** Grasshopper's "fancy wires" setting is respected. When it is off, every wire is drawn as `generic`.
6. **Exact last point:** `Dedupe` keeps a path's exact last point, so paths always end exactly on their port.
7. **Libraries path:** the Libraries folder on macOS includes the `Grasshopper (b45a29b1-…)` folder name.
8. **Yak manifest:** the package name is `StraightWire`. The manifest is generated by `package.sh` with no icon, following WireDrop.
9. **Marker message:** the marker's message is refreshed when a document's settings are first loaded (at paint time), not in `AddedToDocument`. During file reading, the ValueTable may not be loaded yet.

## Review Focus

1. **Stacked or overlapping components, and ports inside other components.** People drop components on top of each other all the time. Routing must not throw or produce NaN, and every path must still start and end exactly on its ports. Tested in Task 7 (`Survives_overlapping_nodes_and_ports_inside_nodes`).
2. **Self-loops and zero-length wires.** A wire from a component's output back into its own input, or two grips on the same point, must route (around the node) without crashing. Tested in Task 7 (`Routes_a_self_loop_around_its_node`, `Survives_a_zero_length_wire`).
3. **Settings written by someone else or by a newer plugin.** Broken JSON, a newer `v`, unknown style tokens and overrides for deleted wires must open the file normally with defaults. A newer version must never be overwritten. Tested in Task 9 (parser tests) and Task 12 (`tools/verify/settings-status.csx` in Rhino).
4. **Wires we don't route must still look native:** a Curve override inside a straight file, hidden-display wires, faint wires and empty-data wires. Tested in Task 13 (`tools/verify/wire-looks.csx`, checked row by row).
5. **The faint pen cache being built while the skin colours are transparent.** Faint wires would stay invisible for the rest of the session, even with the plugin off. Tested in Task 13 (`wire-looks.csx` resets the cache before painting and checks the pen colour afterwards).

---

## File map

```
.gitignore                                   (modify: dist/, artifacts/)
src/StraightWire.Core/StraightWire.Core.csproj
src/StraightWire.Core/Geometry.cs            Pt, Rect, Geo, Intervals
src/StraightWire.Core/Routing.cs             constants
src/StraightWire.Core/Paths.cs               Dedupe, Simplify, Chamfer, Diagonal, Lane, Back
src/StraightWire.Core/Scene.cs               WireStyle, SceneNode, SceneWire, Scene (+Fingerprint)
src/StraightWire.Core/RouteResult.cs         Junction, RouteResult, RouterState
src/StraightWire.Core/SpatialGrid.cs         uniform grid of int ids
src/StraightWire.Core/RouteContext.cs        WireKind, WireJob, RouteContext
src/StraightWire.Core/Lanes.cs               Prepare, Feasible, CrossCount, Relax
src/StraightWire.Core/BackWires.cs           Prepare, Channel, Diagonal, Orthogonal
src/StraightWire.Core/WirePath.cs            final polyline for a job
src/StraightWire.Core/Diagonals.cs           Decide, Touches
src/StraightWire.Core/Junctions.cs           Diverge, Find
src/StraightWire.Core/Router.cs              Route
src/StraightWire.Core/FileSettings.cs        FileSettings, SettingsStatus, SettingsJson, StyleNames
tests/StraightWire.Core.Tests/StraightWire.Core.Tests.csproj
tests/StraightWire.Core.Tests/TestScenes.cs  SceneBuilder, Pipeline, random scenes
tests/StraightWire.Core.Tests/*Tests.cs
src/StraightWire/StraightWire.csproj
src/StraightWire/StraightWireInfo.cs
src/StraightWire/StraightWirePriority.cs
src/StraightWire/Plugin.cs
src/StraightWire/DevHost.cs
src/StraightWire/Log.cs
src/StraightWire/Interop/NativeWires.cs
src/StraightWire/SceneReader.cs
src/StraightWire/UserSettings.cs
src/StraightWire/DocStore.cs
src/StraightWire/CanvasHook.cs
src/StraightWire/WireRenderer.cs
src/StraightWire/Commands.cs
src/StraightWire/UI/StyleMenu.cs
src/StraightWire/UI/StyleWidget.cs
src/StraightWire/Marker.cs
tools/dev-reload.csx, tools/dev-call.csx, tools/make-fixture.csx
tools/verify/*.csx
tests/fixtures/twisted-tower.gh              (generated by tools/make-fixture.csx)
package.sh, README.md, INSTALL.txt, LICENSE
```

Commands used throughout:

- Core tests: `dotnet test tests/StraightWire.Core.Tests --nologo -v q`. Add `--filter "FullyQualifiedName~<Class>"` to run one class.
- Plugin build: `dotnet build src/StraightWire --nologo -v q`. The output is `src/StraightWire/bin/Debug/StraightWire.gha`.

---

### Task 1: Repository scaffold and geometry

**Files:**
- Modify: `.gitignore`
- Modify: `docs/superpowers/specs/2026-10-10-straight-wire-phase1-design.md` (record the deviations listed above)
- Create: `src/StraightWire.Core/StraightWire.Core.csproj`
- Create: `src/StraightWire.Core/Geometry.cs`
- Create: `src/StraightWire.Core/Routing.cs`
- Create: `tests/StraightWire.Core.Tests/StraightWire.Core.Tests.csproj`
- Test: `tests/StraightWire.Core.Tests/GeometryTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `public readonly struct Pt` with `X` and `Y`, `Pt(double, double)`, `static double Dist(Pt, Pt)` and value equality.
  - `public readonly struct Rect` with `X0, Y0, X1, Y1`, `static Rect FromXYWH(x, y, w, h)`, `Rect Inflate(double)` and `bool Intersects(Rect)` (closed).
  - `public static class Geo`: `Clamp`, `SegDist`, `PolyDist`, `SegSegDist`, `PolyPolyWithin`, `SegRectHit`, `BBox`.
  - `internal static class Intervals`: `Cut(List<(double Lo, double Hi)>, double p, double q)`.
  - `public static class Routing` with the constants.

- [ ] **Step 1: Update `.gitignore`**

Append these lines to `.gitignore`:

```
dist/
artifacts/
```

- [ ] **Step 2: Record the plan's deviations in the spec**

In `docs/superpowers/specs/2026-10-10-straight-wire-phase1-design.md`:
- In section 5, change `netstandard2.0` to `net7.0` in the tree. Add one sentence after the dependency paragraph: "The Core sources are compiled into the .gha, so the plugin installs as a single file; the Core project exists for the tests."
- In 7.2, replace the "Colour and gradient" and "Wire type" bullets with: "Routed wires are drawn with Grasshopper's own wire pen (`GH_Painter.GenerateWirePen`, reached by reflection), so item / list / tree / faint / empty / selection all match native. Unrouted wires are drawn with the public `GH_Painter.DrawConnection`. When Grasshopper's fancy-wires setting is off, every wire uses the generic type, as native does. Before every takeover the faint pen is requested once while the colours are still real, because Grasshopper caches that pen the first time it is built."
- In 7.2, replace the "Hidden display" bullet with: "Hidden display: not routed. With neither end selected Grasshopper draws only a small arc symbol at the input, which does not use the wire colours, so it is left alone; with an end selected the wire is drawn with `DrawConnection`."
- In 9.5, change the first bullet to "`dotnet build -c Release` produces `StraightWire.gha`." Change the second to "`package.sh` writes the Yak manifest (name `StraightWire`) and builds the package, following WireDrop."
- In 13, change the install path to `~/Library/Application Support/McNeel/Rhinoceros/8.0/Plug-ins/Grasshopper (b45a29b1-4343-4035-989e-044e8580d9cf)/Libraries/`.

- [ ] **Step 3: Create the Core project**

`src/StraightWire.Core/StraightWire.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net7.0</TargetFramework>
    <RootNamespace>StraightWire.Core</RootNamespace>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <!-- net7.0 is out of support, but it is what Rhino 8 plugins target. -->
    <NoWarn>NETSDK1138</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <!-- Declared here rather than in source, so it is not compiled into the .gha. -->
    <InternalsVisibleTo Include="StraightWire.Core.Tests" />
  </ItemGroup>

</Project>
```

- [ ] **Step 4: Create the test project**

`tests/StraightWire.Core.Tests/StraightWire.Core.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <IsPackable>false</IsPackable>
    <NoWarn>NETSDK1138</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/StraightWire.Core/StraightWire.Core.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5: Write the failing tests**

`tests/StraightWire.Core.Tests/GeometryTests.cs`:

```csharp
using System.Collections.Generic;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class GeometryTests
    {
        [Fact]
        public void SegDist_is_the_perpendicular_distance_inside_the_segment() =>
            Assert.Equal(5, Geo.SegDist(new Pt(5, 5), new Pt(0, 0), new Pt(10, 0)), 9);

        [Fact]
        public void SegDist_clamps_to_the_nearest_end() =>
            Assert.Equal(5, Geo.SegDist(new Pt(13, 4), new Pt(0, 0), new Pt(10, 0)), 9);

        [Fact]
        public void SegDist_of_a_degenerate_segment_is_point_distance() =>
            Assert.Equal(5, Geo.SegDist(new Pt(3, 4), new Pt(0, 0), new Pt(0, 0)), 9);

        [Fact]
        public void PolyDist_takes_the_nearest_segment() =>
            Assert.Equal(2, Geo.PolyDist(new Pt(12, 5), new[] { new Pt(0, 0), new Pt(10, 0), new Pt(10, 10) }), 9);

        [Fact]
        public void SegSegDist_is_zero_when_segments_cross() =>
            Assert.Equal(0, Geo.SegSegDist(new Pt(0, 0), new Pt(10, 10), new Pt(0, 10), new Pt(10, 0)));

        [Fact]
        public void SegSegDist_of_parallel_segments() =>
            Assert.Equal(3, Geo.SegSegDist(new Pt(0, 0), new Pt(10, 0), new Pt(0, 3), new Pt(10, 3)), 9);

        [Fact]
        public void PolyPolyWithin_is_true_when_some_segments_come_closer_than_d()
        {
            var a = new[] { new Pt(0, 0), new Pt(10, 0) };
            var b = new[] { new Pt(0, 7), new Pt(5, 4), new Pt(10, 7) };
            Assert.True(Geo.PolyPolyWithin(a, b, 4.5));
            Assert.False(Geo.PolyPolyWithin(a, b, 4));
        }

        [Fact]
        public void PolyPolyWithin_is_false_for_far_apart_paths() =>
            Assert.False(Geo.PolyPolyWithin(
                new[] { new Pt(0, 0), new Pt(10, 0) },
                new[] { new Pt(100, 100), new Pt(110, 100) }, 6));

        [Theory]
        [InlineData(-5, 5, 15, 5, true)]    // passes straight through
        [InlineData(-5, -5, -1, -1, false)] // stops short
        [InlineData(2, 2, 3, 3, true)]      // entirely inside
        [InlineData(-5, 0, 15, 0, true)]    // runs along the top edge
        [InlineData(-5, 11, 15, 11, false)] // runs just below
        public void SegRectHit(double ax, double ay, double bx, double by, bool hit) =>
            Assert.Equal(hit, Geo.SegRectHit(new Pt(ax, ay), new Pt(bx, by), new Rect(0, 0, 10, 10)));

        [Fact]
        public void BBox_pads_every_side()
        {
            var r = Geo.BBox(new[] { new Pt(1, 2), new Pt(5, -3) }, 1);
            Assert.Equal((0.0, -4.0, 6.0, 3.0), (r.X0, r.Y0, r.X1, r.Y1));
        }

        [Fact]
        public void Clamp_matches_the_prototype_when_the_range_is_empty()
        {
            Assert.Equal(5, Geo.Clamp(4, 5, 3));
            Assert.Equal(3, Geo.Clamp(7, 5, 3));
        }

        [Fact]
        public void Rect_intersects_when_edges_touch() =>
            Assert.True(new Rect(0, 0, 10, 10).Intersects(new Rect(10, 0, 20, 10)));

        [Fact]
        public void Cut_splits_an_interval_around_the_hole() =>
            Assert.Equal(new[] { (0.0, 3.0), (5.0, 10.0) },
                Intervals.Cut(new List<(double Lo, double Hi)> { (0, 10) }, 3, 5));

        [Fact]
        public void Cut_keeps_intervals_the_hole_misses_and_drops_covered_ones() =>
            Assert.Equal(new[] { (0.0, 2.0), (8.0, 9.0) },
                Intervals.Cut(new List<(double Lo, double Hi)> { (0, 2), (3, 4), (6, 9) }, 2, 8));
    }
}
```

- [ ] **Step 6: Run the tests to verify they fail**

Run: `dotnet test tests/StraightWire.Core.Tests --nologo -v q`
Expected: the build fails with errors like `CS0246: The type or namespace name 'Pt' could not be found`.

- [ ] **Step 7: Write the routing constants**

`src/StraightWire.Core/Routing.cs`:

```csharp
namespace StraightWire.Core
{
    /// <summary>
    /// Routing constants in canvas units (pixels at zoom 1). The names in brackets are the
    /// prototype's (docs/reference/straight-wire-prototype.html), which these are ported from.
    /// </summary>
    public static class Routing
    {
        /// <summary>Shortest horizontal run out of a port [STUB].</summary>
        public const double Stub = 18;

        /// <summary>Size of a 45° chamfer [CH].</summary>
        public const double Chamfer = 14;

        /// <summary>Shortest horizontal run at each end of a single diagonal [DSTUB].</summary>
        public const double DiagonalStub = 6;

        /// <summary>Clearance kept around nodes [CLR].</summary>
        public const double Clearance = 8;

        /// <summary>A wire whose target is less than this far right of its source runs backwards.</summary>
        public const double BackwardBelow = 8;

        /// <summary>Ends closer than this vertically make a wire one straight segment.</summary>
        public const double StraightBelow = 0.5;

        /// <summary>How far above or below its nodes a backwards wire goes around when they leave no gap.</summary>
        public const double AroundGap = 24;

        /// <summary>Padding around nodes when counting what a 45° candidate touches.</summary>
        public const double TouchPad = 3;

        /// <summary>Wires closer than this fraction of the spacing count as touching.</summary>
        public const double TouchFraction = 0.6;

        /// <summary>Push-and-shove passes when spreading lanes.</summary>
        public const int RelaxIterations = 32;

        /// <summary>Cell size of the spatial grids.</summary>
        public const double GridCell = 128;

        public const double DefaultSpacing = 10;
        public const double MinSpacing = 6;
        public const double MaxSpacing = 20;
        public static readonly double[] SpacingOptions = { 6, 8, 10, 12, 16, 20 };
    }
}
```

- [ ] **Step 8: Write the geometry**

`src/StraightWire.Core/Geometry.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;

namespace StraightWire.Core
{
    /// <summary>A point in Grasshopper canvas units: pixels at zoom 1, y pointing down.</summary>
    public readonly struct Pt : IEquatable<Pt>
    {
        public Pt(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }

        public static double Dist(Pt a, Pt b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public bool Equals(Pt other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Pt other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###})", X, Y);
    }

    /// <summary>An axis-aligned rectangle given by its four edges.</summary>
    public readonly struct Rect
    {
        public Rect(double x0, double y0, double x1, double y1)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
        }

        public static Rect FromXYWH(double x, double y, double w, double h) => new Rect(x, y, x + w, y + h);

        public double X0 { get; }
        public double Y0 { get; }
        public double X1 { get; }
        public double Y1 { get; }

        public Rect Inflate(double d) => new Rect(X0 - d, Y0 - d, X1 + d, Y1 + d);

        /// <summary>Closed test: rectangles that only touch along an edge intersect.</summary>
        public bool Intersects(Rect o) => X0 <= o.X1 && o.X0 <= X1 && Y0 <= o.Y1 && o.Y0 <= Y1;

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "[{0:0.###}, {1:0.###} .. {2:0.###}, {3:0.###}]", X0, Y0, X1, Y1);
    }

    /// <summary>Distance and hit tests, ported from the prototype's geometry helpers.</summary>
    public static class Geo
    {
        /// <summary>The prototype's clamp: never throws, and an empty range (lo &gt; hi) clamps as written.</summary>
        public static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;

        public static double SegDist(Pt p, Pt a, Pt b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
            double t = l2 > 0 ? Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1) : 0;
            double ex = p.X - a.X - dx * t, ey = p.Y - a.Y - dy * t;
            return Math.Sqrt(ex * ex + ey * ey);
        }

        public static double PolyDist(Pt p, IReadOnlyList<Pt> poly)
        {
            double m = double.PositiveInfinity;
            for (int i = 0; i < poly.Count - 1; i++) m = Math.Min(m, SegDist(p, poly[i], poly[i + 1]));
            return m;
        }

        static double Orient(Pt a, Pt b, Pt c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

        public static double SegSegDist(Pt p1, Pt p2, Pt q1, Pt q2)
        {
            double d1 = Orient(q1, q2, p1), d2 = Orient(q1, q2, p2), d3 = Orient(p1, p2, q1), d4 = Orient(p1, p2, q2);
            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0))) return 0;
            return Math.Min(Math.Min(SegDist(p1, q1, q2), SegDist(p2, q1, q2)), Math.Min(SegDist(q1, p1, p2), SegDist(q2, p1, p2)));
        }

        /// <summary>
        /// True when some segment of <paramref name="a"/> comes closer than <paramref name="d"/> to
        /// some segment of <paramref name="b"/>. Segment pairs whose boxes are more than d apart are
        /// skipped before the exact test; that is what keeps 45° decisions fast on big canvases.
        /// </summary>
        public static bool PolyPolyWithin(IReadOnlyList<Pt> a, IReadOnlyList<Pt> b, double d)
        {
            for (int i = 0; i < a.Count - 1; i++)
            {
                Pt p1 = a[i], p2 = a[i + 1];
                double ax0 = Math.Min(p1.X, p2.X) - d, ax1 = Math.Max(p1.X, p2.X) + d;
                double ay0 = Math.Min(p1.Y, p2.Y) - d, ay1 = Math.Max(p1.Y, p2.Y) + d;
                for (int j = 0; j < b.Count - 1; j++)
                {
                    Pt q1 = b[j], q2 = b[j + 1];
                    if (Math.Max(q1.X, q2.X) < ax0 || Math.Min(q1.X, q2.X) > ax1 ||
                        Math.Max(q1.Y, q2.Y) < ay0 || Math.Min(q1.Y, q2.Y) > ay1) continue;
                    if (SegSegDist(p1, p2, q1, q2) < d) return true;
                }
            }
            return false;
        }

        /// <summary>Liang–Barsky: does segment a–b touch rectangle r (edges included)?</summary>
        public static bool SegRectHit(Pt a, Pt b, Rect r)
        {
            double t0 = 0, t1 = 1, dx = b.X - a.X, dy = b.Y - a.Y;
            double[] p = { -dx, dx, -dy, dy };
            double[] q = { a.X - r.X0, r.X1 - a.X, a.Y - r.Y0, r.Y1 - a.Y };
            for (int i = 0; i < 4; i++)
            {
                if (p[i] == 0)
                {
                    if (q[i] < 0) return false;
                    continue;
                }
                double t = q[i] / p[i];
                if (p[i] < 0)
                {
                    if (t > t1) return false;
                    if (t > t0) t0 = t;
                }
                else
                {
                    if (t < t0) return false;
                    if (t < t1) t1 = t;
                }
            }
            return true;
        }

        public static Rect BBox(IReadOnlyList<Pt> poly, double pad)
        {
            double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity;
            double x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;
            foreach (var p in poly)
            {
                if (p.X < x0) x0 = p.X;
                if (p.X > x1) x1 = p.X;
                if (p.Y < y0) y0 = p.Y;
                if (p.Y > y1) y1 = p.Y;
            }
            return new Rect(x0 - pad, y0 - pad, x1 + pad, y1 + pad);
        }
    }

    /// <summary>Sorted, disjoint intervals on one axis.</summary>
    internal static class Intervals
    {
        /// <summary>Removes (p, q) from every interval in the list. The result stays sorted.</summary>
        public static List<(double Lo, double Hi)> Cut(List<(double Lo, double Hi)> spans, double p, double q)
        {
            var next = new List<(double Lo, double Hi)>(spans.Count + 1);
            foreach (var (u, v) in spans)
            {
                if (q <= u || p >= v)
                {
                    next.Add((u, v));
                    continue;
                }
                if (p > u) next.Add((u, p));
                if (q < v) next.Add((q, v));
            }
            return next;
        }
    }
}
```

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test tests/StraightWire.Core.Tests --nologo -v q`
Expected: `Passed!  - Failed: 0, Passed: 18`.

- [ ] **Step 10: Commit**

```bash
git add .gitignore docs/superpowers/specs/2026-10-10-straight-wire-phase1-design.md src/StraightWire.Core tests/StraightWire.Core.Tests
git commit -F - <<'EOF'
Add Core project with routing constants and geometry

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GKJGcqmxvBcFYsUcoRsYRS
EOF
```

---

### Task 2: Path builders

**Files:**
- Create: `src/StraightWire.Core/Paths.cs`
- Test: `tests/StraightWire.Core.Tests/PathsTests.cs`

**Interfaces:**
- Consumes: `Pt`, `Routing` (Task 1).
- Produces: `internal static class Paths`, with every method returning a new `List<Pt>`:
  - `Dedupe(IReadOnlyList<Pt>)`
  - `Simplify(IReadOnlyList<Pt>)`
  - `Chamfer(IReadOnlyList<Pt>, double c)`
  - `Diagonal(double ax, double ay, double bx, double by, double x, double c)` (already deduped)
  - `Lane(Pt a, Pt b, double x)`
  - `Back(Pt a, Pt b, double y)`

- [ ] **Step 1: Write the failing tests**

`tests/StraightWire.Core.Tests/PathsTests.cs`:

```csharp
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class PathsTests
    {
        static Pt P(double x, double y) => new Pt(x, y);

        [Fact]
        public void Dedupe_drops_points_within_a_hundredth() =>
            Assert.Equal(new[] { P(0, 0), P(10, 0) }, Paths.Dedupe(new[] { P(0, 0), P(0.005, 0), P(10, 0) }));

        [Fact]
        public void Dedupe_keeps_the_exact_last_point() =>
            Assert.Equal(new[] { P(0, 0), P(10.005, 0) }, Paths.Dedupe(new[] { P(0, 0), P(10, 0), P(10.005, 0) }));

        [Fact]
        public void Dedupe_of_one_repeated_point_gives_two_points() =>
            Assert.Equal(new[] { P(1, 1), P(1, 1) }, Paths.Dedupe(new[] { P(1, 1), P(1, 1) }));

        [Fact]
        public void Simplify_merges_collinear_runs() =>
            Assert.Equal(new[] { P(0, 0), P(10, 0), P(10, 5) }, Paths.Simplify(new[] { P(0, 0), P(5, 0), P(10, 0), P(10, 5) }));

        [Fact]
        public void Simplify_keeps_a_reversal() =>
            Assert.Equal(new[] { P(0, 0), P(10, 0), P(5, 0) }, Paths.Simplify(new[] { P(0, 0), P(10, 0), P(5, 0) }));

        [Fact]
        public void Chamfer_cuts_a_right_angle() =>
            Assert.Equal(new[] { P(0, 0), P(36, 0), P(50, 14), P(50, 50) },
                Paths.Chamfer(new[] { P(0, 0), P(50, 0), P(50, 50) }, 14));

        [Fact]
        public void Chamfer_leaves_a_45_degree_corner() =>
            Assert.Equal(new[] { P(0, 0), P(20, 0), P(40, 20) },
                Paths.Chamfer(new[] { P(0, 0), P(20, 0), P(40, 20) }, 14));

        [Fact]
        public void Chamfer_is_limited_by_short_legs() =>
            Assert.Equal(new[] { P(0, 0), P(8, 0), P(10, 2), P(12, 4), P(30, 4) },
                Paths.Dedupe(Paths.Chamfer(new[] { P(0, 0), P(10, 0), P(10, 4), P(30, 4) }, 14)));

        [Fact]
        public void Diagonal_with_a_half_height_cut_is_one_straight_diagonal() =>
            Assert.Equal(new[] { P(0, 0), P(30, 0), P(70, 40), P(100, 40) },
                Paths.Simplify(Paths.Diagonal(0, 0, 100, 40, 50, 20)));

        [Fact]
        public void Diagonal_with_a_small_cut_keeps_a_vertical_run() =>
            Assert.Equal(new[] { P(0, 0), P(36, 0), P(50, 14), P(50, 26), P(64, 40), P(100, 40) },
                Paths.Diagonal(0, 0, 100, 40, 50, 14));

        [Fact]
        public void Diagonal_cut_shrinks_near_a_port() =>
            Assert.Equal(new[] { P(0, 0), P(2, 0), P(10, 8), P(10, 32), P(18, 40), P(100, 40) },
                Paths.Diagonal(0, 0, 100, 40, 10, 20));

        [Fact]
        public void Diagonal_going_up() =>
            Assert.Equal(new[] { P(0, 40), P(30, 40), P(50, 20), P(70, 0), P(100, 0) },
                Paths.Diagonal(0, 40, 100, 0, 50, 20));

        [Fact]
        public void Lane_runs_out_down_and_in() =>
            Assert.Equal(new[] { P(0, 0), P(50, 0), P(50, 30), P(100, 30) }, Paths.Lane(P(0, 0), P(100, 30), 50));

        [Fact]
        public void Back_steps_out_crosses_at_y_and_steps_in() =>
            Assert.Equal(new[] { P(380, 230), P(398, 230), P(398, 130), P(82, 130), P(82, 30), P(100, 30) },
                Paths.Back(P(380, 230), P(100, 30), 130));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/StraightWire.Core.Tests --nologo -v q --filter "FullyQualifiedName~PathsTests"`
Expected: the build fails with `CS0103: The name 'Paths' does not exist in the current context`.

- [ ] **Step 3: Write the path builders**

`src/StraightWire.Core/Paths.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace StraightWire.Core
{
    /// <summary>Polyline builders and clean-up, ported from the prototype.</summary>
    internal static class Paths
    {
        const double Same = 0.01;

        /// <summary>
        /// Drops points within 0.01 of the previous kept point [dedupe]. Unlike the prototype, the
        /// exact last point is always kept, so a path ends on its port even when a point lands
        /// right next to it.
        /// </summary>
        public static List<Pt> Dedupe(IReadOnlyList<Pt> p)
        {
            var o = new List<Pt>(p.Count) { p[0] };
            for (int i = 1; i < p.Count; i++)
            {
                var q = o[o.Count - 1];
                if (Math.Abs(p[i].X - q.X) > Same || Math.Abs(p[i].Y - q.Y) > Same) o.Add(p[i]);
            }
            var last = p[p.Count - 1];
            if (o.Count == 1) o.Add(last);
            else o[o.Count - 1] = last;
            return o;
        }

        /// <summary>Merges runs of collinear, same-direction segments [simplify].</summary>
        public static List<Pt> Simplify(IReadOnlyList<Pt> p)
        {
            if (p.Count < 3) return new List<Pt>(p);
            var o = new List<Pt>(p.Count) { p[0] };
            for (int i = 1; i < p.Count - 1; i++)
            {
                Pt a = o[o.Count - 1], b = p[i], c = p[i + 1];
                double ux = b.X - a.X, uy = b.Y - a.Y, vx = c.X - b.X, vy = c.Y - b.Y;
                double lu = Math.Sqrt(ux * ux + uy * uy), lv = Math.Sqrt(vx * vx + vy * vy);
                if (lu > 0 && lv > 0 && Math.Abs(ux * vy - uy * vx) / (lu * lv) < 1e-4 && ux * vx + uy * vy > 0) continue;
                o.Add(b);
            }
            o.Add(p[p.Count - 1]);
            return o;
        }

        /// <summary>
        /// Cuts each right-angle corner back by up to <paramref name="c"/> along both legs [chamfer].
        /// Corners that are not about 90° (a 45° turn is already a clean trace corner) are left alone.
        /// </summary>
        public static List<Pt> Chamfer(IReadOnlyList<Pt> p, double c)
        {
            int n = p.Count;
            if (n < 3) return new List<Pt>(p);
            var o = new List<Pt>(n * 2) { p[0] };
            for (int i = 1; i < n - 1; i++)
            {
                Pt a = p[i - 1], v = p[i], b = p[i + 1];
                double la = Pt.Dist(a, v), lb = Pt.Dist(v, b);
                double ba = i - 1 == 0 ? la - 2 : la / 2, bb = i + 1 == n - 1 ? lb - 2 : lb / 2;
                double d = Math.Max(0, Math.Min(c, Math.Min(ba, bb)));
                bool square = la > 0 && lb > 0 &&
                              Math.Abs((v.X - a.X) * (b.X - v.X) + (v.Y - a.Y) * (b.Y - v.Y)) < 0.1 * la * lb;
                if (d < 0.01 || !square)
                {
                    o.Add(v);
                    continue;
                }
                o.Add(new Pt(v.X + (a.X - v.X) / la * d, v.Y + (a.Y - v.Y) / la * d));
                o.Add(new Pt(v.X + (b.X - v.X) / lb * d, v.Y + (b.Y - v.Y) / lb * d));
            }
            o.Add(p[n - 1]);
            return o;
        }

        /// <summary>
        /// A vertical run at x with 45° cuts of size c at both ends [diagPath]. c = |dy|/2 collapses it
        /// into one diagonal. The cut shrinks to keep 2 units of horizontal run at each port.
        /// </summary>
        public static List<Pt> Diagonal(double ax, double ay, double bx, double by, double x, double c)
        {
            double sg = by < ay ? -1 : 1;
            c = Math.Max(0, Math.Min(Math.Min(c, Math.Abs(by - ay) / 2), Math.Min(x - ax - 2, bx - 2 - x)));
            return Dedupe(new[]
            {
                new Pt(ax, ay), new Pt(x - c, ay), new Pt(x, ay + sg * c),
                new Pt(x, by - sg * c), new Pt(x + c, by), new Pt(bx, by),
            });
        }

        /// <summary>Out to the lane at x, along it, and in [orthoPath for a lane wire].</summary>
        public static List<Pt> Lane(Pt a, Pt b, double x) =>
            new List<Pt> { a, new Pt(x, a.Y), new Pt(x, b.Y), b };

        /// <summary>A backwards wire: a stub out, across at y, a stub in [orthoPath for a back wire].</summary>
        public static List<Pt> Back(Pt a, Pt b, double y)
        {
            double x1 = a.X + Routing.Stub, x2 = b.X - Routing.Stub;
            return new List<Pt> { a, new Pt(x1, a.Y), new Pt(x1, y), new Pt(x2, y), new Pt(x2, b.Y), b };
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/StraightWire.Core.Tests --nologo -v q`
Expected: `Passed!  - Failed: 0, Passed: 32`.

- [ ] **Step 5: Commit**

```bash
git add src/StraightWire.Core/Paths.cs tests/StraightWire.Core.Tests/PathsTests.cs
git commit -F - <<'EOF'
Port the prototype's path builders

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GKJGcqmxvBcFYsUcoRsYRS
EOF
```

---

### Task 3: Scene model, spatial grid and routing context

**Files:**
- Create: `src/StraightWire.Core/Scene.cs`
- Create: `src/StraightWire.Core/RouteResult.cs`
- Create: `src/StraightWire.Core/SpatialGrid.cs`
- Create: `src/StraightWire.Core/RouteContext.cs`
- Create: `tests/StraightWire.Core.Tests/TestScenes.cs`
- Test: `tests/StraightWire.Core.Tests/SceneTests.cs`, `tests/StraightWire.Core.Tests/SpatialGridTests.cs`, `tests/StraightWire.Core.Tests/RouteContextTests.cs`

**Interfaces:**
- Consumes: `Pt`, `Rect`, `Routing` (Task 1).
- Produces:
  - `public enum WireStyle { Curve, Orthogonal, Diagonal }`
  - `public sealed class SceneNode(string id, Rect bounds)` with `Id` and `Bounds`
  - `public sealed class SceneWire(string id, string group, string sourceNode, string targetNode, Pt start, Pt end, WireStyle style)` with matching properties
  - `public sealed class Scene(IEnumerable<SceneNode>, IEnumerable<SceneWire>, double spacing)`:
    - `Nodes` and `Wires` are sorted by id (ordinal)
    - `Spacing`
    - `long Fingerprint()`
  - `public sealed class Junction(string group, Pt at)`
  - `public sealed class RouteResult(IReadOnlyDictionary<string, Pt[]> paths, IReadOnlyList<Junction> junctions)`
  - `public sealed class RouterState`: `int Count`, `bool TryGetLane(string, out double)`, `internal void Replace(IEnumerable<KeyValuePair<string, double>>)`
  - `internal sealed class SpatialGrid(double cell)`: `Add(int id, Rect r)` (replaces an existing id), `Remove(int id)`, `Query(Rect r, List<int> into)` (sorted, unique, intersecting)
  - `internal enum WireKind { Straight, Lane, Back }`
  - `internal sealed class WireJob`, the working fields used by Tasks 4–7:
    - `Index`, `Wire`, `Id`, `Group`, `Style`, `Routed`
    - `ANode`, `BNode` (node index or -1), `A`, `B` (node rects)
    - `Ax`, `Ay`, `Bx`, `By`, `Kind`, `PrevLane` (`double?`)
    - lanes: `Lo`, `Hi`, `YLo`, `YHi`, `Pref`, `P`, `Lane`
    - back wires: `Yb`, `HasBand`, `Band0`, `Band1`
    - 45°: `Diag`, `Offset`, `Target` (`List<Pt>`), `TargetBox`
  - `internal sealed class RouteContext(Scene, RouterState)`: `Scene`, `Sp`, `NodeRects` (`Rect[]`), `NodeGrid` (`SpatialGrid`), `Jobs` (`List<WireJob>`, where `Jobs[i].Index == i`)
  - Test helper `SceneBuilder`:
    - `Node(id, x, y, w = 80, h = 40)`
    - `Out(node, dy)`, `In(node, dy)`
    - `Wire(from, to, start, end, style, group = null)` returns an id `"w000"`, `"w001"`… in insertion order
    - `Wire(from, to, style, group = null)` connects edge midpoints
    - `Build()`

- [ ] **Step 1: Write the test helper**

`tests/StraightWire.Core.Tests/TestScenes.cs`:

```csharp
using System.Collections.Generic;
using StraightWire.Core;

namespace StraightWire.Core.Tests
{
    /// <summary>Builds small scenes by hand. Wire ids are w000, w001, … in the order added.</summary>
    internal sealed class SceneBuilder
    {
        readonly List<SceneNode> _nodes = new List<SceneNode>();
        readonly Dictionary<string, Rect> _rects = new Dictionary<string, Rect>();
        readonly List<SceneWire> _wires = new List<SceneWire>();

        public double Spacing { get; set; } = 10;

        public SceneBuilder Node(string id, double x, double y, double w = 80, double h = 40)
        {
            var r = Rect.FromXYWH(x, y, w, h);
            _rects[id] = r;
            _nodes.Add(new SceneNode(id, r));
            return this;
        }

        /// <summary>A point on a node's right edge, dy below its top.</summary>
        public Pt Out(string node, double dy) => new Pt(_rects[node].X1, _rects[node].Y0 + dy);

        /// <summary>A point on a node's left edge, dy below its top.</summary>
        public Pt In(string node, double dy) => new Pt(_rects[node].X0, _rects[node].Y0 + dy);

        public string Wire(string from, string to, Pt start, Pt end, WireStyle style, string group = null)
        {
            string id = "w" + _wires.Count.ToString("D3");
            _wires.Add(new SceneWire(id, group ?? from + ":0", from, to, start, end, style));
            return id;
        }

        /// <summary>From the middle of one node's right edge to the middle of another's left edge.</summary>
        public string Wire(string from, string to, WireStyle style, string group = null) =>
            Wire(from, to, Out(from, (_rects[from].Y1 - _rects[from].Y0) / 2),
                 In(to, (_rects[to].Y1 - _rects[to].Y0) / 2), style, group);

        public Scene Build() => new Scene(_nodes, _wires, Spacing);
    }
}
```

- [ ] **Step 2: Write the failing tests**

`tests/StraightWire.Core.Tests/SceneTests.cs`:

```csharp
using System.Linq;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class SceneTests
    {
        static SceneNode N(string id, double x) => new SceneNode(id, Rect.FromXYWH(x, 0, 80, 40));
        static SceneWire W(string id, WireStyle style = WireStyle.Diagonal) =>
            new SceneWire(id, "a:0", "a", "b", new Pt(80, 20), new Pt(200, 60), style);

        [Fact]
        public void Nodes_and_wires_are_sorted_by_id()
        {
            var s = new Scene(new[] { N("b", 0), N("a", 0) }, new[] { W("w2"), W("w1") }, 10);
            Assert.Equal(new[] { "a", "b" }, s.Nodes.Select(n => n.Id));
            Assert.Equal(new[] { "w1", "w2" }, s.Wires.Select(w => w.Id));
        }

        [Fact]
        public void Fingerprint_ignores_input_order()
        {
            var a = new Scene(new[] { N("a", 0), N("b", 200) }, new[] { W("w1"), W("w2") }, 10);
            var b = new Scene(new[] { N("b", 200), N("a", 0) }, new[] { W("w2"), W("w1") }, 10);
            Assert.Equal(a.Fingerprint(), b.Fingerprint());
        }

        [Fact]
        public void Fingerprint_changes_with_anything_routing_reads()
        {
            long baseline = new Scene(new[] { N("a", 0) }, new[] { W("w1") }, 10).Fingerprint();
            Assert.NotEqual(baseline, new Scene(new[] { N("a", 0.5) }, new[] { W("w1") }, 10).Fingerprint());
            Assert.NotEqual(baseline, new Scene(new[] { N("a", 0) }, new[] { W("w1", WireStyle.Orthogonal) }, 10).Fingerprint());
            Assert.NotEqual(baseline, new Scene(new[] { N("a", 0) }, new[] { W("w1") }, 12).Fingerprint());
        }
    }
}
```

`tests/StraightWire.Core.Tests/SpatialGridTests.cs`:

```csharp
using System.Collections.Generic;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class SpatialGridTests
    {
        [Fact]
        public void Query_returns_only_intersecting_ids_sorted_and_once()
        {
            var g = new SpatialGrid(64);
            g.Add(3, new Rect(0, 0, 300, 300));   // spans many cells
            g.Add(1, new Rect(10, 10, 20, 20));
            g.Add(2, new Rect(500, 500, 510, 510));
            var hits = new List<int>();
            g.Query(new Rect(0, 0, 100, 100), hits);
            Assert.Equal(new[] { 1, 3 }, hits);
        }

        [Fact]
        public void Remove_and_re_add_move_an_item()
        {
            var g = new SpatialGrid(64);
            g.Add(1, new Rect(0, 0, 10, 10));
            g.Add(1, new Rect(1000, 1000, 1010, 1010));   // replaces
            var hits = new List<int>();
            g.Query(new Rect(0, 0, 10, 10), hits);
            Assert.Empty(hits);
            g.Query(new Rect(1000, 1000, 1001, 1001), hits);
            Assert.Equal(new[] { 1 }, hits);
            g.Remove(1);
            g.Query(new Rect(1000, 1000, 1001, 1001), hits);
            Assert.Empty(hits);
        }

        [Fact]
        public void Negative_coordinates_work()
        {
            var g = new SpatialGrid(64);
            g.Add(7, new Rect(-200, -200, -150, -150));
            var hits = new List<int>();
            g.Query(new Rect(-160, -160, -100, -100), hits);
            Assert.Equal(new[] { 7 }, hits);
        }
    }
}
```

`tests/StraightWire.Core.Tests/RouteContextTests.cs`:

```csharp
using System.Collections.Generic;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class RouteContextTests
    {
        [Fact]
        public void Wires_are_classified_by_their_ends()
        {
            var b = new SceneBuilder().Node("S", 0, 0).Node("T", 300, 0);
            b.Wire("S", "T", new Pt(80, 20), new Pt(300, 60), WireStyle.Diagonal);    // lane
            b.Wire("S", "T", new Pt(80, 20), new Pt(300, 20.3), WireStyle.Diagonal);  // straight
            b.Wire("S", "T", new Pt(80, 20), new Pt(87, 60), WireStyle.Diagonal);     // dx 7: back
            var ctx = new RouteContext(b.Build(), null);
            Assert.Equal(new[] { WireKind.Lane, WireKind.Straight, WireKind.Back },
                new[] { ctx.Jobs[0].Kind, ctx.Jobs[1].Kind, ctx.Jobs[2].Kind });
        }

        [Fact]
        public void Jobs_know_their_nodes_and_index()
        {
            var b = new SceneBuilder().Node("S", 0, 0).Node("T", 300, 0);
            b.Wire("S", "T", WireStyle.Orthogonal);
            var ctx = new RouteContext(b.Build(), null);
            var j = ctx.Jobs[0];
            Assert.Equal(0, j.Index);
            Assert.Equal(new Rect(0, 0, 80, 40).ToString(), j.A.ToString());
            Assert.Equal(new Rect(300, 0, 380, 40).ToString(), j.B.ToString());
            Assert.Equal(ctx.NodeRects[j.ANode].ToString(), j.A.ToString());
        }

        [Fact]
        public void Previous_lanes_come_from_the_state()
        {
            var b = new SceneBuilder().Node("S", 0, 0).Node("T", 300, 0);
            string id = b.Wire("S", "T", WireStyle.Orthogonal);
            var state = new RouterState();
            state.Replace(new[] { new KeyValuePair<string, double>(id, 150) });
            Assert.Equal(150, new RouteContext(b.Build(), state).Jobs[0].PrevLane);
            Assert.Null(new RouteContext(b.Build(), null).Jobs[0].PrevLane);
        }

        [Fact]
        public void A_missing_node_becomes_a_point_at_the_port()
        {
            var b = new SceneBuilder().Node("S", 0, 0);
            b.Wire("S", "gone", new Pt(80, 20), new Pt(300, 60), WireStyle.Diagonal);
            var j = new RouteContext(b.Build(), null).Jobs[0];
            Assert.Equal(-1, j.BNode);
            Assert.Equal(new Rect(300, 60, 300, 60).ToString(), j.B.ToString());
        }
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/StraightWire.Core.Tests --nologo -v q`
Expected: the build fails with `CS0246` errors for `Scene`, `SpatialGrid`, `RouteContext` and `RouterState`.

- [ ] **Step 4: Write the scene model**

`src/StraightWire.Core/Scene.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace StraightWire.Core
{
    public enum WireStyle { Curve, Orthogonal, Diagonal }

    /// <summary>Something wires avoid: a top-level component or parameter on the canvas.</summary>
    public sealed class SceneNode
    {
        public SceneNode(string id, Rect bounds)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Bounds = bounds;
        }

        public string Id { get; }
        public Rect Bounds { get; }
    }

    /// <summary>
    /// One wire, from a source parameter's output grip (Start) to a target parameter's input grip
    /// (End). Wires with the same Group come from the same source parameter and may share a trunk.
    /// Curve wires are not routed; they are in the scene so junctions know their group is mixed.
    /// </summary>
    public sealed class SceneWire
    {
        public SceneWire(string id, string group, string sourceNode, string targetNode, Pt start, Pt end, WireStyle style)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Group = group ?? throw new ArgumentNullException(nameof(group));
            SourceNode = sourceNode ?? "";
            TargetNode = targetNode ?? "";
            Start = start;
            End = end;
            Style = style;
        }

        public string Id { get; }
        public string Group { get; }
        public string SourceNode { get; }
        public string TargetNode { get; }
        public Pt Start { get; }
        public Pt End { get; }
        public WireStyle Style { get; }
    }

    /// <summary>
    /// Everything routing reads. Nodes and wires are sorted by id, so the same canvas always
    /// routes the same way, whatever order Grasshopper lists its objects in.
    /// </summary>
    public sealed class Scene
    {
        public Scene(IEnumerable<SceneNode> nodes, IEnumerable<SceneWire> wires, double spacing)
        {
            Nodes = nodes.OrderBy(n => n.Id, StringComparer.Ordinal).ToArray();
            Wires = wires.OrderBy(w => w.Id, StringComparer.Ordinal).ToArray();
            Spacing = spacing;
        }

        public IReadOnlyList<SceneNode> Nodes { get; }
        public IReadOnlyList<SceneWire> Wires { get; }
        public double Spacing { get; }

        /// <summary>
        /// A 64-bit FNV-1a hash of everything routing depends on. The plugin re-routes only when
        /// this changes, so panning, zooming, selecting and solving cost nothing.
        /// </summary>
        public long Fingerprint()
        {
            var h = new Fnv();
            h.Add(Spacing);
            h.Add(Nodes.Count);
            foreach (var n in Nodes)
            {
                h.Add(n.Id);
                h.Add(n.Bounds);
            }
            h.Add(Wires.Count);
            foreach (var w in Wires)
            {
                h.Add(w.Id);
                h.Add(w.Group);
                h.Add(w.SourceNode);
                h.Add(w.TargetNode);
                h.Add(w.Start);
                h.Add(w.End);
                h.Add((int)w.Style);
            }
            return h.Value;
        }

        sealed class Fnv
        {
            ulong _h = 14695981039346656037UL;

            public long Value => unchecked((long)_h);

            void Byte(byte b)
            {
                _h ^= b;
                _h = unchecked(_h * 1099511628211UL);
            }

            void Add(ulong v)
            {
                for (int i = 0; i < 8; i++)
                {
                    Byte((byte)v);
                    v >>= 8;
                }
            }

            public void Add(double d) => Add(unchecked((ulong)BitConverter.DoubleToInt64Bits(d)));
            public void Add(int i) => Add(unchecked((ulong)(uint)i));
            public void Add(Pt p) { Add(p.X); Add(p.Y); }
            public void Add(Rect r) { Add(r.X0); Add(r.Y0); Add(r.X1); Add(r.Y1); }

            public void Add(string s)
            {
                Add(s.Length);
                foreach (char c in s)
                {
                    Byte((byte)c);
                    Byte((byte)(c >> 8));
                }
            }
        }
    }
}
```

- [ ] **Step 5: Write the result types**

`src/StraightWire.Core/RouteResult.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace StraightWire.Core
{
    /// <summary>A dot where wires from the same source part ways.</summary>
    public sealed class Junction
    {
        public Junction(string group, Pt at)
        {
            Group = group;
            At = at;
        }

        public string Group { get; }
        public Pt At { get; }
    }

    public sealed class RouteResult
    {
        public RouteResult(IReadOnlyDictionary<string, Pt[]> paths, IReadOnlyList<Junction> junctions)
        {
            Paths = paths;
            Junctions = junctions;
        }

        /// <summary>The polyline of every routed wire, by wire id. Curve wires have no entry.</summary>
        public IReadOnlyDictionary<string, Pt[]> Paths { get; }

        public IReadOnlyList<Junction> Junctions { get; }
    }

    /// <summary>
    /// What routing remembers between calls: each lane wire's last lane x. It keeps a lane in the
    /// free stretch it was in while a component is dragged, instead of jumping at every tie.
    /// </summary>
    public sealed class RouterState
    {
        readonly Dictionary<string, double> _lanes = new Dictionary<string, double>(StringComparer.Ordinal);

        public int Count => _lanes.Count;

        public bool TryGetLane(string wireId, out double x) => _lanes.TryGetValue(wireId, out x);

        internal void Replace(IEnumerable<KeyValuePair<string, double>> lanes)
        {
            _lanes.Clear();
            foreach (var kv in lanes) _lanes[kv.Key] = kv.Value;
        }
    }
}
```

- [ ] **Step 6: Write the spatial grid**

`src/StraightWire.Core/SpatialGrid.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace StraightWire.Core
{
    /// <summary>
    /// A uniform grid of rectangles keyed by int id. It turns "which nodes or wires are near
    /// this?" from a scan of everything into a look at a few cells.
    /// </summary>
    internal sealed class SpatialGrid
    {
        readonly double _cell;
        readonly Dictionary<long, List<int>> _cells = new Dictionary<long, List<int>>();
        readonly Dictionary<int, Rect> _items = new Dictionary<int, Rect>();
        readonly HashSet<int> _seen = new HashSet<int>();

        public SpatialGrid(double cell)
        {
            _cell = cell;
        }

        /// <summary>Adds an item, or moves it if the id is already in the grid.</summary>
        public void Add(int id, Rect r)
        {
            Remove(id);
            _items[id] = r;
            if (!Range(r, out long x0, out long x1, out long y0, out long y1)) return;
            for (long x = x0; x <= x1; x++)
                for (long y = y0; y <= y1; y++)
                {
                    long key = Key(x, y);
                    if (!_cells.TryGetValue(key, out var list)) _cells[key] = list = new List<int>();
                    list.Add(id);
                }
        }

        public void Remove(int id)
        {
            if (!_items.TryGetValue(id, out var r)) return;
            _items.Remove(id);
            if (!Range(r, out long x0, out long x1, out long y0, out long y1)) return;
            for (long x = x0; x <= x1; x++)
                for (long y = y0; y <= y1; y++)
                    if (_cells.TryGetValue(Key(x, y), out var list)) list.Remove(id);
        }

        /// <summary>Fills <paramref name="into"/> with the ids whose rectangle intersects r, ascending, once each.</summary>
        public void Query(Rect r, List<int> into)
        {
            into.Clear();
            _seen.Clear();
            if (!Range(r, out long x0, out long x1, out long y0, out long y1)) return;
            for (long x = x0; x <= x1; x++)
                for (long y = y0; y <= y1; y++)
                {
                    if (!_cells.TryGetValue(Key(x, y), out var list)) continue;
                    foreach (int id in list)
                        if (_seen.Add(id) && _items[id].Intersects(r)) into.Add(id);
                }
            into.Sort();
        }

        bool Range(Rect r, out long x0, out long x1, out long y0, out long y1)
        {
            x0 = x1 = y0 = y1 = 0;
            if (!IsFinite(r.X0) || !IsFinite(r.Y0) || !IsFinite(r.X1) || !IsFinite(r.Y1)) return false;
            x0 = (long)Math.Floor(r.X0 / _cell);
            x1 = (long)Math.Floor(r.X1 / _cell);
            y0 = (long)Math.Floor(r.Y0 / _cell);
            y1 = (long)Math.Floor(r.Y1 / _cell);
            return true;
        }

        static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        static long Key(long x, long y) => (x << 32) ^ (y & 0xffffffffL);
    }
}
```

- [ ] **Step 7: Write the routing context**

`src/StraightWire.Core/RouteContext.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace StraightWire.Core
{
    internal enum WireKind { Straight, Lane, Back }

    /// <summary>
    /// Working state for one wire during a single Route call (the prototype's `w`). Each routing
    /// stage fills in its own group of fields.
    /// </summary>
    internal sealed class WireJob
    {
        public WireJob(int index, SceneWire wire, int aNode, int bNode, Rect a, Rect b)
        {
            Index = index;
            Wire = wire;
            ANode = aNode;
            BNode = bNode;
            A = a;
            B = b;
            Ax = wire.Start.X;
            Ay = wire.Start.Y;
            Bx = wire.End.X;
            By = wire.End.Y;
            double dx = Bx - Ax, dy = By - Ay;
            Kind = dx < Routing.BackwardBelow ? WireKind.Back
                 : Math.Abs(dy) < Routing.StraightBelow ? WireKind.Straight
                 : WireKind.Lane;
        }

        public int Index { get; }
        public SceneWire Wire { get; }
        public string Id => Wire.Id;
        public string Group => Wire.Group;
        public WireStyle Style => Wire.Style;
        public bool Routed => Wire.Style != WireStyle.Curve;

        /// <summary>Index of the source / target node in RouteContext.NodeRects, or -1.</summary>
        public int ANode { get; }
        public int BNode { get; }

        /// <summary>Source and target node rectangles (a point at the port when the node is unknown).</summary>
        public Rect A { get; }
        public Rect B { get; }

        public double Ax { get; }
        public double Ay { get; }
        public double Bx { get; }
        public double By { get; }
        public WireKind Kind { get; }

        /// <summary>The lane x this wire had in the previous Route call, if it had one.</summary>
        public double? PrevLane;

        // Lane wires (Lanes)
        public double Lo, Hi, YLo, YHi, Pref, P, Lane;

        // Backwards wires (BackWires)
        public double Yb;
        public bool HasBand;
        public double Band0, Band1;

        // 45° decision (Diagonals)
        public bool Diag;
        public double Offset;
        public List<Pt> Target;
        public Rect TargetBox;
    }

    internal sealed class RouteContext
    {
        public RouteContext(Scene scene, RouterState state)
        {
            Scene = scene;
            Sp = scene.Spacing;
            NodeRects = new Rect[scene.Nodes.Count];
            NodeGrid = new SpatialGrid(Routing.GridCell);
            var byId = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < scene.Nodes.Count; i++)
            {
                NodeRects[i] = scene.Nodes[i].Bounds;
                NodeGrid.Add(i, NodeRects[i]);
                byId[scene.Nodes[i].Id] = i;
            }
            Jobs = new List<WireJob>(scene.Wires.Count);
            for (int i = 0; i < scene.Wires.Count; i++)
            {
                var w = scene.Wires[i];
                int a = byId.TryGetValue(w.SourceNode, out int ai) ? ai : -1;
                int b = byId.TryGetValue(w.TargetNode, out int bi) ? bi : -1;
                var job = new WireJob(i, w, a, b,
                    a >= 0 ? NodeRects[a] : new Rect(w.Start.X, w.Start.Y, w.Start.X, w.Start.Y),
                    b >= 0 ? NodeRects[b] : new Rect(w.End.X, w.End.Y, w.End.X, w.End.Y));
                if (state != null && state.TryGetLane(w.Id, out double x)) job.PrevLane = x;
                Jobs.Add(job);
            }
        }

        public Scene Scene { get; }
        public double Sp { get; }
        public Rect[] NodeRects { get; }
        public SpatialGrid NodeGrid { get; }

        /// <summary>One job per scene wire, in scene order: Jobs[i].Index == i.</summary>
        public List<WireJob> Jobs { get; }
    }
}
```

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test tests/StraightWire.Core.Tests --nologo -v q`
Expected: `Passed!  - Failed: 0, Passed: 42`.

- [ ] **Step 9: Commit**

```bash
git add src/StraightWire.Core tests/StraightWire.Core.Tests
git commit -F - <<'EOF'
Add scene model, spatial grid and routing context

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GKJGcqmxvBcFYsUcoRsYRS
EOF
```

---

### Task 4: Lanes (feasible range, crossing order, push-and-shove)

**Files:**
- Create: `src/StraightWire.Core/Lanes.cs`
- Test: `tests/StraightWire.Core.Tests/LanesTests.cs`

**Interfaces:**
- Consumes: `RouteContext`, `WireJob`, `WireKind`, `SpatialGrid.Query`, `Intervals.Cut`, `Geo.Clamp`, `Routing` (Tasks 1, 3).
- Produces: `internal static class Lanes`:
  - `List<WireJob> Prepare(RouteContext ctx)`: routed lane wires in index order, with `Lo`, `Hi`, `YLo`, `YHi` and `Pref` set.
  - `void Feasible(RouteContext ctx, WireJob w, List<int> scratch)`: sets `w.Lo` and `w.Hi`.
  - `int CrossCount(WireJob a, double xa, WireJob b, double xb)`.
  - `void Relax(IReadOnlyList<WireJob> lanes, double sp)`: sets `P` and `Lane`.

Scenario used below: source `S` at (-80, 80, 80×40), whose output is (0, 100), and target `T` at (400, 280, 80×40), whose input is (400, 300). The feasible range starts as [18, 382]. An obstacle `O` that is 40 wide spans y 150–250, so it overlaps the wire's vertical run.

- [ ] **Step 1: Write the failing tests**

`tests/StraightWire.Core.Tests/LanesTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class LanesTests
    {
        static SceneBuilder Corridor() => new SceneBuilder().Node("S", -80, 80).Node("T", 400, 280);

        static WireJob FeasibleOf(SceneBuilder b, RouterState state = null)
        {
            var ctx = new RouteContext(b.Build(), state);
            var w = ctx.Jobs[0];
            Lanes.Feasible(ctx, w, new List<int>());
            return w;
        }

        [Fact]
        public void Feasible_uses_the_whole_run_when_nothing_is_in_the_way()
        {
            var b = Corridor();
            b.Wire("S", "T", WireStyle.Orthogonal);
            var w = FeasibleOf(b);
            Assert.Equal((18.0, 382.0), (w.Lo, w.Hi));
        }

        [Fact]
        public void Feasible_picks_the_wider_free_stretch()
        {
            var b = Corridor().Node("O", 184, 150, 40, 100);   // with clearance: 176..232
            b.Wire("S", "T", WireStyle.Orthogonal);
            var w = FeasibleOf(b);
            Assert.Equal((18.0, 176.0), (w.Lo, w.Hi));
        }

        [Fact]
        public void Feasible_prefers_the_right_stretch_on_a_tie()
        {
            var b = Corridor().Node("O", 180, 150, 40, 100);   // 172..228: both stretches 154 wide
            b.Wire("S", "T", WireStyle.Orthogonal);
            var w = FeasibleOf(b);
            Assert.Equal((228.0, 382.0), (w.Lo, w.Hi));
        }

        [Fact]
        public void Feasible_keeps_the_stretch_holding_the_previous_lane()
        {
            var b = Corridor().Node("O", 180, 150, 40, 100);
            string id = b.Wire("S", "T", WireStyle.Orthogonal);
            var state = new RouterState();
            state.Replace(new[] { new KeyValuePair<string, double>(id, 95) });
            var w = FeasibleOf(b, state);
            Assert.Equal((18.0, 172.0), (w.Lo, w.Hi));
        }

        [Fact]
        public void Feasible_stops_short_of_a_node_in_front_of_the_source_port()
        {
            var b = Corridor().Node("P", 100, 90, 40, 20);     // sits on y = 100, the source's row
            b.Wire("S", "T", WireStyle.Orthogonal);
            var w = FeasibleOf(b);
            Assert.Equal((18.0, 92.0), (w.Lo, w.Hi));
        }

        [Fact]
        public void Feasible_falls_back_to_the_whole_run_when_everything_is_blocked()
        {
            var b = Corridor().Node("O", 0, 150, 400, 100);
            b.Wire("S", "T", WireStyle.Orthogonal);
            var w = FeasibleOf(b);
            Assert.Equal((18.0, 382.0), (w.Lo, w.Hi));
        }

        static (WireJob A, WireJob B) TwoStacked()
        {
            var b = new SceneBuilder().Node("S1", -80, 80).Node("T1", 400, 280)
                                      .Node("S2", -80, 130).Node("T2", 400, 330);
            b.Wire("S1", "T1", WireStyle.Orthogonal);   // 100 -> 300
            b.Wire("S2", "T2", WireStyle.Orthogonal);   // 150 -> 350
            var ctx = new RouteContext(b.Build(), null);
            var lanes = Lanes.Prepare(ctx);
            Lanes.Relax(lanes, ctx.Sp);
            return (lanes[0], lanes[1]);
        }

        [Fact]
        public void CrossCount_counts_horizontal_runs_crossing_the_other_lane()
        {
            var b = new SceneBuilder().Node("S1", -80, 80).Node("T1", 400, 280)
                                      .Node("S2", -80, 130).Node("T2", 400, 330);
            b.Wire("S1", "T1", WireStyle.Orthogonal);
            b.Wire("S2", "T2", WireStyle.Orthogonal);
            var ctx = new RouteContext(b.Build(), null);
            WireJob a = ctx.Jobs[0], c = ctx.Jobs[1];
            Assert.Equal(2, Lanes.CrossCount(a, 195, c, 205));
            Assert.Equal(0, Lanes.CrossCount(a, 205, c, 195));
        }

        [Fact]
        public void Relax_orders_lanes_to_avoid_crossings_and_keeps_the_spacing()
        {
            var (a, b) = TwoStacked();
            Assert.True(a.Lane > b.Lane, $"a {a.Lane} should be right of b {b.Lane}");
            Assert.True(a.Lane - b.Lane >= 10 - 1e-9, $"gap {a.Lane - b.Lane}");
            Assert.InRange(a.Lane, a.Lo, a.Hi);
            Assert.InRange(b.Lane, b.Lo, b.Hi);
        }

        [Fact]
        public void Relax_draws_same_source_wires_onto_one_trunk()
        {
            var b = new SceneBuilder().Node("S", -80, 80).Node("T1", 400, 280).Node("T2", 300, -120);
            b.Wire("S", "T1", WireStyle.Orthogonal);   // range 18..382, prefers 200
            b.Wire("S", "T2", WireStyle.Orthogonal);   // range 18..282, prefers 150
            var ctx = new RouteContext(b.Build(), null);
            var lanes = Lanes.Prepare(ctx);
            Lanes.Relax(lanes, ctx.Sp);
            Assert.True(Math.Abs(lanes[0].Lane - lanes[1].Lane) < 0.01);
            Assert.Equal(175, lanes[0].Lane, 2);
        }

        [Fact]
        public void Prepare_skips_curve_straight_and_backwards_wires()
        {
            var b = new SceneBuilder().Node("S", 0, 0).Node("T", 300, 0);
            b.Wire("S", "T", new Pt(80, 20), new Pt(300, 60), WireStyle.Curve);
            b.Wire("S", "T", new Pt(80, 20), new Pt(300, 20), WireStyle.Diagonal);
            b.Wire("S", "T", new Pt(80, 20), new Pt(50, 60), WireStyle.Diagonal);
            b.Wire("S", "T", new Pt(80, 20), new Pt(300, 60), WireStyle.Orthogonal);
            var lanes = Lanes.Prepare(new RouteContext(b.Build(), null));
            Assert.Equal(new[] { "w003" }, lanes.ConvertAll(l => l.Id));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/StraightWire.Core.Tests --nologo -v q --filter "FullyQualifiedName~LanesTests"`
Expected: the build fails with `CS0103: The name 'Lanes' does not exist in the current context`.

- [ ] **Step 3: Write the lanes**

`src/StraightWire.Core/Lanes.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace StraightWire.Core
{
    /// <summary>
    /// Vertical lanes for forward wires that bend: where each may go (Feasible), and how they
    /// spread out to keep the spacing and cross as little as possible (Relax). Ported from the
    /// prototype's feasible(), crossCount() and relax(), without dragging or animation.
    /// </summary>
    internal static class Lanes
    {
        /// <summary>Finds each routed lane wire's range and preferred x. Returns them in index order.</summary>
        public static List<WireJob> Prepare(RouteContext ctx)
        {
            var lanes = new List<WireJob>();
            var scratch = new List<int>();
            foreach (var w in ctx.Jobs)
            {
                if (!w.Routed || w.Kind != WireKind.Lane) continue;
                Feasible(ctx, w, scratch);
                w.YLo = Math.Min(w.Ay, w.By);
                w.YHi = Math.Max(w.Ay, w.By);
                w.Pref = w.Lo + (w.Hi - w.Lo) / 2;
                lanes.Add(w);
            }
            return lanes;
        }

        /// <summary>
        /// The x range a wire's vertical run may use: between the two stubs, minus every node
        /// (with clearance) that overlaps the run vertically, and short of any node sitting in
        /// front of either port. The free stretch holding the previous lane wins (hysteresis);
        /// otherwise the widest, and the rightmost on a tie.
        /// </summary>
        public static void Feasible(RouteContext ctx, WireJob w, List<int> scratch)
        {
            const double C = Routing.Clearance;
            double sx = w.Ax, sy = w.Ay, tx = w.Bx, ty = w.By;
            double lo = sx + Routing.Stub, hi = tx - Routing.Stub;
            if (lo > hi)
            {
                w.Lo = w.Hi = (sx + tx) / 2;
                return;
            }
            double lo0 = lo, hi0 = hi;
            double y0 = Math.Min(sy, ty), y1 = Math.Max(sy, ty);
            var forbidden = new List<(double Lo, double Hi)>();
            ctx.NodeGrid.Query(new Rect(lo0 - C, y0 - C, hi0 + C, y1 + C), scratch);
            foreach (int k in scratch)
            {
                if (k == w.ANode || k == w.BNode) continue;
                var n = ctx.NodeRects[k];
                double x0 = n.X0 - C, x1 = n.X1 + C, ny0 = n.Y0 - C, ny1 = n.Y1 + C;
                if (x1 <= lo0 || x0 >= hi0) continue;
                if (sy > ny0 && sy < ny1 && x0 > sx) hi = Math.Min(hi, x0);
                if (ty > ny0 && ty < ny1 && x1 < tx) lo = Math.Max(lo, x1);
                if (ny1 > y0 && ny0 < y1) forbidden.Add((x0, x1));
            }
            if (lo > hi)
            {
                w.Lo = lo0;
                w.Hi = hi0;
                return;
            }
            var spans = new List<(double Lo, double Hi)> { (lo, hi) };
            foreach (var (p, q) in forbidden) spans = Intervals.Cut(spans, p, q);
            if (spans.Count == 0)
            {
                w.Lo = lo;
                w.Hi = hi;
                return;
            }
            if (w.PrevLane is double probe)
            {
                bool found = false;
                (double Lo, double Hi) pick = default;
                double pd = double.PositiveInfinity;
                foreach (var s in spans)
                {
                    if (s.Hi - s.Lo < 4) continue;
                    double d = probe < s.Lo ? s.Lo - probe : probe > s.Hi ? probe - s.Hi : 0;
                    if (d < pd)
                    {
                        pd = d;
                        pick = s;
                        found = true;
                    }
                }
                if (found && pd <= 1)
                {
                    w.Lo = pick.Lo;
                    w.Hi = pick.Hi;
                    return;
                }
            }
            var best = spans[0];
            foreach (var s in spans)
            {
                double ds = (s.Hi - s.Lo) - (best.Hi - best.Lo);
                if (ds > 1 || (ds > -1 && s.Lo > best.Lo)) best = s;
            }
            w.Lo = best.Lo;
            w.Hi = best.Hi;
        }

        static int Hv(double hy, double hx0, double hx1, double vx, double vy0, double vy1)
        {
            double x0 = Math.Min(hx0, hx1), x1 = Math.Max(hx0, hx1), y0 = Math.Min(vy0, vy1), y1 = Math.Max(vy0, vy1);
            return vx > x0 + 0.01 && vx < x1 - 0.01 && hy > y0 + 0.01 && hy < y1 - 0.01 ? 1 : 0;
        }

        /// <summary>How many times two right-angle wires cross with lanes at xa and xb.</summary>
        public static int CrossCount(WireJob a, double xa, WireJob b, double xb) =>
            Hv(a.Ay, a.Ax, xa, xb, b.Ay, b.By) + Hv(a.By, xa, a.Bx, xb, b.Ay, b.By) +
            Hv(b.Ay, b.Ax, xb, xa, a.Ay, a.By) + Hv(b.By, xb, b.Bx, xa, a.Ay, a.By);

        /// <summary>
        /// One-dimensional push-and-shove. Lanes whose runs overlap vertically are pushed at least
        /// <paramref name="sp"/> apart, in the left-right order that crosses least; lanes from the
        /// same source are pulled together so they share a trunk. Every lane stays in its range.
        /// </summary>
        public static void Relax(IReadOnlyList<WireJob> lanes, double sp)
        {
            foreach (var w in lanes) w.P = Geo.Clamp(w.Pref, w.Lo, w.Hi);
            var pairs = Pairs(lanes, sp);
            for (int it = 0; it < Routing.RelaxIterations; it++)
            {
                foreach (var pr in pairs)
                {
                    if (pr.Same)
                    {
                        double d = pr.B.P - pr.A.P;
                        pr.A.P += d * 0.25;
                        pr.B.P -= d * 0.25;
                    }
                    else
                    {
                        double gap = pr.Right.P - pr.Left.P;
                        if (gap >= sp) continue;
                        double d = sp - gap;
                        pr.Left.P -= d / 2;
                        pr.Right.P += d / 2;
                    }
                }
                foreach (var w in lanes) w.P = Geo.Clamp(w.P, w.Lo, w.Hi);
            }
            foreach (var w in lanes)
            {
                double span = w.Hi - w.Lo;
                w.Lane = span > 0.5 ? w.P : w.Lo + span / 2;
            }
        }

        sealed class Pair
        {
            public WireJob A, B, Left, Right;
            public bool Same;
        }

        /// <summary>
        /// The pairs that interact, found by a sweep over runs sorted by top edge instead of every
        /// pair of lanes. They are processed in (lower index, higher index) order, the prototype's
        /// double-loop order, so results do not depend on the sweep.
        /// </summary>
        static List<Pair> Pairs(IReadOnlyList<WireJob> lanes, double sp)
        {
            var keys = new List<(int I, int J)>();
            int[] byY = Enumerable.Range(0, lanes.Count).OrderBy(i => lanes[i].YLo).ThenBy(i => i).ToArray();
            for (int s = 0; s < byY.Length; s++)
            {
                var a = lanes[byY[s]];
                for (int t = s + 1; t < byY.Length; t++)
                {
                    var b = lanes[byY[t]];
                    if (b.YLo >= a.YHi + sp) break;
                    if (a.Group == b.Group) continue;   // paired below, whatever their heights
                    if (a.Hi + sp <= b.Lo || b.Hi + sp <= a.Lo) continue;
                    keys.Add(byY[s] < byY[t] ? (byY[s], byY[t]) : (byY[t], byY[s]));
                }
            }
            var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (int i = 0; i < lanes.Count; i++)
            {
                if (!groups.TryGetValue(lanes[i].Group, out var g)) groups[lanes[i].Group] = g = new List<int>();
                g.Add(i);
            }
            foreach (var g in groups.Values)
                for (int i = 0; i < g.Count; i++)
                    for (int j = i + 1; j < g.Count; j++)
                        keys.Add((g[i], g[j]));
            keys.Sort();

            var pairs = new List<Pair>(keys.Count);
            foreach (var (i, j) in keys)
            {
                WireJob a = lanes[i], b = lanes[j];
                if (a.Group == b.Group)
                {
                    pairs.Add(new Pair { A = a, B = b, Same = true });
                    continue;
                }
                double m = (a.P + b.P) / 2, x1 = m - sp / 2, x2 = m + sp / 2;
                int cl = CrossCount(a, x1, b, x2), cr = CrossCount(a, x2, b, x1);
                bool aLeft;
                if (cl != cr) aLeft = cl < cr;
                else
                {
                    double da = a.PrevLane ?? a.P, db = b.PrevLane ?? b.P;
                    aLeft = Math.Abs(da - db) > 0.5 ? da < db
                          : a.Pref != b.Pref ? a.Pref < b.Pref
                          : a.Index < b.Index;
                }
                pairs.Add(aLeft ? new Pair { A = a, B = b, Left = a, Right = b }
                                : new Pair { A = a, B = b, Left = b, Right = a });
            }
            return pairs;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/StraightWire.Core.Tests --nologo -v q`
Expected: `Passed!  - Failed: 0, Passed: 52`.

- [ ] **Step 5: Commit**

```bash
git add src/StraightWire.Core/Lanes.cs tests/StraightWire.Core.Tests/LanesTests.cs
git commit -F - <<'EOF'
Port lane ranges, crossing order and push-and-shove

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GKJGcqmxvBcFYsUcoRsYRS
EOF
```

---

### Task 5: Backwards wires

**Files:**
- Create: `src/StraightWire.Core/BackWires.cs`
- Test: `tests/StraightWire.Core.Tests/BackWiresTests.cs`

**Interfaces:**
- Consumes: `RouteContext`, `WireJob`, `Intervals.Cut`, `Geo.Clamp`, `Paths.Back`, `Routing` (Tasks 1–3).
- Produces: `internal static class BackWires`:
  - `void Prepare(RouteContext ctx)`: runs `Channel` for every routed `Back` wire.
  - `void Channel(RouteContext ctx, WireJob w, List<int> scratch)`: sets `Yb`, `HasBand`, `Band0` and `Band1`.
  - `List<Pt> Diagonal(WireJob w)`: returns null when no single diagonal fits.
  - `List<Pt> Orthogonal(WireJob w)`.

Scenario A: source `S` at (300, 200, 80×60) with output (380, 230); target `T` at (100, 0, 80×60) with input (100, 30). The gap between them is y 68–192. Scenario B (tall gap): `S` at (140, 400, 80×60) with output (220, 430), and the same `T`.

- [ ] **Step 1: Write the failing tests**

`tests/StraightWire.Core.Tests/BackWiresTests.cs`:

```csharp
using System.Collections.Generic;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class BackWiresTests
    {
        static Pt P(double x, double y) => new Pt(x, y);

        static WireJob Channelled(SceneBuilder b)
        {
            var ctx = new RouteContext(b.Build(), null);
            BackWires.Prepare(ctx);
            return ctx.Jobs[0];
        }

        static SceneBuilder ScenarioA()
        {
            var b = new SceneBuilder().Node("S", 300, 200, 80, 60).Node("T", 100, 0, 80, 60);
            b.Wire("S", "T", P(380, 230), P(100, 30), WireStyle.Orthogonal);
            return b;
        }

        static SceneBuilder ScenarioB()
        {
            var b = new SceneBuilder().Node("S", 140, 400, 80, 60).Node("T", 100, 0, 80, 60);
            b.Wire("S", "T", P(220, 430), P(100, 30), WireStyle.Diagonal);
            return b;
        }

        [Fact]
        public void Channel_crosses_in_the_gap_between_the_two_nodes()
        {
            var w = Channelled(ScenarioA());
            Assert.True(w.HasBand);
            Assert.Equal((68.0, 192.0, 130.0), (w.Band0, w.Band1, w.Yb));
        }

        [Fact]
        public void Channel_skips_a_node_parked_in_the_gap()
        {
            var w = Channelled(ScenarioA().Node("M", 200, 100, 60, 40));   // with clearance: 92..148
            Assert.Equal((148.0, 192.0, 170.0), (w.Band0, w.Band1, w.Yb));
        }

        [Fact]
        public void Channel_goes_around_when_the_nodes_overlap_vertically()
        {
            var b = new SceneBuilder().Node("S", 300, 0, 80, 60).Node("T", 100, 20, 80, 60);
            b.Wire("S", "T", P(380, 30), P(100, 50), WireStyle.Orthogonal);
            var w = Channelled(b);
            Assert.False(w.HasBand);
            Assert.Equal(-24, w.Yb);   // above and below cost the same; above wins
        }

        [Fact]
        public void Orthogonal_crosses_at_the_channel()
        {
            var w = Channelled(ScenarioA());
            Assert.Equal(new[] { P(380, 230), P(398, 230), P(398, 130), P(82, 130), P(82, 30), P(100, 30) },
                BackWires.Orthogonal(w));
        }

        [Fact]
        public void Diagonal_crosses_a_tall_gap_in_one_45_degree_run()
        {
            var w = Channelled(ScenarioB());
            Assert.Equal(new[] { P(220, 430), P(238, 430), P(238, 308), P(82, 152), P(82, 30), P(100, 30) },
                BackWires.Diagonal(w));
        }

        [Fact]
        public void Diagonal_is_null_when_the_gap_is_shorter_than_the_run() =>
            Assert.Null(BackWires.Diagonal(Channelled(ScenarioA())));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/StraightWire.Core.Tests --nologo -v q --filter "FullyQualifiedName~BackWiresTests"`
Expected: the build fails with `CS0103: The name 'BackWires' does not exist in the current context`.

- [ ] **Step 3: Write the backwards wires**

`src/StraightWire.Core/BackWires.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace StraightWire.Core
{
    /// <summary>
    /// Wires whose target sits left of their source. When one node is above the other, the
    /// crossing runs through the gap between them, skipping anything parked there; only when
    /// there is no gap does it go around. Ported from the prototype's backChannel() and backDiag().
    /// </summary>
    internal static class BackWires
    {
        public static void Prepare(RouteContext ctx)
        {
            var scratch = new List<int>();
            foreach (var w in ctx.Jobs)
                if (w.Routed && w.Kind == WireKind.Back) Channel(ctx, w, scratch);
        }

        public static void Channel(RouteContext ctx, WireJob w, List<int> scratch)
        {
            const double C = Routing.Clearance;
            Rect a = w.A, b = w.B;
            double xl = w.Bx - Routing.Stub - C, xr = w.Ax + Routing.Stub + C;
            bool hasGap = false;
            double g0 = 0, g1 = 0;
            if (b.Y1 + 2 * C <= a.Y0)
            {
                hasGap = true;
                g0 = b.Y1 + C;
                g1 = a.Y0 - C;
            }
            else if (a.Y1 + 2 * C <= b.Y0)
            {
                hasGap = true;
                g0 = a.Y1 + C;
                g1 = b.Y0 - C;
            }
            if (hasGap)
            {
                var free = new List<(double Lo, double Hi)> { (g0, g1) };
                ctx.NodeGrid.Query(new Rect(xl - C, g0 - C, xr + C, g1 + C), scratch);
                foreach (int k in scratch)
                {
                    if (k == w.ANode || k == w.BNode) continue;
                    var n = ctx.NodeRects[k];
                    if (n.X1 + C < xl || n.X0 - C > xr) continue;
                    free = Intervals.Cut(free, n.Y0 - C, n.Y1 + C);
                }
                bool found = false;
                (double Lo, double Hi) best = default;
                foreach (var s in free)
                {
                    if (s.Hi - s.Lo < 4) continue;
                    if (!found || s.Hi - s.Lo > best.Hi - best.Lo)
                    {
                        best = s;
                        found = true;
                    }
                }
                if (found)
                {
                    w.Yb = (best.Lo + best.Hi) / 2;
                    w.HasBand = true;
                    w.Band0 = best.Lo;
                    w.Band1 = best.Hi;
                    return;
                }
            }
            double above = Math.Min(a.Y0, b.Y0) - Routing.AroundGap;
            double below = Math.Max(a.Y1, b.Y1) + Routing.AroundGap;
            double Cost(double y) => Math.Abs(w.Ay - y) + Math.Abs(y - w.By);
            w.Yb = Cost(above) <= Cost(below) ? above : below;
            w.HasBand = false;
        }

        /// <summary>
        /// In 45° style the crossing becomes one diagonal when the gap is at least as tall as the
        /// horizontal run (keeping a chamfer's room at each end); otherwise null.
        /// </summary>
        public static List<Pt> Diagonal(WireJob w)
        {
            if (!w.HasBand) return null;
            double x1 = w.Ax + Routing.Stub, x2 = w.Bx - Routing.Stub, run = x1 - x2;
            double sg = w.By < w.Ay ? -1 : 1, m = Routing.Chamfer + 2;
            double lo = Math.Max(w.Band0, Math.Min(w.Ay, w.By) + m);
            double hi = Math.Min(w.Band1, Math.Max(w.Ay, w.By) - m);
            if (hi - lo < run) return null;
            double mid = Geo.Clamp(w.Yb, lo + run / 2, hi - run / 2);
            double yp = mid - sg * run / 2, yq = mid + sg * run / 2;
            return new List<Pt>
            {
                new Pt(w.Ax, w.Ay), new Pt(x1, w.Ay), new Pt(x1, yp),
                new Pt(x2, yq), new Pt(x2, w.By), new Pt(w.Bx, w.By),
            };
        }

        public static List<Pt> Orthogonal(WireJob w) => Paths.Back(new Pt(w.Ax, w.Ay), new Pt(w.Bx, w.By), w.Yb);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/StraightWire.Core.Tests --nologo -v q`
Expected: `Passed!  - Failed: 0, Passed: 58`.

- [ ] **Step 5: Commit**

```bash
git add src/StraightWire.Core/BackWires.cs tests/StraightWire.Core.Tests/BackWiresTests.cs
git commit -F - <<'EOF'
Route backwards wires through the gap between their nodes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GKJGcqmxvBcFYsUcoRsYRS
EOF
```

---

### Task 6: 45° decisions and final wire paths

**Files:**
- Create: `src/StraightWire.Core/WirePath.cs`
- Create: `src/StraightWire.Core/Diagonals.cs`
- Modify: `tests/StraightWire.Core.Tests/TestScenes.cs` (add `Pipeline`)
- Test: `tests/StraightWire.Core.Tests/DiagonalsTests.cs`

**Interfaces:**
- Consumes:
  - `Lanes.Prepare` and `Lanes.Relax` (Task 4)
  - `BackWires.Prepare`, `BackWires.Diagonal` and `BackWires.Orthogonal` (Task 5)
  - `Paths.*` (Task 2)
  - `SpatialGrid` (Task 3)
  - `Geo.PolyPolyWithin`, `Geo.SegRectHit` and `Geo.BBox` (Task 1)
- Produces:
  - `internal static class WirePath`: `List<Pt> For(WireJob w)`.
  - `internal static class Diagonals`: `void Decide(RouteContext ctx)`, which sets `Diag`, `Offset`, `Target` and `TargetBox`.
  - Test helper `Pipeline.Run(Scene, RouterState = null) → RouteContext`, which runs back channels, lanes, relax and decide.

- [ ] **Step 1: Add the pipeline helper**

Append to `tests/StraightWire.Core.Tests/TestScenes.cs`, inside the namespace:

```csharp
    /// <summary>The routing stages in Router's order, stopping before paths are built.</summary>
    internal static class Pipeline
    {
        public static RouteContext Run(Scene scene, RouterState state = null)
        {
            var ctx = new RouteContext(scene, state);
            BackWires.Prepare(ctx);
            var lanes = Lanes.Prepare(ctx);
            Lanes.Relax(lanes, ctx.Sp);
            Diagonals.Decide(ctx);
            return ctx;
        }
    }
```

- [ ] **Step 2: Write the failing tests**

`tests/StraightWire.Core.Tests/DiagonalsTests.cs`:

```csharp
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class DiagonalsTests
    {
        static Pt P(double x, double y) => new Pt(x, y);

        // S's output is (0, 0); T's input is (200, 100).
        static SceneBuilder Open() => new SceneBuilder().Node("S", -80, -20).Node("T", 200, 80);

        static Pt[] PathOf(SceneBuilder b) => WirePath.For(Pipeline.Run(b.Build()).Jobs[0]).ToArray();

        [Fact]
        public void An_unobstructed_45_degree_wire_bends_twice()
        {
            var b = Open();
            b.Wire("S", "T", WireStyle.Diagonal);
            Assert.Equal(new[] { P(0, 0), P(50, 0), P(150, 100), P(200, 100) }, PathOf(b));
        }

        [Fact]
        public void A_shallow_wire_is_one_diagonal_without_a_search()
        {
            var b = new SceneBuilder().Node("S", -80, -20).Node("T", 200, 0);   // input (200, 20)
            b.Wire("S", "T", WireStyle.Diagonal);
            Assert.Equal(new[] { P(0, 0), P(90, 0), P(110, 20), P(200, 20) }, PathOf(b));
        }

        [Fact]
        public void A_diagonal_that_would_hit_nodes_keeps_four_bends()
        {
            // Two nodes leave only a narrow free stretch at x 88..112. Every single diagonal
            // within reach crosses one of them; the four-bend route down x = 100 touches neither.
            var b = Open().Node("N1", 20, 30, 60, 40).Node("N2", 120, 30, 60, 40);
            b.Wire("S", "T", WireStyle.Diagonal);
            Assert.Equal(new[] { P(0, 0), P(86, 0), P(100, 14), P(100, 86), P(114, 100), P(200, 100) }, PathOf(b));
        }

        [Fact]
        public void Orthogonal_lane_wires_bend_at_right_angles()
        {
            var b = Open();
            b.Wire("S", "T", WireStyle.Orthogonal);
            Assert.Equal(new[] { P(0, 0), P(100, 0), P(100, 100), P(200, 100) }, PathOf(b));
        }

        [Fact]
        public void A_straight_wire_is_one_segment()
        {
            var b = Open();
            b.Wire("S", "T", P(0, 0), P(200, 0.3), WireStyle.Orthogonal);
            Assert.Equal(new[] { P(0, 0), P(200, 0.3) }, PathOf(b));
        }

        [Fact]
        public void A_45_degree_backwards_wire_chamfers_its_square_corners()
        {
            var b = new SceneBuilder().Node("S", 300, 200, 80, 60).Node("T", 100, 0, 80, 60);
            b.Wire("S", "T", P(380, 230), P(100, 30), WireStyle.Diagonal);
            Assert.Equal(new[]
            {
                P(380, 230), P(384, 230), P(398, 216), P(398, 144), P(384, 130),
                P(96, 130), P(82, 116), P(82, 44), P(96, 30), P(100, 30),
            }, PathOf(b));
        }
    }
}
```

The expected chamfer points come from `Paths.Chamfer` on `[(380,230),(398,230),(398,130),(82,130),(82,30),(100,30)]` with c = 14:
- The first leg (18 long) allows 16 and the second (100 long) allows 50, so the cut is 14: (384,230) and (398,216).
- At (398,130), both cuts are 14: (398,144) and (384,130).
- At (82,130): (96,130) and (82,116).
- At (82,30), the last leg (18) allows 16: (82,44) and (96,30).

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/StraightWire.Core.Tests --nologo -v q --filter "FullyQualifiedName~DiagonalsTests"`
Expected: the build fails with `CS0103` for `Diagonals` and `WirePath`.

- [ ] **Step 4: Write the final path builder**

`src/StraightWire.Core/WirePath.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace StraightWire.Core
{
    /// <summary>A wire's final polyline from the decisions made so far [pathFor, without animation].</summary>
    internal static class WirePath
    {
        public static List<Pt> For(WireJob w)
        {
            var a = new Pt(w.Ax, w.Ay);
            var b = new Pt(w.Bx, w.By);
            bool diagonal = w.Style == WireStyle.Diagonal;
            switch (w.Kind)
            {
                case WireKind.Straight:
                    return Paths.Dedupe(new[] { a, b });
                case WireKind.Lane:
                    if (!diagonal) return Paths.Simplify(Paths.Dedupe(Paths.Lane(a, b, w.Lane)));
                    double h = Math.Abs(w.By - w.Ay) / 2, c0 = Math.Min(Routing.Chamfer, h);
                    return Paths.Simplify(Paths.Diagonal(w.Ax, w.Ay, w.Bx, w.By,
                        w.Lane + (w.Diag ? w.Offset : 0), w.Diag ? h : c0));
                default:
                    var p = Paths.Dedupe((diagonal ? BackWires.Diagonal(w) : null) ?? BackWires.Orthogonal(w));
                    return Paths.Simplify(diagonal ? Paths.Dedupe(Paths.Chamfer(p, Routing.Chamfer)) : p);
            }
        }
    }
}
```

- [ ] **Step 5: Write the 45° decision**

`src/StraightWire.Core/Diagonals.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace StraightWire.Core
{
    /// <summary>
    /// In 45° style a lane wire tries to become one straight diagonal (two bends). It keeps the
    /// diagonal only if that touches no more wires and nodes than its four-bend route would.
    /// Wires are decided shortest rise first, each against the others' current paths.
    /// Ported from the prototype's decideDiag() and touches().
    /// </summary>
    internal static class Diagonals
    {
        public static void Decide(RouteContext ctx)
        {
            var grid = new SpatialGrid(Routing.GridCell);
            foreach (var w in ctx.Jobs)
            {
                w.Diag = false;
                w.Offset = 0;
                w.Target = null;
                if (!w.Routed) continue;
                w.Target = w.Kind == WireKind.Lane && w.Style == WireStyle.Diagonal
                    ? Paths.Diagonal(w.Ax, w.Ay, w.Bx, w.By, w.Lane, Routing.Chamfer)
                    : WirePath.For(w);
                w.TargetBox = Geo.BBox(w.Target, 0);
                grid.Add(w.Index, w.TargetBox);
            }

            double thr = ctx.Sp * Routing.TouchFraction;
            var wireHits = new List<int>();
            var nodeHits = new List<int>();
            var candidates = ctx.Jobs
                .Where(w => w.Routed && w.Kind == WireKind.Lane && w.Style == WireStyle.Diagonal)
                .OrderBy(w => Math.Abs(w.By - w.Ay)).ThenBy(w => w.Index)
                .ToList();
            foreach (var w in candidates)
            {
                double h = Math.Abs(w.By - w.Ay) / 2;
                if (h <= Routing.Chamfer)
                {
                    w.Diag = true;
                    continue;
                }
                double lo = w.Ax + Routing.DiagonalStub + h, hi = w.Bx - Routing.DiagonalStub - h;
                if (lo > hi) continue;
                int baseline = Touches(ctx, grid, w.Target, w, thr, wireHits, nodeHits);
                foreach (double off in Offsets(w.Lane, lo, hi, ctx.Sp))
                {
                    double x = w.Lane + off;
                    if (x < lo - 0.01 || x > hi + 0.01) continue;
                    var p = Paths.Diagonal(w.Ax, w.Ay, w.Bx, w.By, x, h);
                    if (Touches(ctx, grid, p, w, thr, wireHits, nodeHits) > baseline) continue;
                    w.Diag = true;
                    w.Offset = off;
                    w.Target = p;
                    w.TargetBox = Geo.BBox(p, 0);
                    grid.Add(w.Index, w.TargetBox);
                    break;
                }
            }
        }

        /// <summary>0, ±SP … ±8SP, then the two ends of the range; nearest first, ties in that order.</summary>
        static IEnumerable<double> Offsets(double lane, double lo, double hi, double sp)
        {
            var offs = new List<double> { 0 };
            for (int k = 1; k <= 8; k++)
            {
                offs.Add(k * sp);
                offs.Add(-k * sp);
            }
            offs.Add(lo - lane);
            offs.Add(hi - lane);
            return offs.Select((o, i) => (o, i)).OrderBy(t => Math.Abs(t.o)).ThenBy(t => t.i).Select(t => t.o).ToList();
        }

        /// <summary>
        /// How many other wires (not from the same source) come within <paramref name="thr"/> of
        /// the path, plus how many nodes (other than its own two, padded by 3) it crosses.
        /// </summary>
        internal static int Touches(RouteContext ctx, SpatialGrid wires, List<Pt> path, WireJob w, double thr,
                                    List<int> wireHits, List<int> nodeHits)
        {
            var bb = Geo.BBox(path, thr);
            int n = 0;
            wires.Query(bb, wireHits);
            foreach (int k in wireHits)
            {
                var o = ctx.Jobs[k];
                if (o == w || o.Target == null || o.Group == w.Group) continue;
                if (Geo.PolyPolyWithin(path, o.Target, thr)) n++;
            }
            ctx.NodeGrid.Query(bb.Inflate(Routing.TouchPad), nodeHits);
            foreach (int k in nodeHits)
            {
                if (k == w.ANode || k == w.BNode) continue;
                var r = ctx.NodeRects[k].Inflate(Routing.TouchPad);
                if (!bb.Intersects(r)) continue;
                for (int i = 0; i < path.Count - 1; i++)
                {
                    if (!Geo.SegRectHit(path[i], path[i + 1], r)) continue;
                    n++;
                    break;
                }
            }
            return n;
        }
    }
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/StraightWire.Core.Tests --nologo -v q`
Expected: `Passed!  - Failed: 0, Passed: 64`.

- [ ] **Step 7: Commit**

```bash
git add src/StraightWire.Core tests/StraightWire.Core.Tests
git commit -F - <<'EOF'
Decide 45-degree diagonals and build final wire paths

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GKJGcqmxvBcFYsUcoRsYRS
EOF
```

---

### Task 7: Junctions, the router, and routing invariants

**Files:**
- Create: `src/StraightWire.Core/Junctions.cs`
- Create: `src/StraightWire.Core/Router.cs`
- Modify: `tests/StraightWire.Core.Tests/TestScenes.cs` (add `TestScenes.Random`)
- Test: `tests/StraightWire.Core.Tests/JunctionsTests.cs`, `tests/StraightWire.Core.Tests/RouterTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 1–6.
- Produces:
  - `internal static class Junctions`: `Pt? Diverge(IReadOnlyList<Pt>, IReadOnlyList<Pt>)` and `List<Junction> Find(IReadOnlyList<(string Group, bool Routed, Pt[] Path)>)`.
  - `public static class Router`: `RouteResult Route(Scene scene, RouterState state)`. `state` may be null, which starts fresh; when given, it is updated with the new lanes.
  - Test helper `TestScenes.Random(int seed, int columns, int rows, int wires, WireStyle style, double spacing = 10) → Scene`.

- [ ] **Step 1: Add the random scene generator**

Append to `tests/StraightWire.Core.Tests/TestScenes.cs`, inside the namespace:

```csharp
    internal static class TestScenes
    {
        /// <summary>
        /// A seeded grid of nodes with wires running one to three columns right (one in twenty runs
        /// one column left, as does anything starting in the last column), up to four rows up or
        /// down. Each node has three output ports, so some wires share a source.
        /// </summary>
        public static Scene Random(int seed, int columns, int rows, int wires, WireStyle style, double spacing = 10)
        {
            var rng = new System.Random(seed);
            var rects = new Rect[columns, rows];
            var nodes = new List<SceneNode>();
            for (int c = 0; c < columns; c++)
                for (int r = 0; r < rows; r++)
                {
                    rects[c, r] = Rect.FromXYWH(c * 240 + rng.Next(0, 30), r * 140 + rng.Next(0, 20),
                                                70 + rng.Next(0, 40), 40 + rng.Next(0, 60));
                    nodes.Add(new SceneNode(NodeId(c, r), rects[c, r]));
                }
            var list = new List<SceneWire>();
            for (int i = 0; i < wires; i++)
            {
                int c = rng.Next(0, columns), r = rng.Next(0, rows);
                bool back = c > 0 && rng.NextDouble() < 0.05;
                int tc = back ? c - 1 : System.Math.Min(columns - 1, c + 1 + rng.Next(0, 3));
                if (tc == c) tc = System.Math.Max(0, c - 1);
                int tr = System.Math.Clamp(r + rng.Next(-4, 5), 0, rows - 1);
                Rect a = rects[c, r], b = rects[tc, tr];
                int port = rng.Next(0, 3);
                var start = new Pt(a.X1, a.Y0 + (a.Y1 - a.Y0) * (port + 1) / 4);
                var end = new Pt(b.X0, b.Y0 + (b.Y1 - b.Y0) * (rng.Next(0, 3) + 1) / 4);
                list.Add(new SceneWire("w" + i.ToString("D4"), NodeId(c, r) + ":" + port,
                                       NodeId(c, r), NodeId(tc, tr), start, end, style));
            }
            return new Scene(nodes, list, spacing);
        }

        static string NodeId(int c, int r) => "n" + c.ToString("D2") + r.ToString("D2");
    }
```

- [ ] **Step 2: Write the failing junction tests**

`tests/StraightWire.Core.Tests/JunctionsTests.cs`:

```csharp
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class JunctionsTests
    {
        static Pt P(double x, double y) => new Pt(x, y);
        static readonly Pt[] Down = { P(0, 0), P(100, 0), P(100, 50) };
        static readonly Pt[] Up = { P(0, 0), P(100, 0), P(100, -50) };

        [Fact]
        public void Diverge_finds_where_two_paths_part()
        {
            var p = Junctions.Diverge(Down, Up).Value;
            Assert.Equal(100, p.X, 6);
            Assert.InRange(p.Y, 0, 1);
        }

        [Fact]
        public void Diverge_is_null_for_the_same_path() => Assert.Null(Junctions.Diverge(Down, Down));

        [Fact]
        public void Find_puts_one_dot_where_same_source_wires_split()
        {
            var dot = Assert.Single(Junctions.Find(new[] { ("g", true, Down), ("g", true, Up) }));
            Assert.Equal("g", dot.Group);
            Assert.Equal(100, dot.At.X, 6);
        }

        [Fact]
        public void Find_skips_a_split_right_at_the_port() =>
            Assert.Empty(Junctions.Find(new[]
            {
                ("g", true, new[] { P(0, 0), P(0, 50) }),
                ("g", true, new[] { P(0, 0), P(0, -50) }),
            }));

        [Fact]
        public void Find_skips_a_group_with_a_curve_wire() =>
            Assert.Empty(Junctions.Find(new[] { ("g", true, Down), ("g", false, (Pt[])null), ("g", true, Up) }));

        [Fact]
        public void Find_ignores_wires_from_different_sources() =>
            Assert.Empty(Junctions.Find(new[] { ("g1", true, Down), ("g2", true, Up) }));
    }
}
```

- [ ] **Step 3: Write the failing router tests**

`tests/StraightWire.Core.Tests/RouterTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class RouterTests
    {
        static Pt P(double x, double y) => new Pt(x, y);

        public static IEnumerable<object[]> RandomScenes() =>
            from seed in new[] { 1, 2, 3 }
            from style in new[] { WireStyle.Orthogonal, WireStyle.Diagonal }
            select new object[] { seed, style };

        static void AssertSane(Scene scene, RouteResult result)
        {
            foreach (var w in scene.Wires)
            {
                if (w.Style == WireStyle.Curve)
                {
                    Assert.False(result.Paths.ContainsKey(w.Id));
                    continue;
                }
                var p = result.Paths[w.Id];
                Assert.True(p.Length >= 2, w.Id + " has " + p.Length + " points");
                Assert.Equal(w.Start, p[0]);
                Assert.Equal(w.End, p[p.Length - 1]);
                Assert.All(p, q => Assert.True(double.IsFinite(q.X) && double.IsFinite(q.Y), w.Id + " has " + q));
            }
        }

        [Theory, MemberData(nameof(RandomScenes))]
        public void Every_path_starts_and_ends_exactly_on_its_ports(int seed, WireStyle style)
        {
            var scene = TestScenes.Random(seed, 6, 5, 60, style);
            AssertSane(scene, Router.Route(scene, new RouterState()));
        }

        [Theory, MemberData(nameof(RandomScenes))]
        public void Segments_keep_to_the_style_angles(int seed, WireStyle style)
        {
            var scene = TestScenes.Random(seed, 6, 5, 60, style);
            foreach (var path in Router.Route(scene, new RouterState()).Paths.Values)
                for (int i = 0; i < path.Length - 1; i++)
                {
                    double dx = Math.Abs(path[i + 1].X - path[i].X), dy = Math.Abs(path[i + 1].Y - path[i].Y);
                    bool flat = dy < Routing.StraightBelow;   // a straight wire may lean by under half a unit
                    bool upright = dx < 1e-9;
                    bool diagonal = Math.Abs(dx - dy) < 0.02;
                    bool ok = style == WireStyle.Orthogonal ? flat || upright : flat || upright || diagonal;
                    Assert.True(ok, $"segment {path[i]} -> {path[i + 1]}");
                }
        }

        [Fact]
        public void Overlapping_lanes_keep_the_spacing()
        {
            var b = new SceneBuilder();
            for (int i = 0; i < 4; i++) b.Node("S" + i, -80, 40 * i - 20).Node("T" + i, 400, 180 + 40 * i);
            for (int i = 0; i < 4; i++) b.Wire("S" + i, "T" + i, WireStyle.Orthogonal);
            var lanes = Router.Route(b.Build(), new RouterState()).Paths.Values.Select(p => p[1].X).OrderBy(x => x).ToArray();
            for (int i = 1; i < lanes.Length; i++)
                Assert.True(lanes[i] - lanes[i - 1] >= 10 - 0.01, string.Join(", ", lanes));
        }

        [Fact]
        public void Lanes_avoid_a_node_when_there_is_room()
        {
            var b = new SceneBuilder().Node("S", -80, 80).Node("T", 400, 280).Node("O", 180, 150, 40, 100);
            string id = b.Wire("S", "T", WireStyle.Orthogonal);
            var path = Router.Route(b.Build(), new RouterState()).Paths[id];
            var keepOut = new Rect(180, 150, 220, 250).Inflate(Routing.Clearance - 0.01);
            for (int i = 0; i < path.Length - 1; i++)
                Assert.False(Geo.SegRectHit(path[i], path[i + 1], keepOut), $"segment {path[i]} -> {path[i + 1]}");
        }

        [Fact]
        public void An_unobstructed_45_degree_wire_bends_twice()
        {
            var b = new SceneBuilder().Node("S", -80, -20).Node("T", 200, 80);
            string id = b.Wire("S", "T", WireStyle.Diagonal);
            Assert.Equal(new[] { P(0, 0), P(50, 0), P(150, 100), P(200, 100) }, Router.Route(b.Build(), null).Paths[id]);
        }

        [Fact]
        public void A_backwards_wire_crosses_between_its_nodes()
        {
            var b = new SceneBuilder().Node("S", 300, 200, 80, 60).Node("T", 100, 0, 80, 60);
            string id = b.Wire("S", "T", P(380, 230), P(100, 30), WireStyle.Orthogonal);
            Assert.Equal(new[] { P(380, 230), P(398, 230), P(398, 130), P(82, 130), P(82, 30), P(100, 30) },
                Router.Route(b.Build(), null).Paths[id]);
        }

        [Fact]
        public void A_tall_gap_takes_a_single_45_degree_crossing()
        {
            var b = new SceneBuilder().Node("S", 140, 400, 80, 60).Node("T", 100, 0, 80, 60);
            string id = b.Wire("S", "T", P(220, 430), P(100, 30), WireStyle.Diagonal);
            var path = Router.Route(b.Build(), null).Paths[id];
            Assert.Contains((P(238, 308), P(82, 152)), path.Zip(path.Skip(1), (p, q) => (p, q)));
        }

        [Fact]
        public void Same_source_wires_share_a_trunk_with_a_dot_where_they_split()
        {
            var b = new SceneBuilder().Node("S", -80, 80).Node("T1", 400, 280).Node("T2", 300, -120);
            string down = b.Wire("S", "T1", WireStyle.Orthogonal);
            string up = b.Wire("S", "T2", WireStyle.Orthogonal);
            var r = Router.Route(b.Build(), new RouterState());
            Assert.Equal(r.Paths[down][1].X, r.Paths[up][1].X, 2);
            var dot = Assert.Single(r.Junctions);
            Assert.Equal("S:0", dot.Group);
            Assert.Equal(175, dot.At.X, 2);
            Assert.InRange(dot.At.Y, 100, 101);
        }

        [Fact]
        public void The_same_canvas_in_any_order_routes_the_same()
        {
            var scene = TestScenes.Random(4, 6, 5, 60, WireStyle.Diagonal);
            var rng = new Random(99);
            var shuffled = new Scene(scene.Nodes.OrderBy(_ => rng.Next()).ToList(),
                                     scene.Wires.OrderBy(_ => rng.Next()).ToList(), scene.Spacing);
            var a = Router.Route(scene, new RouterState());
            var b = Router.Route(shuffled, new RouterState());
            Assert.Equal(a.Paths.Keys.OrderBy(k => k, StringComparer.Ordinal), b.Paths.Keys.OrderBy(k => k, StringComparer.Ordinal));
            foreach (var k in a.Paths.Keys) Assert.Equal(a.Paths[k], b.Paths[k]);
            Assert.Equal(a.Junctions.Select(j => (j.Group, j.At)), b.Junctions.Select(j => (j.Group, j.At)));
        }

        [Fact]
        public void A_lane_does_not_jump_when_a_node_moves_by_one()
        {
            Scene Corridor(double obstacleX)
            {
                var b = new SceneBuilder().Node("S", -80, 80).Node("T", 400, 280).Node("O", obstacleX, 150, 40, 100);
                b.Wire("S", "T", WireStyle.Orthogonal);
                return b.Build();
            }
            var state = new RouterState();
            double before = Router.Route(Corridor(181), state).Paths["w000"][1].X;   // left stretch is wider
            double after = Router.Route(Corridor(180), state).Paths["w000"][1].X;    // now a tie
            double fresh = Router.Route(Corridor(180), new RouterState()).Paths["w000"][1].X;
            Assert.True(before < 173, $"before {before}");
            Assert.True(after < 172, $"after {after}");
            Assert.True(fresh > 228, $"fresh {fresh}");
        }

        [Fact]
        public void Curve_wires_get_no_path_and_take_no_lane()
        {
            var b = new SceneBuilder().Node("S", -80, 80).Node("T", 400, 280);
            string curve = b.Wire("S", "T", WireStyle.Curve, "c:0");
            string ortho = b.Wire("S", "T", P(0, 110), P(400, 310), WireStyle.Orthogonal, "o:0");
            var state = new RouterState();
            var r = Router.Route(b.Build(), state);
            Assert.False(r.Paths.ContainsKey(curve));
            Assert.Equal(200, r.Paths[ortho][1].X, 6);   // alone: the middle of 18..382
            Assert.Equal(1, state.Count);
        }

        // Review focus 1: components dropped on top of each other, ports inside other nodes.
        [Theory]
        [InlineData(WireStyle.Orthogonal)]
        [InlineData(WireStyle.Diagonal)]
        public void Survives_overlapping_nodes_and_ports_inside_nodes(WireStyle style)
        {
            var b = new SceneBuilder().Node("S", -80, -20).Node("T", 200, 80)
                                      .Node("Big", -40, -60, 320, 220).Node("T2", 210, 90);
            b.Wire("S", "T", style);
            b.Wire("S", "T2", style);
            b.Wire("T", "S", style);   // backwards, both ends inside Big
            var scene = b.Build();
            AssertSane(scene, Router.Route(scene, new RouterState()));
        }

        // Review focus 2: an output wired back into its own component.
        [Fact]
        public void Routes_a_self_loop_around_its_node()
        {
            var b = new SceneBuilder().Node("N", 0, 0);
            string id = b.Wire("N", "N", P(80, 20), P(0, 20), WireStyle.Orthogonal);
            Assert.Equal(new[] { P(80, 20), P(98, 20), P(98, -24), P(-18, -24), P(-18, 20), P(0, 20) },
                Router.Route(b.Build(), null).Paths[id]);
        }

        // Review focus 2: both grips on the same point.
        [Theory]
        [InlineData(WireStyle.Orthogonal)]
        [InlineData(WireStyle.Diagonal)]
        public void Survives_a_zero_length_wire(WireStyle style)
        {
            var b = new SceneBuilder().Node("N", 0, 0);
            b.Wire("N", "N", P(50, 50), P(50, 50), style);
            var scene = b.Build();
            AssertSane(scene, Router.Route(scene, null));
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they fail**

Run: `dotnet test tests/StraightWire.Core.Tests --nologo -v q`
Expected: the build fails with `CS0103` for `Junctions` and `Router`.

- [ ] **Step 5: Write the junctions**

`src/StraightWire.Core/Junctions.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace StraightWire.Core
{
    /// <summary>
    /// Dots where wires from the same source part ways [diverge, junctions]. Without them a
    /// shared trunk reads as one wire.
    /// </summary>
    internal static class Junctions
    {
        const double SplitDistance = 0.6, PortGap = 3, MergeDistance = 1.5;
        const int BisectSteps = 14;

        /// <summary>Where <paramref name="a"/> first leaves <paramref name="b"/>, refined by bisection; null if it never does.</summary>
        public static Pt? Diverge(IReadOnlyList<Pt> a, IReadOnlyList<Pt> b)
        {
            for (int i = 1; i < a.Count; i++)
            {
                if (Geo.PolyDist(a[i], b) <= SplitDistance) continue;
                Pt p = a[i - 1], q = a[i];
                double lo = 0, hi = 1;
                for (int k = 0; k < BisectSteps; k++)
                {
                    double m = (lo + hi) / 2;
                    if (Geo.PolyDist(Lerp(p, q, m), b) <= SplitDistance) lo = m;
                    else hi = m;
                }
                return Lerp(p, q, lo);
            }
            return null;
        }

        static Pt Lerp(Pt p, Pt q, double t) => new Pt(p.X + (q.X - p.X) * t, p.Y + (q.Y - p.Y) * t);

        /// <summary>
        /// Dots for every same-source group, groups in order of first appearance. A group with any
        /// unrouted (Curve) wire gets none: a dot where a curve leaves would float in mid-air.
        /// </summary>
        public static List<Junction> Find(IReadOnlyList<(string Group, bool Routed, Pt[] Path)> wires)
        {
            var order = new List<string>();
            var groups = new Dictionary<string, List<(bool Routed, Pt[] Path)>>(StringComparer.Ordinal);
            foreach (var (group, routed, path) in wires)
            {
                if (!groups.TryGetValue(group, out var list))
                {
                    groups[group] = list = new List<(bool Routed, Pt[] Path)>();
                    order.Add(group);
                }
                list.Add((routed, path));
            }
            var dots = new List<Junction>();
            foreach (string group in order)
            {
                var list = groups[group];
                if (list.Count < 2 || list.Exists(e => !e.Routed)) continue;
                for (int i = 0; i < list.Count; i++)
                    for (int j = i + 1; j < list.Count; j++)
                    {
                        var split = Diverge(list[i].Path, list[j].Path);
                        if (split == null) continue;
                        var p = split.Value;
                        if (Pt.Dist(p, list[i].Path[0]) < PortGap) continue;
                        if (dots.Exists(d => Pt.Dist(d.At, p) < MergeDistance)) continue;
                        dots.Add(new Junction(group, p));
                    }
            }
            return dots;
        }
    }
}
```

- [ ] **Step 6: Write the router**

`src/StraightWire.Core/Router.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace StraightWire.Core
{
    /// <summary>
    /// Routes every wire of a scene: backwards wires through gaps, lane wires into spaced lanes,
    /// 45° wires into single diagonals where that touches nothing new, then junction dots.
    /// </summary>
    public static class Router
    {
        /// <summary>
        /// Routes <paramref name="scene"/>. <paramref name="state"/> carries lanes from the previous
        /// call (pass the same instance while the user drags) and is updated; null starts fresh.
        /// The same scene and state always give the same result.
        /// </summary>
        public static RouteResult Route(Scene scene, RouterState state)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            state = state ?? new RouterState();
            var ctx = new RouteContext(scene, state);
            BackWires.Prepare(ctx);
            var lanes = Lanes.Prepare(ctx);
            Lanes.Relax(lanes, ctx.Sp);
            Diagonals.Decide(ctx);

            var paths = new Dictionary<string, Pt[]>(StringComparer.Ordinal);
            var forJunctions = new List<(string Group, bool Routed, Pt[] Path)>(ctx.Jobs.Count);
            foreach (var w in ctx.Jobs)
            {
                Pt[] path = w.Routed ? WirePath.For(w).ToArray() : null;
                if (path != null) paths[w.Id] = path;
                forJunctions.Add((w.Group, w.Routed, path));
            }
            state.Replace(lanes.Select(w => new KeyValuePair<string, double>(w.Id, w.Lane)));
            return new RouteResult(paths, Junctions.Find(forJunctions));
        }
    }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/StraightWire.Core.Tests --nologo -v q`
Expected: `Passed!  - Failed: 0, Passed: 96`.

If `Overlapping_lanes_keep_the_spacing` or `A_lane_does_not_jump_when_a_node_moves_by_one` fails, compare `Lanes.Feasible` and `Lanes.Relax` line by line with the prototype's `feasible()` and `relax()`. The expected numbers in these tests were derived by hand from the prototype's rules; do not change them to match the output.

- [ ] **Step 8: Commit**

```bash
git add src/StraightWire.Core tests/StraightWire.Core.Tests
git commit -F - <<'EOF'
Add junction dots and the router, with routing invariants

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GKJGcqmxvBcFYsUcoRsYRS
EOF
```

---

### Task 8: Routing performance budget

**Files:**
- Test: `tests/StraightWire.Core.Tests/RouterPerformanceTests.cs`

**Interfaces:**
- Consumes: `Router.Route` and `TestScenes.Random` (Task 7), plus the stage classes (Tasks 3–7) for the timing breakdown.
- Produces: nothing new. This task pins the spec's performance target: 500 nodes and 800 wires route in under 10 ms (median, Release).

- [ ] **Step 1: Write the performance tests**

`tests/StraightWire.Core.Tests/RouterPerformanceTests.cs`:

```csharp
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using StraightWire.Core;
using Xunit;
using Xunit.Abstractions;

namespace StraightWire.Core.Tests
{
    public class RouterPerformanceTests
    {
#if DEBUG
        // Debug builds run unoptimised; the spec's 10 ms is measured with -c Release.
        const double SceneBudgetMs = 30, FanOutBudgetMs = 15;
#else
        const double SceneBudgetMs = 10, FanOutBudgetMs = 5;
#endif

        readonly ITestOutputHelper _out;

        public RouterPerformanceTests(ITestOutputHelper output)
        {
            _out = output;
        }

        static double MedianMs(int runs, System.Action action)
        {
            for (int i = 0; i < 5; i++) action();
            var times = new List<double>();
            var watch = new Stopwatch();
            for (int i = 0; i < runs; i++)
            {
                watch.Restart();
                action();
                times.Add(watch.Elapsed.TotalMilliseconds);
            }
            times.Sort();
            return times[times.Count / 2];
        }

        [Fact]
        public void Routes_500_nodes_and_800_wires_within_budget()
        {
            var scene = TestScenes.Random(7, 25, 20, 800, WireStyle.Diagonal);
            var state = new RouterState();
            double median = MedianMs(21, () => Router.Route(scene, state));
            _out.WriteLine($"500 nodes / 800 wires, 45°: median {median:0.00} ms (budget {SceneBudgetMs} ms)");
            Assert.True(median < SceneBudgetMs, $"median {median:0.00} ms is over the {SceneBudgetMs} ms budget");
        }

        [Fact]
        public void A_wide_fan_out_routes_within_budget()
        {
            var b = new SceneBuilder().Node("S", -80, 600);
            for (int i = 0; i < 60; i++) b.Node("T" + i, 400, i * 40, 80, 30);
            for (int i = 0; i < 60; i++) b.Wire("S", "T" + i, WireStyle.Diagonal);
            var scene = b.Build();
            var state = new RouterState();
            double median = MedianMs(21, () => Router.Route(scene, state));
            _out.WriteLine($"one source to 60 targets: median {median:0.00} ms (budget {FanOutBudgetMs} ms)");
            Assert.True(median < FanOutBudgetMs, $"median {median:0.00} ms is over the {FanOutBudgetMs} ms budget");
        }

        /// <summary>Never fails; prints where the time goes, for when a budget test does.</summary>
        [Fact]
        public void Prints_stage_timings()
        {
            var scene = TestScenes.Random(7, 25, 20, 800, WireStyle.Diagonal);
            var state = new RouterState();
            Router.Route(scene, state);
            var stages = new Dictionary<string, double>();
            for (int run = 0; run < 11; run++)
            {
                var w = Stopwatch.StartNew();
                var ctx = new RouteContext(scene, state); Add(stages, "context", w);
                BackWires.Prepare(ctx); Add(stages, "backwards", w);
                var lanes = Lanes.Prepare(ctx); Add(stages, "feasible", w);
                Lanes.Relax(lanes, ctx.Sp); Add(stages, "relax", w);
                Diagonals.Decide(ctx); Add(stages, "diagonals", w);
                var forJunctions = ctx.Jobs.Select(j => (j.Group, j.Routed, j.Routed ? WirePath.For(j).ToArray() : null)).ToList();
                Add(stages, "paths", w);
                Junctions.Find(forJunctions); Add(stages, "junctions", w);
            }
            foreach (var kv in stages) _out.WriteLine($"{kv.Key,-10} {kv.Value / 11:0.000} ms");
        }

        static void Add(Dictionary<string, double> stages, string name, Stopwatch w)
        {
            stages.TryGetValue(name, out double t);
            stages[name] = t + w.Elapsed.TotalMilliseconds;
            w.Restart();
        }
    }
}
```

- [ ] **Step 2: Run them in Release and read the numbers**

Run: `dotnet test tests/StraightWire.Core.Tests -c Release --nologo -v n --filter "FullyQualifiedName~RouterPerformanceTests" --logger "console;verbosity=detailed"`
Expected: 3 passed. The output shows the two medians under 10 ms and 5 ms, plus the stage table.

If a budget test fails:
- Do not raise the budget or skip the test.
- Use the stage table to find the slow stage, and check it for accidental quadratic work: for example a `List.Contains` inside a loop, a LINQ query re-run per wire, or a grid query with a needlessly large rectangle.
- If the stage is doing what the prototype does and is still over budget, stop and report the stage table to the controller. A missed performance target is the user's call (accept, or change approach), not the implementer's.

- [ ] **Step 3: Run the whole suite in Debug**

Run: `dotnet test tests/StraightWire.Core.Tests --nologo -v q`
Expected: `Passed!  - Failed: 0, Passed: 99`.

- [ ] **Step 4: Commit**

```bash
git add tests/StraightWire.Core.Tests/RouterPerformanceTests.cs
git commit -F - <<'EOF'
Pin the routing performance budget

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GKJGcqmxvBcFYsUcoRsYRS
EOF
```

---

### Task 9: File settings and their JSON

**Files:**
- Create: `src/StraightWire.Core/FileSettings.cs`
- Test: `tests/StraightWire.Core.Tests/FileSettingsTests.cs`

**Interfaces:**
- Consumes: `WireStyle` (Task 3) and `Routing` (Task 1).
- Produces:
  - `public sealed class FileSettings(WireStyle mode, double spacing, IReadOnlyDictionary<string, WireStyle> overrides = null)`:
    - `Mode`, `Spacing` (clamped) and `Overrides`
    - `StyleOf(string wireId)`
    - `WithMode`, `WithSpacing`, `WithOverride(IEnumerable<string>, WireStyle)` and `WithoutOverride(IEnumerable<string>)`
    - `Pruned(ICollection<string> liveWireIds)`

    Every `With…`/`Pruned` call returns a new instance.
  - `public enum SettingsStatus { Missing, Ok, Broken, TooNew }`.
  - `public static class SettingsJson`:
    - `Key = "StraightWire.v1"` and `Version = 1`
    - `double ClampSpacing(double)`
    - `string Serialize(FileSettings)`
    - `SettingsStatus Parse(string json, out FileSettings settings)`, where `settings` is non-null only when Ok
  - `public static class StyleNames`: `ToToken`, `TryParseToken`, `Label`, `ShortLabel`, `Summary(FileSettings)`.

- [ ] **Step 1: Write the failing tests**

`tests/StraightWire.Core.Tests/FileSettingsTests.cs`:

```csharp
using System.Collections.Generic;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class FileSettingsTests
    {
        static FileSettings Sample() => new FileSettings(WireStyle.Orthogonal, 12,
            new Dictionary<string, WireStyle> { ["b>c"] = WireStyle.Curve, ["a>b"] = WireStyle.Diagonal });

        [Fact]
        public void Serialize_then_parse_round_trips()
        {
            Assert.Equal(SettingsStatus.Ok, SettingsJson.Parse(SettingsJson.Serialize(Sample()), out var back));
            Assert.Equal(WireStyle.Orthogonal, back.Mode);
            Assert.Equal(12, back.Spacing);
            Assert.Equal(2, back.Overrides.Count);
            Assert.Equal(WireStyle.Curve, back.StyleOf("b>c"));
            Assert.Equal(WireStyle.Diagonal, back.StyleOf("a>b"));
        }

        [Fact]
        public void Serialize_writes_the_spec_format_with_overrides_sorted() =>
            Assert.Equal("{\"v\":1,\"mode\":\"ortho\",\"spacing\":12,\"wires\":{\"a>b\":\"diag\",\"b>c\":\"bezier\"}}",
                SettingsJson.Serialize(Sample()));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Missing_when_the_file_has_no_settings(string json)
        {
            Assert.Equal(SettingsStatus.Missing, SettingsJson.Parse(json, out var s));
            Assert.Null(s);
        }

        // Review focus 3: settings this plugin cannot read.
        [Theory]
        [InlineData("{broken")]
        [InlineData("[]")]
        [InlineData("{\"mode\":\"ortho\"}")]
        [InlineData("{\"v\":1,\"mode\":\"zigzag\"}")]
        [InlineData("{\"v\":1,\"mode\":\"ortho\",\"spacing\":\"wide\"}")]
        [InlineData("{\"v\":0,\"mode\":\"ortho\"}")]
        public void Broken_for_text_that_is_not_our_settings(string json)
        {
            Assert.Equal(SettingsStatus.Broken, SettingsJson.Parse(json, out var s));
            Assert.Null(s);
        }

        // Review focus 3: written by a newer plugin. Never parsed further, never overwritten.
        [Fact]
        public void TooNew_for_a_newer_version_whatever_else_it_says()
        {
            Assert.Equal(SettingsStatus.TooNew, SettingsJson.Parse("{\"v\":2,\"mode\":\"spiral\"}", out var s));
            Assert.Null(s);
        }

        [Fact]
        public void Overrides_with_unknown_styles_are_dropped()
        {
            SettingsJson.Parse("{\"v\":1,\"mode\":\"diag\",\"spacing\":10,\"wires\":{\"a>b\":\"zigzag\",\"c>d\":\"ortho\"}}", out var s);
            Assert.Equal(new[] { "c>d" }, s.Overrides.Keys);
        }

        [Theory]
        [InlineData(50, 20)]
        [InlineData(2, 6)]
        [InlineData(12.5, 12.5)]
        public void Spacing_is_clamped_to_the_supported_range(double written, double read)
        {
            SettingsJson.Parse("{\"v\":1,\"mode\":\"diag\",\"spacing\":" + written.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}", out var s);
            Assert.Equal(read, s.Spacing);
        }

        [Fact]
        public void StyleOf_prefers_the_wire_override()
        {
            Assert.Equal(WireStyle.Curve, Sample().StyleOf("b>c"));
            Assert.Equal(WireStyle.Orthogonal, Sample().StyleOf("x>y"));
        }

        [Fact]
        public void Changes_return_new_settings_and_leave_the_original_alone()
        {
            var s = Sample();
            var t = s.WithOverride(new[] { "x>y" }, WireStyle.Curve).WithoutOverride(new[] { "a>b" }).WithMode(WireStyle.Diagonal).WithSpacing(16);
            Assert.Equal(WireStyle.Curve, t.StyleOf("x>y"));
            Assert.Equal(WireStyle.Diagonal, t.StyleOf("a>b"));
            Assert.Equal(16, t.Spacing);
            Assert.Equal(2, s.Overrides.Count);
            Assert.False(s.Overrides.ContainsKey("x>y"));
            Assert.Equal(WireStyle.Orthogonal, s.Mode);
        }

        // Review focus 3: overrides for wires that were deleted.
        [Fact]
        public void Pruned_drops_overrides_for_wires_that_are_gone() =>
            Assert.Equal(new[] { "a>b" }, Sample().Pruned(new HashSet<string> { "a>b", "q>r" }).Overrides.Keys);

        [Fact]
        public void Summary_reads_like_the_marker_message()
        {
            Assert.Equal("45° · 10", StyleNames.Summary(new FileSettings(WireStyle.Diagonal, 10)));
            Assert.Equal("Orthogonal · 12.5", StyleNames.Summary(new FileSettings(WireStyle.Orthogonal, 12.5)));
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/StraightWire.Core.Tests --nologo -v q --filter "FullyQualifiedName~FileSettingsTests"`
Expected: the build fails with `CS0246` for `FileSettings`, `SettingsJson` and `SettingsStatus`.

- [ ] **Step 3: Write the settings model**

`src/StraightWire.Core/FileSettings.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace StraightWire.Core
{
    /// <summary>A file's wire settings: its global style, its spacing, and per-wire overrides by wire id. Immutable.</summary>
    public sealed class FileSettings
    {
        static readonly IReadOnlyDictionary<string, WireStyle> None = new Dictionary<string, WireStyle>(StringComparer.Ordinal);

        public FileSettings(WireStyle mode, double spacing, IReadOnlyDictionary<string, WireStyle> overrides = null)
        {
            Mode = mode;
            Spacing = SettingsJson.ClampSpacing(spacing);
            Overrides = overrides == null || overrides.Count == 0
                ? None
                : new Dictionary<string, WireStyle>(overrides, StringComparer.Ordinal);
        }

        public WireStyle Mode { get; }
        public double Spacing { get; }
        public IReadOnlyDictionary<string, WireStyle> Overrides { get; }

        public WireStyle StyleOf(string wireId) => Overrides.TryGetValue(wireId, out var s) ? s : Mode;

        public FileSettings WithMode(WireStyle mode) => new FileSettings(mode, Spacing, Overrides);

        public FileSettings WithSpacing(double spacing) => new FileSettings(Mode, spacing, Overrides);

        public FileSettings WithOverride(IEnumerable<string> wireIds, WireStyle style)
        {
            var d = Copy();
            foreach (string id in wireIds) d[id] = style;
            return new FileSettings(Mode, Spacing, d);
        }

        public FileSettings WithoutOverride(IEnumerable<string> wireIds)
        {
            var d = Copy();
            foreach (string id in wireIds) d.Remove(id);
            return new FileSettings(Mode, Spacing, d);
        }

        /// <summary>Drops overrides whose wire no longer exists, so files do not collect dead ids.</summary>
        public FileSettings Pruned(ICollection<string> liveWireIds)
        {
            var d = Copy();
            foreach (string id in Overrides.Keys)
                if (!liveWireIds.Contains(id)) d.Remove(id);
            return new FileSettings(Mode, Spacing, d);
        }

        Dictionary<string, WireStyle> Copy() => new Dictionary<string, WireStyle>(Overrides, StringComparer.Ordinal);
    }

    public enum SettingsStatus { Missing, Ok, Broken, TooNew }

    /// <summary>The settings as stored in GH_Document.ValueTable under <see cref="Key"/>.</summary>
    public static class SettingsJson
    {
        public const string Key = "StraightWire.v1";
        public const int Version = 1;

        public static double ClampSpacing(double s) =>
            double.IsNaN(s) ? Routing.DefaultSpacing
            : s < Routing.MinSpacing ? Routing.MinSpacing
            : s > Routing.MaxSpacing ? Routing.MaxSpacing
            : s;

        public static string Serialize(FileSettings s)
        {
            using (var stream = new MemoryStream())
            {
                // Relaxed escaping keeps wire ids readable: "a>b", not "a>b".
                using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
                {
                    w.WriteStartObject();
                    w.WriteNumber("v", Version);
                    w.WriteString("mode", StyleNames.ToToken(s.Mode));
                    w.WriteNumber("spacing", s.Spacing);
                    w.WriteStartObject("wires");
                    foreach (var kv in s.Overrides.OrderBy(k => k.Key, StringComparer.Ordinal))
                        w.WriteString(kv.Key, StyleNames.ToToken(kv.Value));
                    w.WriteEndObject();
                    w.WriteEndObject();
                }
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        /// <summary>
        /// Reads stored settings. Missing: nothing stored. Broken: not readable as version-1
        /// settings. TooNew: written by a newer plugin, which must not be overwritten. Unknown
        /// style names inside "wires" are skipped rather than failing the whole file.
        /// </summary>
        public static SettingsStatus Parse(string json, out FileSettings settings)
        {
            settings = null;
            if (string.IsNullOrWhiteSpace(json)) return SettingsStatus.Missing;
            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object) return SettingsStatus.Broken;
                    if (!root.TryGetProperty("v", out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out int version))
                        return SettingsStatus.Broken;
                    if (version > Version) return SettingsStatus.TooNew;
                    if (version < 1) return SettingsStatus.Broken;
                    if (!root.TryGetProperty("mode", out var m) || m.ValueKind != JsonValueKind.String ||
                        !StyleNames.TryParseToken(m.GetString(), out var mode))
                        return SettingsStatus.Broken;
                    double spacing = Routing.DefaultSpacing;
                    if (root.TryGetProperty("spacing", out var sp))
                    {
                        if (sp.ValueKind != JsonValueKind.Number) return SettingsStatus.Broken;
                        spacing = sp.GetDouble();
                    }
                    var overrides = new Dictionary<string, WireStyle>(StringComparer.Ordinal);
                    if (root.TryGetProperty("wires", out var wires) && wires.ValueKind == JsonValueKind.Object)
                        foreach (var p in wires.EnumerateObject())
                            if (p.Value.ValueKind == JsonValueKind.String && StyleNames.TryParseToken(p.Value.GetString(), out var style))
                                overrides[p.Name] = style;
                    settings = new FileSettings(mode, spacing, overrides);
                    return SettingsStatus.Ok;
                }
            }
            catch (JsonException)
            {
                return SettingsStatus.Broken;
            }
        }
    }

    public static class StyleNames
    {
        public static string ToToken(WireStyle s) =>
            s == WireStyle.Curve ? "bezier" : s == WireStyle.Orthogonal ? "ortho" : "diag";

        public static bool TryParseToken(string token, out WireStyle style)
        {
            switch (token)
            {
                case "bezier": style = WireStyle.Curve; return true;
                case "ortho": style = WireStyle.Orthogonal; return true;
                case "diag": style = WireStyle.Diagonal; return true;
                default: style = WireStyle.Diagonal; return false;
            }
        }

        public static string Label(WireStyle s) =>
            s == WireStyle.Curve ? "Curve" : s == WireStyle.Orthogonal ? "Orthogonal" : "45°";

        /// <summary>The canvas widget's narrower button text.</summary>
        public static string ShortLabel(WireStyle s) => s == WireStyle.Orthogonal ? "Ortho" : Label(s);

        /// <summary>The marker component's message, e.g. "45° · 10".</summary>
        public static string Summary(FileSettings s) =>
            Label(s.Mode) + " · " + s.Spacing.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/StraightWire.Core.Tests --nologo -v q`
Expected: `Passed!  - Failed: 0, Passed: 119`.

- [ ] **Step 5: Commit**

```bash
git add src/StraightWire.Core/FileSettings.cs tests/StraightWire.Core.Tests/FileSettingsTests.cs
git commit -F - <<'EOF'
Add file settings and their JSON format

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GKJGcqmxvBcFYsUcoRsYRS
EOF
```

---

### Task 10: Plugin scaffold and the hot-reload loop

**Files:**
- Create: `src/StraightWire/StraightWire.csproj`
- Create: `src/StraightWire/Log.cs`
- Create: `src/StraightWire/StraightWireInfo.cs`
- Create: `src/StraightWire/Interop/NativeWires.cs`
- Create: `src/StraightWire/Plugin.cs`
- Create: `src/StraightWire/StraightWirePriority.cs`
- Create: `src/StraightWire/DevHost.cs`
- Create: `tools/dev-reload.csx`
- Create: `tools/dev-call.csx`

**Interfaces:**
- Consumes: the Core sources (compiled in).
- Produces:
  - `internal static class Log`: `Info(string)`, `Once(string key, string message)`, `Error(string key, Exception)`, `Guard(string key, Action)`.
  - `internal static class NativeWires`: `bool Available`, `Pen Pen(GH_Painter, PointF a, PointF b, bool asel, bool bsel, GH_WireType)`, `bool IsShared(GH_WireType)`, `void WarmFaintPen(GH_Painter)`.
  - `internal static class Plugin`: `Start()` and `Stop()`, both idempotent.
  - `public static class DevHost`: `string Attach()`, `string Detach()`.
  - `tools/dev-reload.csx` loads `src/StraightWire/bin/Debug/StraightWire.gha` into a collectible `AssemblyLoadContext` stored in AppDomain data slot `"StraightWire.DevContext"`.
  - `tools/dev-call.csx` invokes a `DevHost` method by name on whichever build is loaded.

- [ ] **Step 1: Create the plugin project**

`src/StraightWire/StraightWire.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net7.0</TargetFramework>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <TargetExt>.gha</TargetExt>
    <!-- CA1416: System.Drawing annotates itself as macOS 13+; Rhino 8 requires newer than that anyway. -->
    <!-- MSB3277: the Grasshopper package and Rhino ship different System.Windows.Forms
         references. Rhino's wins by design (Private=false) - it is the one loaded at runtime. -->
    <NoWarn>NU1701;NU1603;NETSDK1138;CS8632;CA1416;MSB3277</NoWarn>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <GenerateAssemblyInfo>true</GenerateAssemblyInfo>
    <Version>0.1.0</Version>
    <!-- Compile against the oldest Rhino 8 we support; this sets the Yak version floor. -->
    <GrasshopperVersion Condition="'$(GrasshopperVersion)' == ''">8.0.23304.9001</GrasshopperVersion>
    <Company>Straight Wire</Company>
    <Description>Draws Grasshopper wires as straight, circuit-board style traces.</Description>
  </PropertyGroup>

  <!--
    Grasshopper is a WinForms application. .NET 7 has no cross-platform WinForms, so we compile
    against the System.Windows.Forms that Rhino itself ships and loads at runtime (Private=false:
    never copy it, always use Rhino's). Override with -p:RhinoSystemDir=... if Rhino lives elsewhere.
  -->
  <PropertyGroup Condition="'$(RhinoSystemDir)' == '' AND $([MSBuild]::IsOSPlatform('OSX'))">
    <RhinoSystemDir>/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Versions/A/Resources</RhinoSystemDir>
  </PropertyGroup>
  <PropertyGroup Condition="'$(RhinoSystemDir)' == '' AND $([MSBuild]::IsOSPlatform('Windows'))">
    <RhinoSystemDir>$(ProgramW6432)\Rhino 8\System</RhinoSystemDir>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="System.Windows.Forms">
      <HintPath>$(RhinoSystemDir)/System.Windows.Forms.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="System.Windows.Forms.Primitives">
      <HintPath>$(RhinoSystemDir)/System.Windows.Forms.Primitives.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="System.Drawing.Common">
      <HintPath>$(RhinoSystemDir)/System.Drawing.Common.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Grasshopper" Version="$(GrasshopperVersion)" ExcludeAssets="runtime" />
  </ItemGroup>

  <!-- The routing core is compiled in, so the plugin installs as one .gha. -->
  <ItemGroup>
    <Compile Include="../StraightWire.Core/**/*.cs"
             Exclude="../StraightWire.Core/bin/**;../StraightWire.Core/obj/**"
             LinkBase="Core" />
  </ItemGroup>

  <Target Name="CheckRhino" BeforeTargets="BeforeBuild">
    <Error Condition="!Exists('$(RhinoSystemDir)/System.Windows.Forms.dll')"
           Text="Rhino 8 not found at '$(RhinoSystemDir)'. Pass -p:RhinoSystemDir=&lt;path to Rhino's System folder&gt;." />
  </Target>

</Project>
```

- [ ] **Step 2: Write the log**

`src/StraightWire/Log.cs`:

```csharp
using System;
using System.Collections.Generic;
using Rhino;

namespace StraightWire
{
    /// <summary>
    /// Nothing here may throw inside Grasshopper's paint or message loop, so failures are caught
    /// and reported once on Rhino's command line. A repeated failure goes quiet.
    /// </summary>
    internal static class Log
    {
        static readonly HashSet<string> Reported = new HashSet<string>();

        public static void Info(string message)
        {
            try { RhinoApp.WriteLine("Straight Wire: " + message); }
            catch { }
        }

        public static void Once(string key, string message)
        {
            lock (Reported)
                if (!Reported.Add(key)) return;
            Info(message);
        }

        public static void Error(string key, Exception ex) => Once(key, key + " failed: " + ex.Message);

        /// <summary>Runs an action, swallowing and reporting anything it throws.</summary>
        public static void Guard(string key, Action action)
        {
            try { action(); }
            catch (Exception ex) { Error(key, ex); }
        }
    }
}
```

- [ ] **Step 3: Write the assembly info**

`src/StraightWire/StraightWireInfo.cs`:

```csharp
using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace StraightWire
{
    public class StraightWireInfo : GH_AssemblyInfo
    {
        public override string Name => "Straight Wire";
        public override Bitmap Icon => null;
        public override string Description =>
            "Draws Grasshopper wires as straight, circuit-board style traces: right-angle or 45°, in their own lanes, around components.";

        /// <summary>The Package Manager finds the plugin for a file's missing components by this id.</summary>
        public override Guid Id => new Guid("6D1CB607-5D76-4B1D-AC6D-E9FA882192FE");

        public override string AuthorName => "Yolanda Xing";
        public override string AuthorContact => "https://github.com/YolandaXing210/straight-wire";

        /// <summary>
        /// Read off the assembly rather than written down here. Yak takes this as the package's
        /// content version, so a hard-coded number would ship contents that disagree with the manifest.
        /// </summary>
        public override string Version
        {
            get
            {
                var version = typeof(StraightWireInfo).Assembly.GetName().Version;
                return version == null ? "0.0.0" : version.Major + "." + version.Minor + "." + version.Build;
            }
        }
    }
}
```

- [ ] **Step 4: Write the native wire pens**

`src/StraightWire/Interop/NativeWires.cs`:

```csharp
using System;
using System.Drawing;
using System.Reflection;
using Grasshopper.GUI.Canvas;

namespace StraightWire.Interop
{
    /// <summary>
    /// Grasshopper's own wire pens. GH_Painter.GenerateWirePen is private, but it is the one place
    /// that knows every wire look: item, list (compound), tree (dashed compound), faint, the orange
    /// of empty data and the selection gradient, all from the user's skin colours. Drawing our
    /// polylines with it makes a routed wire look exactly like the native wire it replaces.
    ///
    /// If a future Grasshopper renames it, Available is false and the plugin stays out of the way.
    /// </summary>
    internal static class NativeWires
    {
        delegate Pen PenFactory(GH_Painter painter, PointF a, PointF b, bool asel, bool bsel, GH_WireType type);

        static readonly PenFactory Factory = Bind();

        public static bool Available => Factory != null;

        static PenFactory Bind()
        {
            try
            {
                var method = typeof(GH_Painter).GetMethod("GenerateWirePen",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null,
                    new[] { typeof(PointF), typeof(PointF), typeof(bool), typeof(bool), typeof(GH_WireType) }, null);
                if (method == null || method.ReturnType != typeof(Pen)) return null;
                return (PenFactory)Delegate.CreateDelegate(typeof(PenFactory), method);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// The pen Grasshopper would use for a wire from a to b. The faint pen is one shared
        /// instance Grasshopper keeps, so dispose a pen only when <see cref="IsShared"/> is false.
        /// </summary>
        public static Pen Pen(GH_Painter painter, PointF a, PointF b, bool asel, bool bsel, GH_WireType type) =>
            Factory?.Invoke(painter, a, b, asel, bsel, type);

        public static bool IsShared(GH_WireType type) => type == GH_WireType.faint;

        /// <summary>
        /// Grasshopper builds its faint pen once per painter, from wire_default, the first time it
        /// needs one, and keeps it. If that first time fell inside our transparent window, faint
        /// wires would stay invisible for the rest of the session, plugin or not. Asking for it
        /// while the colours are still real makes sure it is built from them.
        /// </summary>
        public static void WarmFaintPen(GH_Painter painter) =>
            Factory?.Invoke(painter, PointF.Empty, PointF.Empty, false, false, GH_WireType.faint);
    }
}
```

- [ ] **Step 5: Write the plugin lifecycle**

`src/StraightWire/Plugin.cs`:

```csharp
using Grasshopper;
using Grasshopper.GUI.Canvas;
using StraightWire.Interop;

namespace StraightWire
{
    /// <summary>Starts and stops everything the plugin hooks into Grasshopper. Both are idempotent.</summary>
    internal static class Plugin
    {
        static bool _started;

        public static void Start()
        {
            if (_started) return;
            if (!NativeWires.Available)
            {
                Log.Once("unavailable", "this Grasshopper does not expose the wire pens Straight Wire draws with, so it stays off.");
                return;
            }
            _started = true;
            Instances.CanvasCreated += OnCanvasCreated;
            if (Instances.ActiveCanvas != null) OnCanvasCreated(Instances.ActiveCanvas);
        }

        public static void Stop()
        {
            if (!_started) return;
            _started = false;
            Instances.CanvasCreated -= OnCanvasCreated;
        }

        static void OnCanvasCreated(GH_Canvas canvas)
        {
            // Nothing to attach yet; Task 13 adds the canvas hook.
        }
    }
}
```

`src/StraightWire/StraightWirePriority.cs`:

```csharp
using Grasshopper.Kernel;

namespace StraightWire
{
    public class StraightWirePriority : GH_AssemblyPriority
    {
        public override GH_LoadingInstruction PriorityLoad()
        {
            Log.Guard("priority-load", Plugin.Start);
            return GH_LoadingInstruction.Proceed;
        }
    }
}
```

`src/StraightWire/DevHost.cs`:

```csharp
using StraightWire.Interop;

namespace StraightWire
{
    /// <summary>
    /// Entry points for tools/dev-reload.csx, which loads a development build into a running Rhino
    /// without a restart. Grasshopper never sees that build as a plugin, so the marker component
    /// is not registered; everything else behaves as installed. The checks in tools/verify also
    /// call these, on dev builds and on the installed plugin alike.
    /// </summary>
    public static class DevHost
    {
        public static string Attach()
        {
            Plugin.Start();
            return "Straight Wire dev build attached. Wire pens available: " + NativeWires.Available + ".";
        }

        public static string Detach()
        {
            Plugin.Stop();
            return "Straight Wire dev build detached.";
        }
    }
}
```

- [ ] **Step 6: Build**

Run: `dotnet build src/StraightWire --nologo -v q`
Expected: `Build succeeded.` with `0 Error(s)`, and the file `src/StraightWire/bin/Debug/StraightWire.gha` exists (`ls src/StraightWire/bin/Debug/StraightWire.gha`).

- [ ] **Step 7: Write the dev scripts**

`tools/dev-reload.csx`:

```csharp
// Loads the Debug build of Straight Wire into the running Rhino without a restart.
// Send this file's text to the Rhino MCP tool execute_rhinocommon_csharp_code.
// Each run detaches and unloads the previous dev build first. Build first with:
//   dotnet build src/StraightWire --nologo -v q
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

string Gha = "/Users/yolandaxing/Documents/GitHub/straight-wire/src/StraightWire/bin/Debug/StraightWire.gha";
string Slot = "StraightWire.DevContext";

bool installed = AppDomain.CurrentDomain.GetAssemblies().Any(a =>
    a.GetName().Name == "StraightWire" && AssemblyLoadContext.GetLoadContext(a) == AssemblyLoadContext.Default);
if (installed)
{
    output.AppendLine("The installed Straight Wire plugin is loaded. Remove StraightWire.gha from Grasshopper's Libraries folder and restart Rhino before loading dev builds.");
}
else
{
    if (AppDomain.CurrentDomain.GetData(Slot) is AssemblyLoadContext previous)
    {
        var old = previous.Assemblies.FirstOrDefault(a => a.GetName().Name == "StraightWire");
        output.AppendLine(old?.GetType("StraightWire.DevHost")?.GetMethod("Detach")?.Invoke(null, null) as string
                          ?? "The previous build had no DevHost.");
        AppDomain.CurrentDomain.SetData(Slot, null);
        previous.Unload();
    }
    var context = new AssemblyLoadContext("StraightWire.Dev." + DateTime.Now.Ticks, isCollectible: true);
    Assembly asm;
    using (var stream = File.OpenRead(Gha)) asm = context.LoadFromStream(stream);
    AppDomain.CurrentDomain.SetData(Slot, context);
    output.AppendLine(asm.GetType("StraightWire.DevHost").GetMethod("Attach").Invoke(null, null) as string);
    output.AppendLine("Build written at " + File.GetLastWriteTime(Gha).ToString("HH:mm:ss") + ".");
}
```

`tools/dev-call.csx`:

```csharp
// Calls a public static method of StraightWire.DevHost on the loaded build (a dev build from
// tools/dev-reload.csx, or the installed plugin) and prints what it returns.
// Edit the two lines below, then send this file's text to execute_rhinocommon_csharp_code.
using System.Reflection;
using System.Runtime.Loader;

string Method = "Describe";   // Attach, Detach, Describe, Run
object[] Args = null;         // for Run, e.g. new object[] { "style:ortho" }

var asm = (AppDomain.CurrentDomain.GetData("StraightWire.DevContext") as AssemblyLoadContext)?
              .Assemblies.FirstOrDefault(a => a.GetName().Name == "StraightWire")
          ?? AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "StraightWire");
output.AppendLine(asm == null
    ? "Straight Wire is not loaded."
    : asm.GetType("StraightWire.DevHost").GetMethod(Method).Invoke(null, Args) as string);
```

- [ ] **Step 8: Load the build into Rhino**

Read `tools/dev-reload.csx` and send its full text to `mcp__rhinomcp__execute_rhinocommon_csharp_code`.
Expected output:
```
Straight Wire dev build attached. Wire pens available: True.
Build written at <time>.
```

- [ ] **Step 9: Reload to prove the unload path**

Send `tools/dev-reload.csx` again.
Expected output:
```
Straight Wire dev build detached.
Straight Wire dev build attached. Wire pens available: True.
Build written at <time>.
```

- [ ] **Step 10: Commit**

```bash
git add src/StraightWire tools
git commit -F - <<'EOF'
Add the plugin scaffold and a hot-reload loop for Rhino

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GKJGcqmxvBcFYsUcoRsYRS
EOF
```

---

### Task 11: Reading the canvas, and the test fixture

**Files:**
- Create: `src/StraightWire/SceneReader.cs`
- Modify: `src/StraightWire/DevHost.cs` (add `Describe`)
- Create: `tools/make-fixture.csx`
- Create: `tools/verify/describe-fixture.csx`
- Create (generated): `tests/fixtures/twisted-tower.gh`

**Interfaces:**
- Consumes: `Scene`, `SceneNode`, `SceneWire`, `FileSettings`, `WireStyle` (Core).
- Produces:
  - `internal sealed class WireDraw`, all public fields: `Id`, `Source`, `Target` (`IGH_Param`), `Start`, `End` (`PointF`), `SourceSelected`, `TargetSelected`, `Display` (`GH_ParamWireDisplay`), `Style` (`WireStyle`), `Routed` (`bool`).
  - `internal sealed class SceneRead`: `Scene Scene`, `List<WireDraw> Wires`, `int RoutedCount`.
  - `internal static class SceneReader`:
    - `SceneRead Read(GH_Document, FileSettings)`
    - `IEnumerable<(IGH_Param Source, IGH_Param Target)> Connections(GH_Document)`
    - `HashSet<string> LiveWireIds(GH_Document)`
    - `List<string> WireIdsTouchingSelection(GH_Document)`
    - `string WireId(IGH_Param source, IGH_Param target)`
  - `DevHost.Describe()` returns `nodes=… wires=… routed=… curve=… hidden=…`.

What goes into the Scene:
- **Nodes:** every top-level `IGH_ActiveObject`.
- **Wires:** every source of every input, except wires whose target has hidden display. Those are drawn only when selected, and natively.
- **Curve-style wires:** these stay in the Scene (Core skips routing them) so their group gets no junction dots.

- [ ] **Step 1: Write the reader**

`src/StraightWire/SceneReader.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using StraightWire.Core;

namespace StraightWire
{
    /// <summary>One wire as the canvas sees it, with what drawing needs besides its route.</summary>
    internal sealed class WireDraw
    {
        public string Id;
        public IGH_Param Source, Target;
        public PointF Start, End;
        public bool SourceSelected, TargetSelected;
        public GH_ParamWireDisplay Display;
        public WireStyle Style;

        /// <summary>Routed by Core. False for Curve-style wires and for hidden-display wires.</summary>
        public bool Routed;
    }

    internal sealed class SceneRead
    {
        public Scene Scene;
        public List<WireDraw> Wires;
        public int RoutedCount;
    }

    /// <summary>
    /// Turns a Grasshopper document into Core's Scene. Nodes are the top-level components and
    /// parameters; wires are the sources of every input. A wire's id is "sourceGuid>targetGuid".
    /// </summary>
    internal static class SceneReader
    {
        public static SceneRead Read(GH_Document doc, FileSettings settings)
        {
            var nodes = new List<SceneNode>();
            var wires = new List<SceneWire>();
            var draws = new List<WireDraw>();
            int routed = 0;
            foreach (var obj in doc.Objects)
            {
                if (!(obj is IGH_ActiveObject) || obj.Attributes == null) continue;
                nodes.Add(new SceneNode(obj.InstanceGuid.ToString(), ToRect(obj.Attributes.Bounds)));
                foreach (var target in InputsOf(obj))
                    foreach (var source in target.Sources)
                    {
                        var draw = new WireDraw
                        {
                            Id = WireId(source, target),
                            Source = source,
                            Target = target,
                            Start = source.Attributes.OutputGrip,
                            End = target.Attributes.InputGrip,
                            SourceSelected = source.Attributes.GetTopLevel.Selected,
                            TargetSelected = target.Attributes.GetTopLevel.Selected,
                            Display = target.WireDisplay,
                        };
                        draw.Style = settings.StyleOf(draw.Id);
                        draw.Routed = draw.Style != WireStyle.Curve && draw.Display != GH_ParamWireDisplay.hidden;
                        draws.Add(draw);
                        if (draw.Display == GH_ParamWireDisplay.hidden) continue;
                        if (draw.Routed) routed++;
                        wires.Add(new SceneWire(draw.Id, source.InstanceGuid.ToString(),
                            source.Attributes.GetTopLevel.DocObject.InstanceGuid.ToString(), obj.InstanceGuid.ToString(),
                            ToPt(draw.Start), ToPt(draw.End), draw.Style));
                    }
            }
            return new SceneRead { Scene = new Scene(nodes, wires, settings.Spacing), Wires = draws, RoutedCount = routed };
        }

        public static IEnumerable<(IGH_Param Source, IGH_Param Target)> Connections(GH_Document doc)
        {
            foreach (var obj in doc.Objects)
                foreach (var target in InputsOf(obj))
                    foreach (var source in target.Sources)
                        yield return (source, target);
        }

        public static HashSet<string> LiveWireIds(GH_Document doc)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (source, target) in Connections(doc)) ids.Add(WireId(source, target));
            return ids;
        }

        /// <summary>Wires with at least one end on a selected object, in document order: what "selected" style changes act on.</summary>
        public static List<string> WireIdsTouchingSelection(GH_Document doc)
        {
            var ids = new List<string>();
            foreach (var (source, target) in Connections(doc))
                if (source.Attributes.GetTopLevel.Selected || target.Attributes.GetTopLevel.Selected)
                    ids.Add(WireId(source, target));
            return ids;
        }

        public static string WireId(IGH_Param source, IGH_Param target) => source.InstanceGuid + ">" + target.InstanceGuid;

        static IEnumerable<IGH_Param> InputsOf(IGH_DocumentObject obj)
        {
            if (obj is IGH_Component component) return component.Params.Input;
            if (obj is IGH_Param param) return new[] { param };
            return Array.Empty<IGH_Param>();
        }

        static Rect ToRect(RectangleF r) => new Rect(r.Left, r.Top, r.Right, r.Bottom);

        static Pt ToPt(PointF p) => new Pt(p.X, p.Y);
    }
}
```

- [ ] **Step 2: Add `Describe` to DevHost**

In `src/StraightWire/DevHost.cs`, add these usings at the top:

```csharp
using System.Globalization;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using StraightWire.Core;
```

and add these members inside the class:

```csharp
        /// <summary>A one-line summary of the document on the active canvas.</summary>
        public static string Describe()
        {
            var doc = Instances.ActiveCanvas?.Document;
            if (doc == null) return "no document on the canvas";
            var read = SceneReader.Read(doc, SettingsFor(doc));
            return string.Format(CultureInfo.InvariantCulture, "nodes={0} wires={1} routed={2} curve={3} hidden={4}",
                read.Scene.Nodes.Count, read.Wires.Count, read.RoutedCount,
                read.Wires.Count(w => w.Style == WireStyle.Curve && w.Display != GH_ParamWireDisplay.hidden),
                read.Wires.Count(w => w.Display == GH_ParamWireDisplay.hidden));
        }

        static FileSettings SettingsFor(GH_Document doc) => new FileSettings(WireStyle.Diagonal, Routing.DefaultSpacing);
```

- [ ] **Step 3: Build**

Run: `dotnet build src/StraightWire --nologo -v q`
Expected: `Build succeeded.` with `0 Error(s)`.

- [ ] **Step 4: Write and run the fixture builder**

`tools/make-fixture.csx`:

```csharp
// Builds tests/fixtures/twisted-tower.gh: the prototype's example definition, made of real
// Grasshopper components, used by the checks in tools/verify. Send to execute_rhinocommon_csharp_code.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Drawing;
using System.IO;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;

string FixturePath = "/Users/yolandaxing/Documents/GitHub/straight-wire/tests/fixtures/twisted-tower.gh";
var doc = new GH_Document();
var made = new Dictionary<string, IGH_DocumentObject>();

IGH_DocumentObject Add(string id, string guid, float x, float y)
{
    var obj = Instances.ComponentServer.EmitObjectProxy(new Guid(guid)).CreateInstance();
    obj.CreateAttributes();
    obj.Attributes.Pivot = new PointF(x, y);
    doc.AddObject(obj, false);
    made[id] = obj;
    return obj;
}

void Slider(string id, string name, decimal min, decimal max, decimal value, int decimals, float x, float y)
{
    var s = (GH_NumberSlider)Add(id, "57da07bd-ecab-415d-9d86-af36d7073abc", x, y);
    s.NickName = name;
    s.Slider.DecimalPlaces = decimals;
    s.Slider.Minimum = min;
    s.Slider.Maximum = max;
    s.Slider.Value = value;
}

IGH_Param Out(string id, int i) => made[id] is IGH_Component c ? c.Params.Output[i] : (IGH_Param)made[id];
IGH_Param In(string id, int i) => made[id] is IGH_Component c ? c.Params.Input[i] : (IGH_Param)made[id];
void Wire(string a, int ai, string b, int bi) => In(b, bi).AddSource(Out(a, ai));

Slider("floors", "Floors", 2, 60, 24, 0, 40, 60);
Slider("fh", "Floor H", 2.5m, 5, 3.2m, 2, 70, 150);
Slider("radius", "Radius", 4, 30, 12, 1, 20, 410);
Slider("sides", "Sides", 3, 12, 6, 0, 80, 480);
Slider("twist", "Twist", 0, 0.1m, 0.02m, 3, 330, 575);
Slider("div", "Divisions", 4, 40, 16, 0, 560, 20);
Add("series", "e64c5fb1-845c-4ab1-8911-5f338516ba67", 330, 80);
Add("unitz", "9103c240-a6a9-4223-9b42-dbd19bf38e2b", 500, 250);
Add("poly", "845527a6-5cea-4ae9-a667-96ae1667a4e8", 300, 330);
Add("mult", "ce46b74e-00c9-43c4-805a-193b69ea4a11", 590, 420);
Add("move", "e9eb1dcf-92f6-4d4d-84ae-96222d60f56b", 660, 190);
Add("rot", "b7798b74-037e-4f0c-8ac7-dc1043d093e0", 790, 330);
Add("loft", "a7a41d0a-2188-4f7a-82cc-1a2c4e4ec850", 960, 200);
Add("divc", "2162e72e-72fc-4bf8-9459-d4d82fa8aa14", 900, 450);
Add("flip", "41aa4112-9c9b-42f4-847e-503b9d90e4c7", 1080, 480);
Add("intc", "2b2a4145-3dff-41d4-a8de-1ea9d29eef33", 960, 600);
Add("area", "86b28a7e-94d9-4791-8306-e13e10d5f8d5", 1110, 150);
Add("panel", "59e0b89a-e487-49f8-bab8-b5bab16be14c", 1250, 110);

Wire("floors", 0, "series", 2); Wire("fh", 0, "series", 1);
Wire("series", 0, "unitz", 0); Wire("series", 0, "mult", 0); Wire("twist", 0, "mult", 1);
Wire("radius", 0, "poly", 1); Wire("sides", 0, "poly", 2);
Wire("poly", 0, "move", 0); Wire("unitz", 0, "move", 1);
Wire("move", 0, "rot", 0); Wire("mult", 0, "rot", 1);
Wire("rot", 0, "loft", 0); Wire("rot", 0, "divc", 0); Wire("div", 0, "divc", 1);
Wire("divc", 0, "flip", 0); Wire("flip", 0, "intc", 0);
Wire("loft", 0, "area", 0); Wire("area", 0, "panel", 0);

foreach (var o in doc.Objects) { o.Attributes.ExpireLayout(); o.Attributes.PerformLayout(); }
Directory.CreateDirectory(Path.GetDirectoryName(FixturePath));
output.AppendLine(new GH_DocumentIO(doc).SaveQuiet(FixturePath)
    ? "saved " + FixturePath + " with " + doc.ObjectCount + " objects"
    : "save failed");
```

Send it to `mcp__rhinomcp__execute_rhinocommon_csharp_code`.
Expected: `saved /Users/yolandaxing/Documents/GitHub/straight-wire/tests/fixtures/twisted-tower.gh with 19 objects`.

- [ ] **Step 5: Write the describe check**

`tools/verify/describe-fixture.csx`:

```csharp
// Opens the twisted-tower fixture on the canvas just long enough to ask the loaded build to
// describe it, then puts the user's document back. Set Json to give the fixture settings.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Runtime.Loader;
using Grasshopper;
using Grasshopper.Kernel;

string Repo = "/Users/yolandaxing/Documents/GitHub/straight-wire";
string Json = null;   // e.g. "{\"v\":1,\"mode\":\"ortho\",\"spacing\":10,\"wires\":{}}"

object Dev(string method, params object[] args)
{
    var asm = (AppDomain.CurrentDomain.GetData("StraightWire.DevContext") as AssemblyLoadContext)?
                  .Assemblies.FirstOrDefault(a => a.GetName().Name == "StraightWire")
              ?? AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "StraightWire");
    return asm.GetType("StraightWire.DevHost").GetMethod(method).Invoke(null, args.Length == 0 ? null : args);
}

var io = new GH_DocumentIO();
if (!io.Open(Repo + "/tests/fixtures/twisted-tower.gh")) output.AppendLine("could not open the fixture");
else
{
    var doc = io.Document;
    if (Json != null) doc.ValueTable.SetValue("StraightWire.v1", Json);
    var canvas = Instances.ActiveCanvas;
    var original = canvas.Document;
    try
    {
        canvas.Document = doc;
        output.AppendLine((string)Dev("Describe"));
        output.AppendLine("modified=" + doc.IsModified);
    }
    finally
    {
        canvas.Document = original;
        canvas.Refresh();
    }
}
```

- [ ] **Step 6: Reload and describe the fixture**

Send `tools/dev-reload.csx`, then `tools/verify/describe-fixture.csx`.
Expected:
```
nodes=19 wires=18 routed=18 curve=0 hidden=0
modified=False
```

- [ ] **Step 7: Commit**

```bash
git add src/StraightWire tools tests/fixtures/twisted-tower.gh
git commit -F - <<'EOF'
Read the Grasshopper canvas into a routing scene

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GKJGcqmxvBcFYsUcoRsYRS
EOF
```

---

### Task 12: Personal defaults and per-document settings

**Files:**
- Create: `src/StraightWire/UserSettings.cs`
- Create: `src/StraightWire/DocStore.cs`
- Modify: `src/StraightWire/DevHost.cs` (`Describe` uses `DocStore` and reports settings)
- Create: `tools/verify/settings-status.csx`

**Interfaces:**
- Consumes: `FileSettings`, `SettingsJson`, `SettingsStatus`, `StyleNames`, `RouterState`, `RouteResult` (Core); `SceneReader.LiveWireIds` (Task 11); `Log` (Task 10).
- Produces:
  - `internal static class UserSettings`: `WireStyle DefaultMode`, `double DefaultSpacing`, `bool WidgetVisible`, all get/set and persisted.
  - `internal sealed class DocState`:
    - `Status`, `File` (null unless Ok)
    - `RouterState RouterState`, `long Fingerprint`, `RouteResult Routes`
    - `bool CanWrite`, `FileSettings Effective`
  - `internal static class DocStore`: `DocState Get(GH_Document)` and `bool Write(GH_Document, FileSettings)`.

- [ ] **Step 1: Write the personal defaults**

`src/StraightWire/UserSettings.cs`:

```csharp
using System;
using Grasshopper.Kernel;
using StraightWire.Core;

namespace StraightWire
{
    /// <summary>
    /// What this user prefers, for files with no Straight Wire settings of their own. Stored in
    /// Grasshopper's settings folder as StraightWire.xml, never in a .gh file.
    /// </summary>
    internal static class UserSettings
    {
        const string ModeKey = "DefaultMode", SpacingKey = "DefaultSpacing", WidgetKey = "WidgetVisible";

        static GH_SettingsServer _server;

        static GH_SettingsServer Server => _server ?? (_server = new GH_SettingsServer("StraightWire", true));

        public static WireStyle DefaultMode
        {
            get
            {
                try { return StyleNames.TryParseToken(Server.GetValue(ModeKey, "diag"), out var s) ? s : WireStyle.Diagonal; }
                catch { return WireStyle.Diagonal; }
            }
            set => Write(() => Server.SetValue(ModeKey, StyleNames.ToToken(value)));
        }

        public static double DefaultSpacing
        {
            get
            {
                try { return SettingsJson.ClampSpacing(Server.GetValue(SpacingKey, Routing.DefaultSpacing)); }
                catch { return Routing.DefaultSpacing; }
            }
            set => Write(() => Server.SetValue(SpacingKey, SettingsJson.ClampSpacing(value)));
        }

        public static bool WidgetVisible
        {
            get
            {
                try { return Server.GetValue(WidgetKey, true); }
                catch { return true; }
            }
            set => Write(() => Server.SetValue(WidgetKey, value));
        }

        static void Write(Action set)
        {
            try
            {
                set();
                Server.WritePersistentSettings();
            }
            catch (Exception ex)
            {
                Log.Error("settings-write", ex);
            }
        }
    }
}
```

- [ ] **Step 2: Write the document store**

`src/StraightWire/DocStore.cs`:

```csharp
using System.Runtime.CompilerServices;
using Grasshopper.Kernel;
using StraightWire.Core;

namespace StraightWire
{
    /// <summary>What the plugin keeps per Grasshopper document.</summary>
    internal sealed class DocState
    {
        public SettingsStatus Status;

        /// <summary>The file's own settings; null unless Status is Ok.</summary>
        public FileSettings File;

        public readonly RouterState RouterState = new RouterState();
        public long Fingerprint;
        public RouteResult Routes;

        /// <summary>False for a file written by a newer Straight Wire: its settings are never overwritten.</summary>
        public bool CanWrite => Status != SettingsStatus.TooNew;

        /// <summary>The file's settings, or the personal defaults when it has none of its own (or they cannot be read).</summary>
        public FileSettings Effective =>
            Status == SettingsStatus.Ok ? File : new FileSettings(UserSettings.DefaultMode, UserSettings.DefaultSpacing);
    }

    internal static class DocStore
    {
        static readonly ConditionalWeakTable<GH_Document, DocState> States = new ConditionalWeakTable<GH_Document, DocState>();

        /// <summary>
        /// The document's state, reading its settings the first time it is asked for. That first
        /// time is a paint or a user action, so the document has finished loading by then.
        /// </summary>
        public static DocState Get(GH_Document doc) => States.GetValue(doc, Load);

        static DocState Load(GH_Document doc)
        {
            var status = SettingsJson.Parse(doc.ValueTable.GetValue(SettingsJson.Key, null), out var file);
            string key = RuntimeHelpers.GetHashCode(doc).ToString();
            if (status == SettingsStatus.Broken)
                Log.Once("broken-" + key, "the Straight Wire settings in " + doc.DisplayName +
                         " could not be read, so your defaults are used. Changing a wire style replaces them.");
            if (status == SettingsStatus.TooNew)
                Log.Once("too-new-" + key, doc.DisplayName + " was saved with a newer Straight Wire. Your defaults are used, " +
                         "and its wire styles cannot be changed until you update the plugin.");
            return new DocState { Status = status, File = file };
        }

        /// <summary>
        /// Stores settings in the document (dropping overrides for wires that are gone) and marks it
        /// modified. Returns false, changing nothing, when the file belongs to a newer plugin.
        /// </summary>
        public static bool Write(GH_Document doc, FileSettings next)
        {
            var state = Get(doc);
            if (!state.CanWrite)
            {
                Log.Info(doc.DisplayName + " was saved with a newer Straight Wire. Update the plugin to change its wire styles.");
                return false;
            }
            var pruned = next.Pruned(SceneReader.LiveWireIds(doc));
            doc.ValueTable.SetValue(SettingsJson.Key, SettingsJson.Serialize(pruned));
            state.Status = SettingsStatus.Ok;
            state.File = pruned;
            state.Routes = null;
            doc.Modified();
            return true;
        }
    }
}
```

- [ ] **Step 3: Make `Describe` report the settings**

In `src/StraightWire/DevHost.cs`, replace `Describe` and delete `SettingsFor`:

```csharp
        /// <summary>A one-line summary of the document on the active canvas.</summary>
        public static string Describe()
        {
            var doc = Instances.ActiveCanvas?.Document;
            if (doc == null) return "no document on the canvas";
            var state = DocStore.Get(doc);
            var effective = state.Effective;
            var read = SceneReader.Read(doc, effective);
            return string.Format(CultureInfo.InvariantCulture,
                "nodes={0} wires={1} routed={2} curve={3} hidden={4} settings={5} effective={6} overrides={7} writable={8}",
                read.Scene.Nodes.Count, read.Wires.Count, read.RoutedCount,
                read.Wires.Count(w => w.Style == WireStyle.Curve && w.Display != GH_ParamWireDisplay.hidden),
                read.Wires.Count(w => w.Display == GH_ParamWireDisplay.hidden),
                state.Status, StyleNames.Summary(effective), effective.Overrides.Count, state.CanWrite);
        }
```

- [ ] **Step 4: Build**

Run: `dotnet build src/StraightWire --nologo -v q`
Expected: `Build succeeded.` with `0 Error(s)`.

- [ ] **Step 5: Write the settings-status check**

`tools/verify/settings-status.csx`:

```csharp
// Review focus 3: files with no, broken, newer, or odd settings must all open normally.
// Builds a tiny document per case, puts it on the canvas long enough to describe it, then
// restores the user's document.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Drawing;
using System.Runtime.Loader;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;

object Dev(string method, params object[] args)
{
    var asm = (AppDomain.CurrentDomain.GetData("StraightWire.DevContext") as AssemblyLoadContext)?
                  .Assemblies.FirstOrDefault(a => a.GetName().Name == "StraightWire")
              ?? AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "StraightWire");
    return asm.GetType("StraightWire.DevHost").GetMethod(method).Invoke(null, args.Length == 0 ? null : args);
}

var cases = new (string Name, string Json)[]
{
    ("no settings", null),
    ("broken", "{broken"),
    ("newer", "{\"v\":2,\"mode\":\"spiral\"}"),
    ("odd but readable", "{\"v\":1,\"mode\":\"ortho\",\"spacing\":50,\"wires\":{\"gone>gone\":\"bezier\",\"x>y\":\"zigzag\"}}"),
};
var canvas = Instances.ActiveCanvas;
var original = canvas.Document;
try
{
    foreach (var (name, json) in cases)
    {
        var doc = new GH_Document();
        var s = new Param_Number(); s.CreateAttributes(); s.Attributes.Pivot = new PointF(0, 0); doc.AddObject(s, false);
        var t = new Param_Number(); t.CreateAttributes(); t.Attributes.Pivot = new PointF(200, 40); doc.AddObject(t, false);
        t.AddSource(s);
        if (json != null) doc.ValueTable.SetValue("StraightWire.v1", json);
        canvas.Document = doc;
        output.AppendLine(name + ": " + Dev("Describe"));
    }
}
finally
{
    canvas.Document = original;
    canvas.Refresh();
}
```

- [ ] **Step 6: Reload and run the check**

Send `tools/dev-reload.csx`, then `tools/verify/settings-status.csx`.
Expected output. This assumes the personal defaults were never changed; if they were, `effective` shows them instead of `45° · 10`.
```
no settings: nodes=2 wires=1 routed=1 curve=0 hidden=0 settings=Missing effective=45° · 10 overrides=0 writable=True
broken: nodes=2 wires=1 routed=1 curve=0 hidden=0 settings=Broken effective=45° · 10 overrides=0 writable=True
newer: nodes=2 wires=1 routed=1 curve=0 hidden=0 settings=TooNew effective=45° · 10 overrides=0 writable=False
odd but readable: nodes=2 wires=1 routed=1 curve=0 hidden=0 settings=Ok effective=Orthogonal · 20 overrides=1 writable=True
```
Rhino's command line also shows the "could not be read" line and the "saved with a newer Straight Wire" line, once each.

- [ ] **Step 7: Commit**

```bash
git add src/StraightWire tools
git commit -F - <<'EOF'
Keep personal defaults and per-document wire settings

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GKJGcqmxvBcFYsUcoRsYRS
EOF
```

---

### Task 13: Taking over wire drawing

**Files:**
- Create: `src/StraightWire/CanvasHook.cs`
- Create: `src/StraightWire/WireRenderer.cs`
- Modify: `src/StraightWire/Plugin.cs` (attach and detach the hook)
- Modify: `src/StraightWire/DevHost.cs` (`Describe` reports routes; add `Run`)
- Create: `tools/verify/render.csx`, `tools/verify/wire-looks.csx`, `tools/verify/fallback.csx`

**Interfaces:**
- Consumes:
  - `DocStore.Get` and `DocState` (Task 12)
  - `SceneReader.Read`, `SceneRead` and `WireDraw` (Task 11)
  - `NativeWires` and `Log` (Task 10)
  - `Router.Route` (Task 7)
- Produces:
  - `internal sealed class CanvasHook`:
    - `static void Attach(GH_Canvas)`, `static void DetachAll()`
    - `static void ResetFailures()`, `static void RefreshAll()`
    - `static int Count`
    - `internal static bool ThrowOnNextRoute`, `internal static double LastRouteMilliseconds`
  - `internal static class WireRenderer`: `static void Draw(GH_Canvas, SceneRead, RouteResult)`.
  - `DevHost.Run(string command)` with `"fail-next-paint"` and `"retry"`.

- [ ] **Step 1: Write the canvas hook**

`src/StraightWire/CanvasHook.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Grasshopper.GUI.Canvas;
using StraightWire.Core;
using StraightWire.Interop;

namespace StraightWire
{
    /// <summary>
    /// Takes over wire drawing on one canvas.
    ///
    /// Grasshopper draws every wire between CanvasPrePaintWires and CanvasPostPaintWires, with pens
    /// built from four GH_Skin colours. Setting those colours to transparent for that window hides
    /// the native wires without touching any document or setting. PostPaintWires puts them back
    /// first (our pens are built from them too) and then draws the routed wires; CanvasPaintEnd
    /// puts them back again if PostPaintWires never ran. Anything that throws sends this canvas
    /// back to Grasshopper's own wires until the user picks a style again.
    /// </summary>
    internal sealed class CanvasHook
    {
        static readonly List<CanvasHook> Hooks = new List<CanvasHook>();

        /// <summary>Set through DevHost to prove the fallback: the next takeover throws.</summary>
        internal static bool ThrowOnNextRoute;

        /// <summary>How long the last Router.Route call took, for DevHost.Describe.</summary>
        internal static double LastRouteMilliseconds;

        readonly GH_Canvas _canvas;
        bool _takenOver, _failed;
        Color _default, _empty, _selectedA, _selectedB;
        SceneRead _read;
        RouteResult _routes;

        CanvasHook(GH_Canvas canvas)
        {
            _canvas = canvas;
        }

        public static int Count => Hooks.Count;

        public static void Attach(GH_Canvas canvas)
        {
            if (canvas == null || Hooks.Exists(h => h._canvas == canvas)) return;
            var hook = new CanvasHook(canvas);
            canvas.CanvasPrePaintWires += hook.OnPrePaintWires;
            canvas.CanvasPostPaintWires += hook.OnPostPaintWires;
            canvas.CanvasPaintEnd += hook.OnPaintEnd;
            canvas.Disposed += hook.OnDisposed;
            Hooks.Add(hook);
            canvas.Refresh();
        }

        public static void DetachAll()
        {
            foreach (var hook in Hooks.ToArray()) hook.Detach(true);
        }

        /// <summary>Lets every canvas try again after a failure. Called whenever the user changes a style.</summary>
        public static void ResetFailures()
        {
            foreach (var hook in Hooks) hook._failed = false;
        }

        public static void RefreshAll()
        {
            foreach (var hook in Hooks) hook._canvas.Refresh();
        }

        void OnDisposed(object sender, EventArgs e) => Detach(false);

        void Detach(bool refresh)
        {
            if (_takenOver) Restore();
            _canvas.CanvasPrePaintWires -= OnPrePaintWires;
            _canvas.CanvasPostPaintWires -= OnPostPaintWires;
            _canvas.CanvasPaintEnd -= OnPaintEnd;
            _canvas.Disposed -= OnDisposed;
            Hooks.Remove(this);
            if (refresh) _canvas.Refresh();
        }

        void OnPrePaintWires(GH_Canvas canvas)
        {
            _read = null;
            if (_failed || _takenOver) return;
            try
            {
                var doc = canvas.Document;
                if (doc == null) return;
                if (ThrowOnNextRoute)
                {
                    ThrowOnNextRoute = false;
                    throw new InvalidOperationException("forced failure from DevHost");
                }
                var state = DocStore.Get(doc);
                var read = SceneReader.Read(doc, state.Effective);
                if (read.RoutedCount == 0) return;   // all curves: Grasshopper draws everything
                long fingerprint = read.Scene.Fingerprint();
                if (state.Routes == null || state.Fingerprint != fingerprint)
                {
                    var watch = Stopwatch.StartNew();
                    state.Routes = Router.Route(read.Scene, state.RouterState);
                    LastRouteMilliseconds = watch.Elapsed.TotalMilliseconds;
                    state.Fingerprint = fingerprint;
                }
                NativeWires.WarmFaintPen(canvas.Painter);
                _read = read;
                _routes = state.Routes;
                Hide();
            }
            catch (Exception ex)
            {
                Fail("routing", ex);
            }
        }

        void OnPostPaintWires(GH_Canvas canvas)
        {
            if (!_takenOver) return;
            Restore();
            try
            {
                WireRenderer.Draw(canvas, _read, _routes);
            }
            catch (Exception ex)
            {
                Fail("drawing", ex);
            }
            finally
            {
                _read = null;
            }
        }

        void OnPaintEnd(GH_Canvas canvas)
        {
            if (_takenOver) Restore();
        }

        void Hide()
        {
            _default = GH_Skin.wire_default;
            _empty = GH_Skin.wire_empty;
            _selectedA = GH_Skin.wire_selected_a;
            _selectedB = GH_Skin.wire_selected_b;
            GH_Skin.wire_default = Color.Transparent;
            GH_Skin.wire_empty = Color.Transparent;
            GH_Skin.wire_selected_a = Color.Transparent;
            GH_Skin.wire_selected_b = Color.Transparent;
            _takenOver = true;
        }

        void Restore()
        {
            GH_Skin.wire_default = _default;
            GH_Skin.wire_empty = _empty;
            GH_Skin.wire_selected_a = _selectedA;
            GH_Skin.wire_selected_b = _selectedB;
            _takenOver = false;
        }

        void Fail(string what, Exception ex)
        {
            _failed = true;
            if (_takenOver) Restore();
            Log.Once("paint-" + what, what + " failed (" + ex.Message + "). This canvas is back to Grasshopper's own wires; " +
                     "pick a wire style to try again.");
            try { _canvas.Invalidate(); }
            catch { }
        }
    }
}
```

- [ ] **Step 2: Write the renderer**

`src/StraightWire/WireRenderer.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using StraightWire.Core;
using StraightWire.Interop;

namespace StraightWire
{
    /// <summary>
    /// Draws one paint's wires the way Grasshopper would, except along routed polylines. The
    /// rules for type, faint and hidden are Grasshopper's own (GH_Attributes.RenderIncomingWires);
    /// the pens are GH_Painter's, so list, tree, empty, faint and selection all look native.
    /// </summary>
    internal static class WireRenderer
    {
        const float DotRadius = 2.6f;
        const float DotsBelowZoom = 0.5f;

        public static void Draw(GH_Canvas canvas, SceneRead read, RouteResult routes)
        {
            var g = canvas.Graphics;
            var painter = canvas.Painter;
            var visible = canvas.Viewport.VisibleRegion;
            visible.Inflate(10, 10);
            bool fancy = CentralSettings.CanvasFancyWires;
            var dots = new Dictionary<string, DotLook>(StringComparer.Ordinal);

            foreach (var w in read.Wires)
            {
                bool selected = w.SourceSelected || w.TargetSelected;
                // Grasshopper draws its own marker for a hidden wire, in colours we never touch.
                if (w.Display == GH_ParamWireDisplay.hidden && !selected) continue;
                var type = fancy ? GH_Painter.DetermineWireType(w.Source.VolatileData) : GH_WireType.generic;
                if (w.Display == GH_ParamWireDisplay.faint && !selected) type = GH_WireType.faint;

                if (!w.Routed || !routes.Paths.TryGetValue(w.Id, out var path))
                {
                    painter.DrawConnection(w.End, w.Start, GH_WireDirection.left, GH_WireDirection.right,
                                           w.TargetSelected, w.SourceSelected, type);
                    continue;
                }

                Note(dots, w, type);
                if (!Bounds(path).IntersectsWith(visible)) continue;
                var pen = NativeWires.Pen(painter, w.Start, w.End, w.SourceSelected, w.TargetSelected, type);
                if (pen == null) continue;
                try
                {
                    g.DrawLines(pen, ToPoints(path));
                }
                finally
                {
                    if (!NativeWires.IsShared(type)) pen.Dispose();
                }
            }

            if (canvas.Viewport.Zoom < DotsBelowZoom) return;
            foreach (var dot in routes.Junctions)
            {
                if (!dots.TryGetValue(dot.Group, out var look) || !look.Visible) continue;
                float x = (float)dot.At.X, y = (float)dot.At.Y;
                if (!visible.Contains(x, y)) continue;
                using (var brush = new SolidBrush(look.Color))
                    g.FillEllipse(brush, x - DotRadius, y - DotRadius, 2 * DotRadius, 2 * DotRadius);
            }
        }

        /// <summary>A junction dot takes its source's wire colour, and shows only if some wire of its group is drawn at full strength.</summary>
        struct DotLook
        {
            public Color Color;
            public bool Visible;
        }

        static void Note(Dictionary<string, DotLook> dots, WireDraw w, GH_WireType type)
        {
            string group = w.Source.InstanceGuid.ToString();
            if (!dots.TryGetValue(group, out var look))
                look.Color = w.Source.VolatileDataCount == 0 ? GH_Skin.wire_empty
                           : w.SourceSelected ? GH_Skin.wire_selected_a
                           : GH_Skin.wire_default;
            look.Visible |= type != GH_WireType.faint;
            dots[group] = look;
        }

        static RectangleF Bounds(Pt[] path)
        {
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            foreach (var p in path)
            {
                x0 = Math.Min(x0, p.X);
                y0 = Math.Min(y0, p.Y);
                x1 = Math.Max(x1, p.X);
                y1 = Math.Max(y1, p.Y);
            }
            return RectangleF.FromLTRB((float)x0, (float)y0, (float)x1 + 1, (float)y1 + 1);
        }

        static PointF[] ToPoints(Pt[] path)
        {
            var points = new PointF[path.Length];
            for (int i = 0; i < path.Length; i++) points[i] = new PointF((float)path[i].X, (float)path[i].Y);
            return points;
        }
    }
}
```

- [ ] **Step 3: Attach the hook in the lifecycle**

In `src/StraightWire/Plugin.cs`, change `Stop` to:

```csharp
        public static void Stop()
        {
            if (!_started) return;
            _started = false;
            Instances.CanvasCreated -= OnCanvasCreated;
            CanvasHook.DetachAll();
        }
```

and `OnCanvasCreated` to:

```csharp
        static void OnCanvasCreated(GH_Canvas canvas) => Log.Guard("attach", () => CanvasHook.Attach(canvas));
```

- [ ] **Step 4: Extend DevHost**

In `src/StraightWire/DevHost.cs`, replace `Describe` with the version below and add `Run`:

```csharp
        /// <summary>A one-line summary of the document on the active canvas.</summary>
        public static string Describe()
        {
            var doc = Instances.ActiveCanvas?.Document;
            if (doc == null) return "no document on the canvas";
            var state = DocStore.Get(doc);
            var effective = state.Effective;
            var read = SceneReader.Read(doc, effective);
            return string.Format(CultureInfo.InvariantCulture,
                "nodes={0} wires={1} routed={2} curve={3} hidden={4} settings={5} effective={6} overrides={7} writable={8} " +
                "canvases={9} routes={10} last-route={11:0.00}ms",
                read.Scene.Nodes.Count, read.Wires.Count, read.RoutedCount,
                read.Wires.Count(w => w.Style == WireStyle.Curve && w.Display != GH_ParamWireDisplay.hidden),
                read.Wires.Count(w => w.Display == GH_ParamWireDisplay.hidden),
                state.Status, StyleNames.Summary(effective), effective.Overrides.Count, state.CanWrite,
                CanvasHook.Count,
                state.Routes == null ? "none" : state.Routes.Paths.Count + " paths/" + state.Routes.Junctions.Count + " dots",
                CanvasHook.LastRouteMilliseconds);
        }

        /// <summary>Test hooks for tools/verify. Returns what happened.</summary>
        public static string Run(string command)
        {
            switch (command)
            {
                case "fail-next-paint":
                    CanvasHook.ThrowOnNextRoute = true;
                    return "the next takeover will throw";
                case "retry":
                    CanvasHook.ResetFailures();
                    CanvasHook.RefreshAll();
                    return "canvases will try again";
                default:
                    return "unknown command: " + command;
            }
        }
```

- [ ] **Step 5: Build**

Run: `dotnet build src/StraightWire --nologo -v q`
Expected: `Build succeeded.` with `0 Error(s)`.

- [ ] **Step 6: Write the render check**

`tools/verify/render.csx`:

```csharp
// Renders the twisted-tower fixture with the loaded build in one style, to
// artifacts/verify/tower-<mode>.png, and checks the wire skin colours come back.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Drawing;
using System.IO;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.GUI.Canvas;

string Repo = "/Users/yolandaxing/Documents/GitHub/straight-wire";
string Mode = "diag";   // "bezier", "ortho" or "diag"
string Png = Repo + "/artifacts/verify/tower-" + Mode + ".png";
Directory.CreateDirectory(Path.GetDirectoryName(Png));

var io = new GH_DocumentIO();
if (!io.Open(Repo + "/tests/fixtures/twisted-tower.gh")) output.AppendLine("could not open the fixture");
else
{
    var doc = io.Document;
    doc.ValueTable.SetValue("StraightWire.v1", "{\"v\":1,\"mode\":\"" + Mode + "\",\"spacing\":10,\"wires\":{}}");
    var canvas = Instances.ActiveCanvas;
    var original = canvas.Document;
    var before = new[] { GH_Skin.wire_default, GH_Skin.wire_empty, GH_Skin.wire_selected_a, GH_Skin.wire_selected_b };
    try
    {
        canvas.Document = doc;
        doc.Enabled = true;
        doc.NewSolution(false);
        var box = doc.BoundingBox(false);
        var vp = new GH_Viewport(canvas.Viewport) { Width = 1600, Height = 900 };
        vp.Zoom = Math.Min(1600f / (box.Width + 80), 900f / (box.Height + 80));
        vp.MidPoint = new PointF(box.X + box.Width / 2, box.Y + box.Height / 2);
        using (var bmp = canvas.GenerateHiResImageTile(vp, Color.White))
            bmp.Save(Png, System.Drawing.Imaging.ImageFormat.Png);
        output.AppendLine("saved " + Png);
    }
    finally
    {
        canvas.Document = original;
        canvas.Refresh();
    }
    var after = new[] { GH_Skin.wire_default, GH_Skin.wire_empty, GH_Skin.wire_selected_a, GH_Skin.wire_selected_b };
    output.AppendLine("skin colours restored: " + before.SequenceEqual(after));
}
```

- [ ] **Step 7: Write the wire-looks check**

`tools/verify/wire-looks.csx`:

```csharp
// Review focus 4 and 5. One row per wire look, routed right-angle (every target sits 30 lower
// than its source, so routed wires visibly bend), rendered to artifacts/verify/wire-looks.png.
// Grasshopper's cached faint pen is thrown away first, so this paint is the one that rebuilds it.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Drawing;
using System.IO;
using System.Reflection;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Grasshopper.GUI.Canvas;

string Png = "/Users/yolandaxing/Documents/GitHub/straight-wire/artifacts/verify/wire-looks.png";
Directory.CreateDirectory(Path.GetDirectoryName(Png));
string[] rows =
{
    "item", "list", "tree", "empty", "faint", "faint + target selected", "hidden",
    "hidden + source selected", "source selected", "target selected", "both selected", "curve override",
};
var doc = new GH_Document();
var pairs = new List<(Param_Number S, Param_Number T)>();
for (int i = 0; i < rows.Length; i++)
{
    var s = new Param_Number { NickName = "s" + i };
    s.CreateAttributes(); s.Attributes.Pivot = new PointF(0, i * 60); doc.AddObject(s, false);
    var t = new Param_Number { NickName = "t" + i };
    t.CreateAttributes(); t.Attributes.Pivot = new PointF(260, i * 60 + 30); doc.AddObject(t, false);
    string r = rows[i];
    if (r == "list") { s.PersistentData.Append(new GH_Number(1)); s.PersistentData.Append(new GH_Number(2)); }
    else if (r == "tree") { s.PersistentData.Append(new GH_Number(1), new GH_Path(0)); s.PersistentData.Append(new GH_Number(2), new GH_Path(1)); }
    else if (r != "empty") s.PersistentData.Append(new GH_Number(1));
    t.AddSource(s);
    if (r.StartsWith("faint")) t.WireDisplay = GH_ParamWireDisplay.faint;
    if (r.StartsWith("hidden")) t.WireDisplay = GH_ParamWireDisplay.hidden;
    if (r.Contains("target selected") || r == "both selected") t.Attributes.Selected = true;
    if (r.Contains("source selected") || r == "both selected") s.Attributes.Selected = true;
    pairs.Add((s, t));
}
var curve = pairs[rows.Length - 1];
string curveId = curve.S.InstanceGuid + ">" + curve.T.InstanceGuid;
doc.ValueTable.SetValue("StraightWire.v1", "{\"v\":1,\"mode\":\"ortho\",\"spacing\":10,\"wires\":{\"" + curveId + "\":\"bezier\"}}");

var canvas = Instances.ActiveCanvas;
var original = canvas.Document;
var before = new[] { GH_Skin.wire_default, GH_Skin.wire_empty, GH_Skin.wire_selected_a, GH_Skin.wire_selected_b };
foreach (var f in typeof(GH_Painter).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                                    .Where(f => f.Name.Contains("GenerateWirePen_Static_Faint")))
    f.SetValue(canvas.Painter, null);
try
{
    canvas.Document = doc;
    doc.Enabled = true;
    doc.NewSolution(false);
    var vp = new GH_Viewport(canvas.Viewport) { Width = 900, Height = 1500, Zoom = 2f };
    vp.MidPoint = new PointF(130, rows.Length * 30);
    using (var bmp = canvas.GenerateHiResImageTile(vp, Color.White))
        bmp.Save(Png, System.Drawing.Imaging.ImageFormat.Png);
    output.AppendLine("saved " + Png);
}
finally
{
    canvas.Document = original;
    canvas.Refresh();
}
var after = new[] { GH_Skin.wire_default, GH_Skin.wire_empty, GH_Skin.wire_selected_a, GH_Skin.wire_selected_b };
output.AppendLine("skin colours restored: " + before.SequenceEqual(after));
var gen = typeof(GH_Painter).GetMethod("GenerateWirePen", BindingFlags.Instance | BindingFlags.NonPublic);
var faint = (Pen)gen.Invoke(canvas.Painter, new object[] { PointF.Empty, PointF.Empty, false, false, GH_WireType.faint });
output.AppendLine("faint pen: " + faint.Color + " (expect A=50 and the RGB of " + before[0] + ")");
```

- [ ] **Step 8: Write the fallback check**

`tools/verify/fallback.csx`:

```csharp
// Forces the next takeover to throw, renders the fixture, and checks the canvas fell back to
// Grasshopper's own wires with the skin intact. Then lets the canvas try again.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Drawing;
using System.IO;
using System.Runtime.Loader;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.GUI.Canvas;

string Repo = "/Users/yolandaxing/Documents/GitHub/straight-wire";
string Png = Repo + "/artifacts/verify/fallback.png";
Directory.CreateDirectory(Path.GetDirectoryName(Png));

object Dev(string method, params object[] args)
{
    var asm = (AppDomain.CurrentDomain.GetData("StraightWire.DevContext") as AssemblyLoadContext)?
                  .Assemblies.FirstOrDefault(a => a.GetName().Name == "StraightWire")
              ?? AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "StraightWire");
    return asm.GetType("StraightWire.DevHost").GetMethod(method).Invoke(null, args.Length == 0 ? null : args);
}

var io = new GH_DocumentIO();
io.Open(Repo + "/tests/fixtures/twisted-tower.gh");
var doc = io.Document;
doc.ValueTable.SetValue("StraightWire.v1", "{\"v\":1,\"mode\":\"diag\",\"spacing\":10,\"wires\":{}}");
var canvas = Instances.ActiveCanvas;
var original = canvas.Document;
var before = new[] { GH_Skin.wire_default, GH_Skin.wire_empty, GH_Skin.wire_selected_a, GH_Skin.wire_selected_b };
try
{
    canvas.Document = doc;
    doc.Enabled = true;
    doc.NewSolution(false);
    output.AppendLine((string)Dev("Run", "fail-next-paint"));
    var box = doc.BoundingBox(false);
    var vp = new GH_Viewport(canvas.Viewport) { Width = 1600, Height = 900 };
    vp.Zoom = Math.Min(1600f / (box.Width + 80), 900f / (box.Height + 80));
    vp.MidPoint = new PointF(box.X + box.Width / 2, box.Y + box.Height / 2);
    using (var bmp = canvas.GenerateHiResImageTile(vp, Color.White))
        bmp.Save(Png, System.Drawing.Imaging.ImageFormat.Png);
    output.AppendLine("saved " + Png);
}
finally
{
    canvas.Document = original;
    output.AppendLine((string)Dev("Run", "retry"));
}
var after = new[] { GH_Skin.wire_default, GH_Skin.wire_empty, GH_Skin.wire_selected_a, GH_Skin.wire_selected_b };
output.AppendLine("skin colours restored: " + before.SequenceEqual(after));
```

- [ ] **Step 9: Reload and render the three styles**

Send `tools/dev-reload.csx`. Then send `tools/verify/render.csx` three times, with `Mode` set to `"diag"`, `"ortho"` and `"bezier"`. Each run's output should end with `skin colours restored: True`.

Open each PNG with the Read tool and check:
- **`tower-bezier.png`:** Grasshopper's normal curved wires. The plugin draws nothing in Curve mode.
- **`tower-ortho.png`:** every wire is made of horizontal and vertical runs, and none of them crosses a component body.
  - Wires whose runs overlap sit in visibly separate lanes.
  - `Series` feeds two wires and `Rotate` feeds two wires; each pair shares a trunk and has a dot where it splits.
  - The `Series`→`Unit Z`/`Multiplication` wires are double lines (list), and `Divide Curve`→`Flip Matrix`→`Interpolate` are dashed double lines (tree).
  - No grey curve shows behind any routed wire.
- **`tower-diag.png`:** like ortho (no component crossings, list/tree looks, no grey curves behind), but wires with nothing in the way are one 45° diagonal between two horizontal runs, and the right-angle corners that remain are cut at 45°. Same-source wires that each found a free diagonal may no longer share a trunk; that is the prototype's behaviour too.

- [ ] **Step 10: Check every wire look**

Send `tools/verify/wire-looks.csx`.
Expected output:
```
saved /Users/yolandaxing/Documents/GitHub/straight-wire/artifacts/verify/wire-looks.png
skin colours restored: True
faint pen: Color [A=50, R=0, G=0, B=0] (expect A=50 and the RGB of Color [A=150, R=0, G=0, B=0])
```
The RGB follows the user's skin; what matters is A=50 and RGB matching `wire_default`. A white faint pen (`R=255, G=255, B=255`) means the warm-up failed.

Open `wire-looks.png` and check, top to bottom:

| Row | Expected look |
|---|---|
| item | A thin grey right-angle wire |
| list | A double line, right-angle |
| tree | A dashed double line, right-angle |
| empty | Orange, right-angle |
| faint | A very thin, pale right-angle wire |
| faint + target selected | A normal right-angle wire, green at the target end |
| hidden | No wire; only Grasshopper's small arc symbol at the target's input |
| hidden + source selected | Grasshopper's own curve (not routed), green at the source end |
| source selected | Right-angle, green at the source fading toward the target |
| target selected | Right-angle, green at the target end |
| both selected | Right-angle, solid green |
| curve override | Grasshopper's own curve (not routed) |

No row shows a grey native curve behind a routed wire.

- [ ] **Step 11: Check the fallback**

Send `tools/verify/fallback.csx`.
Expected output:
```
the next takeover will throw
saved /Users/yolandaxing/Documents/GitHub/straight-wire/artifacts/verify/fallback.png
canvases will try again
skin colours restored: True
```
Check two things:
- `fallback.png` shows Grasshopper's curved wires (the fallback), not routed ones.
- Rhino's command line shows `Straight Wire: routing failed (forced failure from DevHost)...` the first time this check runs in a session.

- [ ] **Step 12: Commit**

```bash
git add src/StraightWire tools
git commit -F - <<'EOF'
Take over wire drawing with routed traces and native pens

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GKJGcqmxvBcFYsUcoRsYRS
EOF
```

---

### Task 14: Style commands and the Straight Wire menu

**Files:**
- Create: `src/StraightWire/Commands.cs`
- Create: `src/StraightWire/UI/StyleMenu.cs`
- Modify: `src/StraightWire/Plugin.cs` (add and remove the menu)
- Modify: `src/StraightWire/DevHost.cs` (`Run` gains the user commands)
- Create: `tools/verify/settings-roundtrip.csx`, `tools/verify/menu.csx`

**Interfaces:**
- Consumes:
  - `DocStore.Get`, `DocStore.Write` and `UserSettings` (Task 12)
  - `SceneReader.WireIdsTouchingSelection` (Task 11)
  - `CanvasHook.ResetFailures` and `CanvasHook.RefreshAll` (Task 13)
  - `FileSettings`, `StyleNames` and `Routing.SpacingOptions` (Core)
- Produces:
  - `internal static class Commands`:
    - `GH_Document ActiveDocument`
    - `Scope ScopeOf(GH_Document)`, where `Scope` has `SelectedWires`, `Active` (`WireStyle?`), `CanWrite` and `Label`
    - `SetStyle(WireStyle)`, `SetSpacing(double)`, `FollowGlobal()`, `SaveToFile()`, `MakeMyDefault()`
  - `internal static class StyleMenu`: `AddWhenReady()` and `Remove()`.

- [ ] **Step 1: Write the commands**

`src/StraightWire/Commands.cs`:

```csharp
using System;
using System.Collections.Generic;
using Grasshopper;
using Grasshopper.Kernel;
using StraightWire.Core;

namespace StraightWire
{
    /// <summary>
    /// Everything the menu, the canvas widget and the shortcuts can do. With components selected,
    /// a style applies to the wires with at least one end on them; with nothing selected it is the
    /// file's global style. The first change writes the file's settings (and marks it modified).
    /// </summary>
    internal static class Commands
    {
        public static GH_Document ActiveDocument => Instances.ActiveCanvas?.Document;

        /// <summary>What a style change would act on right now.</summary>
        public readonly struct Scope
        {
            public Scope(int selectedWires, WireStyle? active, bool canWrite)
            {
                SelectedWires = selectedWires;
                Active = active;
                CanWrite = canWrite;
            }

            /// <summary>Wires with an end on a selected object; 0 means the change is global.</summary>
            public int SelectedWires { get; }

            /// <summary>The style those wires share (or the global style), null if they differ.</summary>
            public WireStyle? Active { get; }

            public bool CanWrite { get; }

            public string Label => SelectedWires == 0 ? "All" : SelectedWires + " selected";
        }

        public static Scope ScopeOf(GH_Document doc)
        {
            if (doc == null) return new Scope(0, null, false);
            var state = DocStore.Get(doc);
            var effective = state.Effective;
            var ids = SceneReader.WireIdsTouchingSelection(doc);
            if (ids.Count == 0) return new Scope(0, effective.Mode, state.CanWrite);
            var first = effective.StyleOf(ids[0]);
            bool same = ids.TrueForAll(id => effective.StyleOf(id) == first);
            return new Scope(ids.Count, same ? first : (WireStyle?)null, state.CanWrite);
        }

        public static void SetStyle(WireStyle style) =>
            Change("set-style", (current, ids) => ids.Count > 0 ? current.WithOverride(ids, style) : current.WithMode(style));

        public static void SetSpacing(double spacing) =>
            Change("set-spacing", (current, ids) => current.WithSpacing(spacing));

        public static void FollowGlobal() =>
            Change("follow-global", (current, ids) => ids.Count > 0 ? current.WithoutOverride(ids) : null);

        /// <summary>Writes the settings in effect into the file, so others with the plugin see them.</summary>
        public static void SaveToFile() => Change("save-to-file", (current, ids) => current);

        public static void MakeMyDefault()
        {
            Log.Guard("make-default", () =>
            {
                var doc = ActiveDocument;
                if (doc == null) return;
                var effective = DocStore.Get(doc).Effective;
                UserSettings.DefaultMode = effective.Mode;
                UserSettings.DefaultSpacing = effective.Spacing;
                Log.Info(StyleNames.Summary(effective) + " is now your default for files without their own Straight Wire settings.");
                CanvasHook.RefreshAll();
            });
        }

        static void Change(string key, Func<FileSettings, List<string>, FileSettings> change)
        {
            Log.Guard(key, () =>
            {
                var doc = ActiveDocument;
                if (doc == null) return;
                var next = change(DocStore.Get(doc).Effective, SceneReader.WireIdsTouchingSelection(doc));
                if (next == null) return;
                if (!DocStore.Write(doc, next)) return;
                CanvasHook.ResetFailures();
                CanvasHook.RefreshAll();
            });
        }
    }
}
```

- [ ] **Step 2: Write the menu**

`src/StraightWire/UI/StyleMenu.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using Grasshopper;
using Rhino;
using StraightWire.Core;

namespace StraightWire.UI
{
    /// <summary>
    /// A Straight Wire menu in Grasshopper's menu bar. The editor window does not exist yet when
    /// the plugin loads, so the menu waits on Rhino's idle ticks until it does. Item text and
    /// checks are refreshed each time the menu opens, so they always reflect the selection.
    /// </summary>
    internal static class StyleMenu
    {
        static readonly WireStyle[] Styles = { WireStyle.Curve, WireStyle.Orthogonal, WireStyle.Diagonal };
        static ToolStripMenuItem _menu;

        public static void AddWhenReady() => RhinoApp.Idle += OnIdle;

        public static void Remove()
        {
            RhinoApp.Idle -= OnIdle;
            if (_menu == null) return;
            var menu = _menu;
            _menu = null;
            Log.Guard("menu-remove", () => menu.Owner?.Items.Remove(menu));
        }

        static void OnIdle(object sender, EventArgs e)
        {
            var editor = Instances.DocumentEditor;
            if (editor?.MainMenuStrip == null) return;
            RhinoApp.Idle -= OnIdle;
            if (_menu != null) return;
            Log.Guard("menu-add", () =>
            {
                _menu = Build();
                editor.MainMenuStrip.Items.Add(_menu);
            });
        }

        static ToolStripMenuItem Build()
        {
            var menu = new ToolStripMenuItem("Straight Wire");

            var styleItems = new List<(WireStyle Style, ToolStripMenuItem Item)>();
            foreach (var style in Styles)
            {
                var s = style;
                var item = new ToolStripMenuItem(StyleNames.Label(s))
                {
                    ToolTipText = "Draw wires as " + StyleNames.Label(s) + ". With components selected, only their wires change.",
                };
                item.Click += (o, e) => Commands.SetStyle(s);
                menu.DropDownItems.Add(item);
                styleItems.Add((s, item));
            }
            menu.DropDownItems.Add(new ToolStripSeparator());

            var spacing = new ToolStripMenuItem("Wire Spacing");
            foreach (double option in Routing.SpacingOptions)
            {
                double v = option;
                var item = new ToolStripMenuItem(v.ToString(CultureInfo.InvariantCulture));
                item.Click += (o, e) => Commands.SetSpacing(v);
                spacing.DropDownItems.Add(item);
            }
            menu.DropDownItems.Add(spacing);

            var follow = new ToolStripMenuItem("Follow Global") { ToolTipText = "The selected components' wires go back to the file's style." };
            follow.Click += (o, e) => Commands.FollowGlobal();
            menu.DropDownItems.Add(follow);
            menu.DropDownItems.Add(new ToolStripSeparator());

            var save = new ToolStripMenuItem("Save to This File")
            {
                ToolTipText = "Store the wire styles in this file, so anyone with Straight Wire sees the same wires.",
            };
            save.Click += (o, e) => Commands.SaveToFile();
            menu.DropDownItems.Add(save);

            var mine = new ToolStripMenuItem("Make This My Default")
            {
                ToolTipText = "Use this file's style and spacing for files that have none of their own.",
            };
            mine.Click += (o, e) => Commands.MakeMyDefault();
            menu.DropDownItems.Add(mine);

            menu.DropDownOpening += (o, e) => Log.Guard("menu-open", () =>
            {
                var doc = Commands.ActiveDocument;
                var scope = Commands.ScopeOf(doc);
                var effective = doc == null ? null : DocStore.Get(doc).Effective;
                string suffix = scope.SelectedWires == 0 ? ""
                              : " (" + scope.SelectedWires + " selected wire" + (scope.SelectedWires == 1 ? ")" : "s)");
                foreach (var (s, item) in styleItems)
                {
                    item.Text = StyleNames.Label(s) + suffix;
                    item.Checked = scope.Active == s;
                    item.Enabled = scope.CanWrite;
                }
                foreach (ToolStripMenuItem item in spacing.DropDownItems)
                    item.Checked = effective != null && item.Text == effective.Spacing.ToString(CultureInfo.InvariantCulture);
                spacing.Enabled = scope.CanWrite;
                follow.Enabled = scope.CanWrite && scope.SelectedWires > 0;
                save.Enabled = scope.CanWrite;
                mine.Enabled = doc != null;
            });

            return menu;
        }
    }
}
```

- [ ] **Step 3: Add the menu to the lifecycle**

In `src/StraightWire/Plugin.cs`, add `using StraightWire.UI;`. In `Start`, add after `if (Instances.ActiveCanvas != null) OnCanvasCreated(Instances.ActiveCanvas);`:

```csharp
            StyleMenu.AddWhenReady();
```

In `Stop`, add after `CanvasHook.DetachAll();`:

```csharp
            StyleMenu.Remove();
```

- [ ] **Step 4: Give DevHost the user commands**

In `src/StraightWire/DevHost.cs`, replace `Run` with:

```csharp
        /// <summary>
        /// Test hooks for tools/verify: "fail-next-paint", "retry", and the user's commands
        /// "style:bezier|ortho|diag", "spacing:N", "follow", "save", "default".
        /// The user's commands act on the active canvas's document and return Describe().
        /// </summary>
        public static string Run(string command)
        {
            string[] parts = command.Split(':');
            switch (parts[0])
            {
                case "fail-next-paint":
                    CanvasHook.ThrowOnNextRoute = true;
                    return "the next takeover will throw";
                case "retry":
                    CanvasHook.ResetFailures();
                    CanvasHook.RefreshAll();
                    return "canvases will try again";
                case "style" when parts.Length == 2 && StyleNames.TryParseToken(parts[1], out var style):
                    Commands.SetStyle(style);
                    return Describe();
                case "spacing" when parts.Length == 2:
                    Commands.SetSpacing(double.Parse(parts[1], CultureInfo.InvariantCulture));
                    return Describe();
                case "follow":
                    Commands.FollowGlobal();
                    return Describe();
                case "save":
                    Commands.SaveToFile();
                    return Describe();
                case "default":
                    Commands.MakeMyDefault();
                    return Describe();
                default:
                    return "unknown command: " + command;
            }
        }
```

- [ ] **Step 5: Build**

Run: `dotnet build src/StraightWire --nologo -v q`
Expected: `Build succeeded.` with `0 Error(s)`.

- [ ] **Step 6: Write the round-trip and menu checks**

`tools/verify/settings-roundtrip.csx`:

```csharp
// Opening a file without settings leaves it unmodified; the first style change writes settings
// and marks it modified; per-selection styles and spacing land in the JSON; the JSON survives
// a save and reopen; Follow Global clears the selection's overrides.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.IO;
using System.Runtime.Loader;
using Grasshopper;
using Grasshopper.Kernel;

string Repo = "/Users/yolandaxing/Documents/GitHub/straight-wire";
string Saved = Repo + "/artifacts/verify/roundtrip.gh";

object Dev(string method, params object[] args)
{
    var asm = (AppDomain.CurrentDomain.GetData("StraightWire.DevContext") as AssemblyLoadContext)?
                  .Assemblies.FirstOrDefault(a => a.GetName().Name == "StraightWire")
              ?? AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "StraightWire");
    return asm.GetType("StraightWire.DevHost").GetMethod(method).Invoke(null, args.Length == 0 ? null : args);
}

var io = new GH_DocumentIO();
io.Open(Repo + "/tests/fixtures/twisted-tower.gh");
var doc = io.Document;
var canvas = Instances.ActiveCanvas;
var original = canvas.Document;
try
{
    canvas.Document = doc;
    output.AppendLine("opened: " + Dev("Describe") + " modified=" + doc.IsModified);
    output.AppendLine("style:ortho: " + Dev("Run", "style:ortho") + " modified=" + doc.IsModified);
    foreach (var o in doc.Objects) o.Attributes.Selected = o.Name == "Series";
    output.AppendLine("Series selected, style:bezier: " + Dev("Run", "style:bezier"));
    output.AppendLine("spacing:16: " + Dev("Run", "spacing:16"));
    string json = doc.ValueTable.GetValue("StraightWire.v1", null);
    output.AppendLine("json: " + json);
    Directory.CreateDirectory(Path.GetDirectoryName(Saved));
    new GH_DocumentIO(doc).SaveQuiet(Saved);
    var reopened = new GH_DocumentIO();
    reopened.Open(Saved);
    output.AppendLine("reopened json equal: " + (reopened.Document.ValueTable.GetValue("StraightWire.v1", null) == json));
    output.AppendLine("follow: " + Dev("Run", "follow"));
}
finally
{
    canvas.Document = original;
    canvas.Refresh();
}
```

`tools/verify/menu.csx`:

```csharp
// Lists Grasshopper's menu bar and the Straight Wire menu's items.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
using System.Windows.Forms;
using Grasshopper;

var strip = Instances.DocumentEditor?.MainMenuStrip;
if (strip == null) output.AppendLine("Grasshopper's editor is not open");
else
{
    output.AppendLine("menu bar: " + string.Join(" | ", strip.Items.Cast<ToolStripItem>().Select(i => i.Text)));
    var ours = strip.Items.Cast<ToolStripItem>().OfType<ToolStripMenuItem>().FirstOrDefault(i => i.Text == "Straight Wire");
    if (ours != null)
        output.AppendLine("Straight Wire: " + string.Join(" | ", ours.DropDownItems.Cast<ToolStripItem>().Select(i => i is ToolStripSeparator ? "—" : i.Text)));
}
```

- [ ] **Step 7: Reload and run the checks**

Send `tools/dev-reload.csx`. Then send `tools/verify/menu.csx`.
Expected: exactly one `Straight Wire` entry on the menu bar, and the second line:
```
Straight Wire: Curve | Orthogonal | 45° | — | Wire Spacing | Follow Global | — | Save to This File | Make This My Default
```
If the menu bar does not list `Straight Wire` yet, Rhino has not idled since the reload; send `menu.csx` again.

Send `tools/verify/settings-roundtrip.csx`. Expected, line by line:
- `opened:` … `settings=Missing` … `modified=False`
- `style:ortho:` … `settings=Ok effective=Orthogonal · 10 overrides=0` … `modified=True`
- `Series selected, style:bezier:` … `curve=4` … `overrides=4`. Series has 2 inputs and 2 outgoing wires.
- `spacing:16:` … `effective=Orthogonal · 16 overrides=4`
- `json: {"v":1,"mode":"ortho","spacing":16,"wires":{…four "bezier" entries…}}`
- `reopened json equal: True`
- `follow:` … `overrides=0`

- [ ] **Step 8: Commit**

```bash
git add src/StraightWire tools
git commit -F - <<'EOF'
Add style commands and the Straight Wire menu

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GKJGcqmxvBcFYsUcoRsYRS
EOF
```

---

### Task 15: Canvas switch and keyboard shortcuts

**Files:**
- Create: `src/StraightWire/UI/StyleWidget.cs`
- Modify: `src/StraightWire/CanvasHook.cs` (register the widget, listen for keys)
- Modify: `src/StraightWire/DevHost.cs` (`Describe` reports the widget)
- Create: `tools/verify/widget-and-keys.csx`

**Interfaces:**
- Consumes: `Commands.ScopeOf` and `Commands.SetStyle` (Task 14); `UserSettings.WidgetVisible` (Task 12); `StyleNames.ShortLabel` (Core).
- Produces: `internal sealed class StyleWidget : GH_Widget`, and `CanvasHook.HasWidget(GH_Canvas)` for `Describe`.

- [ ] **Step 1: Write the widget**

`src/StraightWire/UI/StyleWidget.cs`:

```csharp
using System.Drawing;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.GUI.Widgets;
using Grasshopper.Kernel;
using StraightWire.Core;

namespace StraightWire.UI
{
    /// <summary>
    /// Three small buttons in the canvas's bottom-left corner (Curve / Ortho / 45°) with the scope
    /// beside them: All, or how many selected wires a click would change. Like Grasshopper's own
    /// widgets, it can be hidden from the canvas widget menu.
    /// </summary>
    internal sealed class StyleWidget : GH_Widget
    {
        static readonly WireStyle[] Order = { WireStyle.Curve, WireStyle.Orthogonal, WireStyle.Diagonal };
        const int Margin = 12, ButtonWidth = 52, ButtonHeight = 22, Gap = 2;

        public override bool Visible
        {
            get => UserSettings.WidgetVisible;
            set => UserSettings.WidgetVisible = value;
        }

        public override string Name => "Straight Wire";

        public override string Description =>
            "Switch between curved, right-angle and 45° wires, for all wires or for the wires on selected components.";

        public override Bitmap Icon_24x24 => null;

        static Rectangle ButtonRect(GH_Canvas canvas, int i) =>
            new Rectangle(Margin + i * (ButtonWidth + Gap), canvas.ClientSize.Height - Margin - ButtonHeight, ButtonWidth, ButtonHeight);

        static int HitButton(GH_Canvas canvas, Point control)
        {
            for (int i = 0; i < Order.Length; i++)
                if (ButtonRect(canvas, i).Contains(control)) return i;
            return -1;
        }

        public override bool Contains(Point pt_control, PointF pt_canvas) => Owner != null && HitButton(Owner, pt_control) >= 0;

        public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
        {
            if (e.Button != MouseButtons.Left) return GH_ObjectResponse.Ignore;
            int i = HitButton(sender, e.ControlLocation);
            if (i < 0) return GH_ObjectResponse.Ignore;
            Commands.SetStyle(Order[i]);
            return GH_ObjectResponse.Handled;
        }

        public override void Render(GH_Canvas canvas)
        {
            var doc = canvas.Document;
            if (doc == null) return;
            var scope = Commands.ScopeOf(doc);
            var g = canvas.Graphics;
            var saved = g.Transform;
            try
            {
                g.ResetTransform();   // widgets draw in control (screen) coordinates
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (var edge = new Pen(Color.FromArgb(160, 0, 0, 0)))
                using (var offFill = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
                using (var onFill = new SolidBrush(Color.FromArgb(255, 50, 50, 50)))
                using (var offText = new SolidBrush(Color.FromArgb(255, 60, 60, 60)))
                using (var onText = new SolidBrush(Color.White))
                {
                    for (int i = 0; i < Order.Length; i++)
                    {
                        var r = ButtonRect(canvas, i);
                        bool on = scope.Active == Order[i];
                        g.FillRectangle(on ? onFill : offFill, r);
                        g.DrawRectangle(edge, r);
                        g.DrawString(StyleNames.ShortLabel(Order[i]), GH_FontServer.StandardAdjusted, on ? onText : offText, r, format);
                    }
                    var last = ButtonRect(canvas, Order.Length - 1);
                    format.Alignment = StringAlignment.Near;
                    g.DrawString(scope.Label, GH_FontServer.StandardAdjusted, offText,
                                 new RectangleF(last.Right + 8, last.Top, 120, last.Height), format);
                }
            }
            finally
            {
                g.Transform = saved;
            }
        }
    }
}
```

- [ ] **Step 2: Register the widget and the shortcuts in the hook**

In `src/StraightWire/CanvasHook.cs`, add these usings:

```csharp
using System.Windows.Forms;
using StraightWire.UI;
```

Add fields after `RouteResult _routes;`:

```csharp
        readonly StyleWidget _widget = new StyleWidget();
```

Add these members:

```csharp
        public static bool HasWidget(GH_Canvas canvas) =>
            canvas != null && canvas.Widgets.Exists(w => w is StyleWidget);

        /// <summary>Grasshopper rebuilds a canvas's widget list now and then; put ours back each time.</summary>
        void OnWidgetListCreated(object sender, GH_CanvasWidgetListEventArgs e)
        {
            _widget.Owner = _canvas;
            e.AddWidget(_widget);
        }

        /// <summary>
        /// Alt+1 / 2 / 3 (Option on a Mac) pick Curve / Orthogonal / 45°. A modifier keeps them clear
        /// of Grasshopper's own keys.
        /// </summary>
        void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Alt || e.Control || e.Shift) return;
            WireStyle style;
            switch (e.KeyCode)
            {
                case Keys.D1: style = WireStyle.Curve; break;
                case Keys.D2: style = WireStyle.Orthogonal; break;
                case Keys.D3: style = WireStyle.Diagonal; break;
                default: return;
            }
            Commands.SetStyle(style);
            e.Handled = true;
        }
```

In `Attach`, after `canvas.Disposed += hook.OnDisposed;`, add:

```csharp
            canvas.KeyDown += hook.OnKeyDown;
            canvas.WidgetListCreated += hook.OnWidgetListCreated;
            if (!HasWidget(canvas))
            {
                hook._widget.Owner = canvas;
                canvas.Widgets.Add(hook._widget);
            }
```

In `Detach`, after `_canvas.Disposed -= OnDisposed;`, add:

```csharp
            _canvas.KeyDown -= OnKeyDown;
            _canvas.WidgetListCreated -= OnWidgetListCreated;
            _canvas.Widgets.Remove(_widget);
```

- [ ] **Step 3: Report the widget in `Describe`**

In `src/StraightWire/DevHost.cs` `Describe`:
- Extend the format string's tail from `"canvases={9} routes={10} last-route={11:0.00}ms"` to `"canvases={9} routes={10} last-route={11:0.00}ms widget={12}"`.
- Add this as the last argument:

```csharp
                CanvasHook.HasWidget(Instances.ActiveCanvas) ? (UserSettings.WidgetVisible ? "shown" : "hidden") : "missing"
```

- [ ] **Step 4: Build**

Run: `dotnet build src/StraightWire --nologo -v q`
Expected: `Build succeeded.` with `0 Error(s)`.

- [ ] **Step 5: Write the widget and shortcut check**

`tools/verify/widget-and-keys.csx`:

```csharp
// Checks the widget is registered and hit-tests its buttons, and that Alt+2 on the canvas
// switches a document to Orthogonal (by raising the canvas's own KeyDown).
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Drawing;
using System.Reflection;
using System.Runtime.Loader;
using System.Windows.Forms;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;

object Dev(string method, params object[] args)
{
    var asm = (AppDomain.CurrentDomain.GetData("StraightWire.DevContext") as AssemblyLoadContext)?
                  .Assemblies.FirstOrDefault(a => a.GetName().Name == "StraightWire")
              ?? AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "StraightWire");
    return asm.GetType("StraightWire.DevHost").GetMethod(method).Invoke(null, args.Length == 0 ? null : args);
}

var canvas = Instances.ActiveCanvas;
var widget = canvas.Widgets.FirstOrDefault(w => w.GetType().Name == "StyleWidget");
output.AppendLine("widget registered: " + (widget != null));
if (widget != null)
{
    int h = canvas.ClientSize.Height;
    output.AppendLine("hit on first button: " + widget.Contains(new Point(20, h - 20), PointF.Empty));
    output.AppendLine("hit far away: " + widget.Contains(new Point(400, 20), PointF.Empty));
}

var doc = new GH_Document();
var s = new Param_Number(); s.CreateAttributes(); s.Attributes.Pivot = new PointF(0, 0); doc.AddObject(s, false);
var t = new Param_Number(); t.CreateAttributes(); t.Attributes.Pivot = new PointF(200, 40); doc.AddObject(t, false);
t.AddSource(s);
var original = canvas.Document;
try
{
    canvas.Document = doc;
    var onKeyDown = typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic);
    onKeyDown.Invoke(canvas, new object[] { new KeyEventArgs(Keys.Alt | Keys.D2) });
    output.AppendLine("after Alt+2: " + Dev("Describe"));
}
finally
{
    canvas.Document = original;
    canvas.Refresh();
}
```

- [ ] **Step 6: Reload and run the check**

Send `tools/dev-reload.csx`, then `tools/verify/widget-and-keys.csx`.
Expected:
```
widget registered: True
hit on first button: True
hit far away: False
after Alt+2: … settings=Ok effective=Orthogonal · <your default spacing> … widget=shown
```

- [ ] **Step 7: Commit**

```bash
git add src/StraightWire tools
git commit -F - <<'EOF'
Add the canvas style switch and Alt+1/2/3 shortcuts

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GKJGcqmxvBcFYsUcoRsYRS
EOF
```

---

### Task 16: The marker component

**Files:**
- Create: `src/StraightWire/Marker.cs`
- Modify: `src/StraightWire/DocStore.cs` (refresh markers on load; ensure one on write)

**Interfaces:**
- Consumes: `StyleNames.Summary` and `FileSettings` (Core); `Log` (Task 10).
- Produces:
  - `public sealed class StraightWireMarker : GH_Component` with `public static readonly Guid Id`.
  - `internal static class Marker`: `bool Registered`, `void Refresh(GH_Document, FileSettings)`, `void Ensure(GH_Document, FileSettings)`.

The marker exists so Grasshopper records Straight Wire in the file's plugin list. A user without the plugin then gets the missing-plugin report (and, once the plugin is on Yak, the Package Manager's install offer). In a dev build the type is not registered with Grasshopper, so `Ensure` logs and does nothing. Task 18 checks the marker with the installed plugin.

- [ ] **Step 1: Write the marker**

`src/StraightWire/Marker.cs`:

```csharp
using System;
using System.Drawing;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using StraightWire.Core;

namespace StraightWire
{
    /// <summary>
    /// Marks a file as using Straight Wire. It does nothing when solving; its message shows the
    /// file's wire settings.
    /// </summary>
    public sealed class StraightWireMarker : GH_Component
    {
        public static readonly Guid Id = new Guid("8E05DF3D-B91F-431C-ABDB-C638AE1934E4");

        public StraightWireMarker()
            : base("Straight Wire", "SW",
                   "Marks this file as using Straight Wire, so people who open it without the plugin are told what is missing " +
                   "and offered the install. Deleting it does not change how wires are drawn.",
                   "Params", "Util")
        {
        }

        public override Guid ComponentGuid => Id;

        /// <summary>Not on the toolbar: the plugin adds it when a file's settings are first written.</summary>
        public override GH_Exposure Exposure => GH_Exposure.hidden;

        protected override Bitmap Icon => null;

        protected override void RegisterInputParams(GH_InputParamManager pManager) { }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager) { }

        protected override void SolveInstance(IGH_DataAccess DA) { }
    }

    internal static class Marker
    {
        /// <summary>False in a dev build, which Grasshopper never registered as a plugin.</summary>
        public static bool Registered => Instances.ComponentServer?.EmitObjectProxy(StraightWireMarker.Id) != null;

        /// <summary>Shows the current settings on every marker in the document.</summary>
        public static void Refresh(GH_Document doc, FileSettings settings)
        {
            foreach (var marker in doc.Objects.OfType<StraightWireMarker>())
            {
                marker.Message = StyleNames.Summary(settings);
                marker.Attributes?.ExpireLayout();
            }
        }

        /// <summary>
        /// Adds a marker 40 above the top-left corner of everything on the canvas, unless the file
        /// already has one. Called only when settings are written, so a deleted marker stays deleted
        /// until the next change.
        /// </summary>
        public static void Ensure(GH_Document doc, FileSettings settings)
        {
            Refresh(doc, settings);
            if (doc.Objects.OfType<StraightWireMarker>().Any()) return;
            if (!Registered)
            {
                Log.Once("marker-dev", "the marker component is not registered (development build), so none was added.");
                return;
            }
            var others = doc.Objects.Where(o => o.Attributes != null).Select(o => o.Attributes.Bounds).ToList();
            var marker = new StraightWireMarker();
            marker.CreateAttributes();
            marker.Message = StyleNames.Summary(settings);
            marker.Attributes.Pivot = PointF.Empty;
            marker.Attributes.ExpireLayout();
            marker.Attributes.PerformLayout();
            var bounds = marker.Attributes.Bounds;
            PointF target = others.Count == 0
                ? new PointF(0, 0 + bounds.Height)                                   // top-left at the origin
                : new PointF(others.Min(b => b.Left), others.Min(b => b.Top) - 40);  // bottom-left 40 above
            marker.Attributes.Pivot = new PointF(target.X - bounds.Left, target.Y - bounds.Bottom);
            marker.Attributes.ExpireLayout();
            marker.Attributes.PerformLayout();
            doc.AddObject(marker, false);
        }
    }
}
```

In `Ensure`, the marker is laid out at pivot (0, 0) first. The pivot is then shifted by the difference between where its bounds landed and where they should be, which works whatever point Grasshopper uses as a component's pivot. With pivot (0,0) the bounds' bottom-left is (`bounds.Left`, `bounds.Bottom`). Moving the pivot to `target − (bounds.Left, bounds.Bottom)` puts the bottom-left at `target`.

- [ ] **Step 2: Hook the marker into the store**

In `src/StraightWire/DocStore.cs` `Load`, replace `return new DocState { Status = status, File = file };` with:

```csharp
            var state = new DocState { Status = status, File = file };
            Marker.Refresh(doc, state.Effective);
            return state;
```

In `Write`, after `doc.ValueTable.SetValue(SettingsJson.Key, SettingsJson.Serialize(pruned));`, add:

```csharp
            Marker.Ensure(doc, pruned);
```

- [ ] **Step 3: Build**

Run: `dotnet build src/StraightWire --nologo -v q`
Expected: `Build succeeded.` with `0 Error(s)`.

- [ ] **Step 4: Check the dev build stays safe**

Send `tools/dev-reload.csx`, then `tools/verify/settings-roundtrip.csx` again.
Expected:
- The same results as in Task 14.
- Rhino's command line shows `Straight Wire: the marker component is not registered (development build), so none was added.` once.
- No exception.

- [ ] **Step 5: Commit**

```bash
git add src/StraightWire
git commit -F - <<'EOF'
Add the Straight Wire marker component

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GKJGcqmxvBcFYsUcoRsYRS
EOF
```

---

### Task 17: Packaging and documentation

**Files:**
- Create: `package.sh`
- Create: `README.md`
- Create: `INSTALL.txt`
- Create: `LICENSE`

**Interfaces:**
- Consumes: the whole build.
- Produces:
  - `dist/straightwire-0.1.0-rh8_0-any.yak`
  - `dist/StraightWire-0.1.0.zip`, containing `StraightWire.gha`, `INSTALL.txt` and `LICENSE`

- [ ] **Step 1: Write the packaging script**

`package.sh`:

```bash
#!/usr/bin/env bash
# Builds both distributables into dist/:
#   straightwire-<version>-rh8_0-any.yak   for Rhino's Package Manager
#   StraightWire-<version>.zip             for manual install
set -euo pipefail
cd "$(dirname "$0")"

VERSION="${VERSION:-0.1.0}"
YAK="${YAK:-/Applications/Rhino 8.app/Contents/Resources/bin/yak}"

echo "==> test"
dotnet test tests/StraightWire.Core.Tests -c Release --nologo -v q

echo "==> build"
rm -rf src/StraightWire/bin src/StraightWire/obj
dotnet build src/StraightWire -c Release --nologo -v q -p:Version="$VERSION"

rm -rf dist && mkdir -p dist/payload dist/zip
cp src/StraightWire/bin/Release/StraightWire.gha dist/payload/
cp src/StraightWire/bin/Release/StraightWire.gha dist/zip/
cp INSTALL.txt LICENSE dist/zip/
cp LICENSE dist/payload/

cat > dist/payload/manifest.yml <<YML
---
name: StraightWire
version: $VERSION
authors:
  - Yolanda Xing
url: https://github.com/YolandaXing210/straight-wire
description: >
  Draws Grasshopper wires as straight, circuit-board style traces. Wires run at right
  angles or at 45 degrees, each in its own lane and around components, and wires from
  the same output share a trunk. Switch styles for all wires or just the selected ones
  from the Straight Wire menu or the switch in the canvas corner. Styles are saved in
  the .gh file, so anyone with Straight Wire sees the same wires.
keywords:
  - grasshopper
  - canvas
  - wire
  - layout
  - tidy
YML

echo "==> yak"
( cd dist/payload && "$YAK" build --platform any >/dev/null && mv ./*.yak .. )

echo "==> zip"
( cd dist/zip && zip -q -r "../StraightWire-$VERSION.zip" . )

rm -rf dist/payload dist/zip
ls -la dist
```

Run: `chmod +x package.sh`

- [ ] **Step 2: Write the license**

`LICENSE`: copy `~/Documents/GitHub/wire-drop/LICENSE` and change three things:
- every `WireDrop` becomes `Straight Wire`
- the repository URL becomes `https://github.com/YolandaXing210/straight-wire`
- the section 2 sentence "Contributions are accepted under the terms in CONTRIBUTING.md." becomes "Contributions are accepted on the condition that the author may use them under this license."

Keep every other word.

- [ ] **Step 3: Write the install notes**

`INSTALL.txt`:

```
Straight Wire — straight, circuit-board style wires for Grasshopper
===================================================================

Draws Grasshopper's wires as right-angle or 45° traces, each in its own lane,
around components. Requires Rhino 8 (Windows or macOS).

Free to use, for any purpose. Please do not redistribute it - share the
download page instead. See LICENSE for the full terms.


INSTALL
-------

macOS
  1. Copy StraightWire.gha into:
     ~/Library/Application Support/McNeel/Rhinoceros/8.0/Plug-ins/
        Grasshopper (b45a29b1-4343-4035-989e-044e8580d9cf)/Libraries/

     Quick way to open that folder: in Grasshopper, File > Special Folders >
     Components Folder.

  2. If macOS blocks it, open Terminal and run:
     xattr -d com.apple.quarantine "<path to StraightWire.gha>"

  3. Restart Rhino.

Windows
  1. RIGHT-CLICK StraightWire.gha > Properties > tick "Unblock" > OK.

  2. Copy it into:
     %APPDATA%\Grasshopper\Libraries\

  3. Restart Rhino.


USING IT
--------

  Straight Wire menu > Curve / Orthogonal / 45°
      With nothing selected: the style of every wire in this file.
      With components selected: only the wires touching them.
  Straight Wire menu > Wire Spacing      Gap between neighbouring lanes.
  Straight Wire menu > Follow Global     Selected wires go back to the file's style.
  Straight Wire menu > Save to This File Store the styles in the .gh, so anyone with
                                         Straight Wire sees the same wires.
  Straight Wire menu > Make This My Default
                                         Use this style for files with none of their own.
  Buttons in the canvas's bottom-left    The same three styles. Hide them from
                                         Grasshopper's canvas widget menu.
  Alt+1 / Alt+2 / Alt+3                  Curve / Orthogonal / 45° (Option on a Mac).

Changing a style in a file stores it in that file and adds a small "Straight
Wire" component in its top-left corner. That component tells anyone who opens
the file without the plugin what is missing.


UNINSTALL
---------

Delete StraightWire.gha from the folder above and restart Rhino. Files keep
working; their wires are simply drawn as curves again.
```

- [ ] **Step 4: Write the README**

`README.md`:

````markdown
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
````

- [ ] **Step 5: Build the packages**

Run: `./package.sh`
Expected: the tests pass and the build succeeds. The final `ls` lists `straightwire-0.1.0-rh8_0-any.yak` and `StraightWire-0.1.0.zip` in `dist/`.

- [ ] **Step 6: Commit**

```bash
git add package.sh README.md INSTALL.txt LICENSE
git commit -F - <<'EOF'
Add packaging script, install notes, README and license

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GKJGcqmxvBcFYsUcoRsYRS
EOF
```

---

### Task 18: Installed end-to-end check (with the user)

This task needs the user: Rhino must be restarted after installing, and some checks are hands-on. Stop where it says so and wait.

**Files:**
- Create: `tools/verify/marker.csx`
- Create: `tools/verify/big-definition.csx`

- [ ] **Step 1: Install the Release build**

```bash
cp src/StraightWire/bin/Release/StraightWire.gha "$HOME/Library/Application Support/McNeel/Rhinoceros/8.0/Plug-ins/Grasshopper (b45a29b1-4343-4035-989e-044e8580d9cf)/Libraries/"
```

- [ ] **Step 2: Ask the user to restart Rhino**

Tell the user: "Straight Wire is installed. Please quit and reopen Rhino, open Grasshopper, and tell me when it's up." Wait for their reply.

- [ ] **Step 3: Confirm the installed build is running**

Send `tools/dev-call.csx` (Method `Describe`). Expected: a `nodes=… widget=shown` line, not `Straight Wire is not loaded.` Then send `tools/verify/menu.csx`. Expected: one `Straight Wire` menu, as in Task 14.

- [ ] **Step 4: Re-run the checks against the installed build**

Send these and compare with the expectations in Tasks 12–15:
- `tools/verify/settings-status.csx`
- `tools/verify/render.csx` (all three modes)
- `tools/verify/wire-looks.csx`
- `tools/verify/fallback.csx`
- `tools/verify/widget-and-keys.csx`
- `tools/verify/settings-roundtrip.csx`

In this round the round-trip must not print the "marker … development build" line.

- [ ] **Step 5: Write and run the marker check**

`tools/verify/marker.csx`:

```csharp
// With the installed plugin: the first settings write adds one marker 40 above the top-left of
// the definition, showing the settings; it survives a save and reopen; a second write adds none.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.IO;
using Grasshopper;
using Grasshopper.Kernel;

string Repo = "/Users/yolandaxing/Documents/GitHub/straight-wire";
string Saved = Repo + "/artifacts/verify/with-marker.gh";

object Dev(string method, params object[] args)
{
    var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "StraightWire");
    return asm.GetType("StraightWire.DevHost").GetMethod(method).Invoke(null, args.Length == 0 ? null : args);
}

var io = new GH_DocumentIO();
io.Open(Repo + "/tests/fixtures/twisted-tower.gh");
var doc = io.Document;
float left = doc.Objects.Min(o => o.Attributes.Bounds.Left), top = doc.Objects.Min(o => o.Attributes.Bounds.Top);
var canvas = Instances.ActiveCanvas;
var original = canvas.Document;
try
{
    canvas.Document = doc;
    Dev("Run", "save");
    var markers = doc.Objects.Where(o => o.ComponentGuid == new Guid("8E05DF3D-B91F-431C-ABDB-C638AE1934E4")).ToList();
    output.AppendLine("markers after first save: " + markers.Count);
    if (markers.Count == 1)
    {
        var m = (GH_Component)markers[0];
        var b = m.Attributes.Bounds;
        output.AppendLine("message: " + m.Message);
        output.AppendLine($"bottom-left ({b.Left:0}, {b.Bottom:0}), expected ({left:0}, {top - 40:0})");
    }
    Dev("Run", "style:ortho");
    output.AppendLine("markers after another change: " + doc.Objects.Count(o => o.ComponentGuid == new Guid("8E05DF3D-B91F-431C-ABDB-C638AE1934E4")));
    Directory.CreateDirectory(Path.GetDirectoryName(Saved));
    new GH_DocumentIO(doc).SaveQuiet(Saved);
    var reopened = new GH_DocumentIO();
    reopened.Open(Saved);
    output.AppendLine("markers after reopen: " + reopened.Document.Objects.Count(o => o.ComponentGuid == new Guid("8E05DF3D-B91F-431C-ABDB-C638AE1934E4")));
}
finally
{
    canvas.Document = original;
    canvas.Refresh();
}
```

Send it. Expected:
```
markers after first save: 1
message: 45° · 10
bottom-left (<x>, <y>), expected (<x>, <y>)      ← the two pairs match
markers after another change: 1
markers after reopen: 1
```
The message shows the user's defaults if they changed them.

- [ ] **Step 6: Measure routing on a large definition inside Grasshopper**

`tools/verify/big-definition.csx`:

```csharp
// 500 number parameters in a 25 x 20 grid with 800 seeded random wires, routed at 45°.
// Renders once (routing everything), moves one parameter 10 units and renders again
// (re-routing, as a drag does each frame), and reports the plugin's last route time.
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/Grasshopper.dll"
#r "/Applications/Rhino 8.app/Contents/Frameworks/RhCore.framework/Resources/ManagedPlugIns/GrasshopperPlugin.rhp/GH_IO.dll"
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.GUI.Canvas;

object Dev(string method, params object[] args)
{
    var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "StraightWire");
    return asm.GetType("StraightWire.DevHost").GetMethod(method).Invoke(null, args.Length == 0 ? null : args);
}

var rng = new Random(7);
var doc = new GH_Document();
var grid = new Param_Number[25, 20];
for (int c = 0; c < 25; c++)
    for (int r = 0; r < 20; r++)
    {
        var p = new Param_Number { NickName = c + "," + r };
        p.CreateAttributes();
        p.Attributes.Pivot = new PointF(c * 160, r * 60);
        doc.AddObject(p, false);
        grid[c, r] = p;
    }
for (int i = 0; i < 800; i++)
{
    int c = rng.Next(0, 24), r = rng.Next(0, 20);
    int tc = Math.Min(24, c + 1 + rng.Next(0, 3)), tr = Math.Clamp(r + rng.Next(-4, 5), 0, 19);
    grid[tc, tr].AddSource(grid[c, r]);
}
doc.ValueTable.SetValue("StraightWire.v1", "{\"v\":1,\"mode\":\"diag\",\"spacing\":10,\"wires\":{}}");
var canvas = Instances.ActiveCanvas;
var original = canvas.Document;
try
{
    canvas.Document = doc;
    var vp = new GH_Viewport(canvas.Viewport) { Width = 1200, Height = 800, Zoom = 0.4f };
    vp.MidPoint = new PointF(2000, 600);
    canvas.GenerateHiResImageTile(vp, Color.White).Dispose();
    output.AppendLine("first route: " + Dev("Describe"));
    grid[12, 10].Attributes.Pivot = new PointF(grid[12, 10].Attributes.Pivot.X + 10, grid[12, 10].Attributes.Pivot.Y);
    grid[12, 10].Attributes.ExpireLayout();
    canvas.GenerateHiResImageTile(vp, Color.White).Dispose();
    output.AppendLine("after a move: " + Dev("Describe"));
}
finally
{
    canvas.Document = original;
    canvas.Refresh();
}
```

Send it. Report both `last-route=` values to the user, together with the Core benchmark from Task 8. The target is under 10 ms. Over that, the user decides what to do: it is reported, not silently accepted.

Some of the 800 random pairs repeat. Depending on whether Grasshopper keeps a repeated source, `wires=` may come out slightly under 800; either is fine.

- [ ] **Step 7: Hand over to the user for the hands-on check**

Ask the user to try these in Grasshopper and say what felt wrong, if anything:
1. Open a real definition of theirs. Click the bottom-left `Ortho` and `45°` buttons, then pick the same styles from the Straight Wire menu.
2. Drag a component around: wires should re-route smoothly every frame.
3. Select a few components, then pick `Curve` from the menu: only their wires go back to curves. Then use `Follow Global`.
4. Press Option+1, Option+2 and Option+3 with the canvas focused. If nothing happens on the Mac, note it: the spec's fallback is Ctrl+Alt+digit.
5. Try Wire Spacing 6 and 20.
6. Zoom far out (below 50%): junction dots disappear, wires stay.
7. Set an input's wire display to Faint, then Hidden (right-click the input > Wire Display), with and without the component selected.
8. Save, close and reopen the file: the styles and the marker component are still there.
9. Optional, checks the missing-plugin path:
   - Move `StraightWire.gha` out of the Libraries folder and restart Rhino.
   - Open the saved file: Grasshopper should report Straight Wire as a missing plugin, and the wires are plain curves.
   - Put the `.gha` back and restart Rhino.

- [ ] **Step 8: Commit the verification scripts**

```bash
git add tools/verify/marker.csx tools/verify/big-definition.csx
git commit -F - <<'EOF'
Add installed-plugin checks for the marker and a large definition

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01GKJGcqmxvBcFYsUcoRsYRS
EOF
```

Tell the user that publishing to the Package Manager is up to them:

```
"/Applications/Rhino 8.app/Contents/Resources/bin/yak" login
"/Applications/Rhino 8.app/Contents/Resources/bin/yak" push dist/straightwire-0.1.0-rh8_0-any.yak
```

Pushing makes the package public on McNeel's server. It is also what makes the install prompt work for people who open a Straight Wire file without the plugin.

