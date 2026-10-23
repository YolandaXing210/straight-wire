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
            var ids = TargetIds(doc);
            if (ids.Count == 0) return new Scope(0, effective.Mode, state.CanWrite);
            var first = effective.StyleOf(ids[0]);
            bool same = ids.TrueForAll(id => effective.StyleOf(id) == first);
            return new Scope(ids.Count, same ? first : (WireStyle?)null, state.CanWrite);
        }

        public static void SetStyle(WireStyle style) =>
            Change("Wire Style", (current, ids) => ids.Count > 0 ? current.WithOverride(ids, style) : current.WithMode(style));

        public static void SetSpacing(double spacing) =>
            Change("Wire Spacing", (current, ids) => current.WithSpacing(spacing));

        public static void FollowGlobal() =>
            Change("Follow Global", (current, ids) => ids.Count > 0 ? current.WithoutOverride(ids) : null);

        /// <summary>Writes the settings in effect into the file, so others with the plugin see them.</summary>
        public static void SaveToFile() => Change("Save Wire Styles", (current, ids) => current);

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

        /// <summary>Selected wires, plus wires with an end on a selected component: what style actions act on.</summary>
        public static List<string> TargetIds(GH_Document doc)
        {
            var ids = SceneReader.WireIdsTouchingSelection(doc);
            var canvas = Instances.ActiveCanvas;
            if (canvas != null && canvas.Document == doc)
            {
                var live = SceneReader.LiveWireIds(doc);
                foreach (string id in WireEditor.For(canvas).Selected)
                    if (live.Contains(id) && !ids.Contains(id)) ids.Add(id);
            }
            return ids;
        }

        /// <summary>Keeps a wire's lane where the user dropped it (a fraction of its free range).</summary>
        public static void SetLane(GH_Document doc, string wireId, double fraction) =>
            Edit(doc, "Move Wire Lane", current => current.WithLane(wireId, fraction));

        /// <summary>Keeps a backwards wire's run where the user dropped it, on every wire the drag moved.</summary>
        public static void SetBack(GH_Document doc, IReadOnlyCollection<string> wireIds, BackRun run, double value) =>
            Edit(doc, "Move Wire Lane", current =>
            {
                foreach (string id in wireIds)
                    current = current.WithBack(id, (current.Backs.TryGetValue(id, out var runs) ? runs : new BackRuns()).With(run, value));
                return current;
            });

        /// <summary>Makes a wire hand-drawn through these points; no points makes it automatic again.</summary>
        public static void SetPoints(GH_Document doc, string wireId, IReadOnlyList<Pt> points, string undoName) =>
            Edit(doc, undoName, current =>
            {
                if (!SceneReader.Anchors(doc).TryGetValue(wireId, out var at)) return null;
                var edited = current.WithPoints(wireId, new ManualPoints(points, at.Source, at.Target));
                return points.Count == 0 ? edited.WithoutEdits(new[] { wireId }) : edited;
            });

        public static void ResetWires(GH_Document doc, IEnumerable<string> wireIds) =>
            Edit(doc, "Reset Wires", current => current.WithoutEdits(wireIds));

        /// <summary>Tidy All, or Tidy Selected when the selection makes two or more blocks.</summary>
        public static void Tidy()
        {
            var canvas = Instances.ActiveCanvas;
            if (canvas?.Document != null) Tidier.Run(canvas);
        }

        public static void ResetSelected()
        {
            var doc = ActiveDocument;
            if (doc != null) ResetWires(doc, TargetIds(doc));
        }

        static void Edit(GH_Document doc, string undoName, Func<FileSettings, FileSettings> change)
        {
            Log.Guard(undoName, () =>
            {
                var next = change(DocStore.Get(doc).Effective);
                if (next == null || !DocStore.Write(doc, next, undoName)) return;
                CanvasHook.ResetFailures();
                CanvasHook.RefreshAll();
            });
        }

        static void Change(string key, Func<FileSettings, List<string>, FileSettings> change)
        {
            Log.Guard(key, () =>
            {
                var doc = ActiveDocument;
                if (doc == null) return;
                var next = change(DocStore.Get(doc).Effective, TargetIds(doc));
                if (next == null) return;
                if (!DocStore.Write(doc, next, key)) return;
                CanvasHook.ResetFailures();
                CanvasHook.RefreshAll();
            });
        }
    }
}
