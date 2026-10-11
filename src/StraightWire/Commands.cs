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
