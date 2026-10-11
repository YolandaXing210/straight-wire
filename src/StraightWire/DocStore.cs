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
            var state = new DocState { Status = status, File = file };
            Marker.Refresh(doc, state.Effective);
            return state;
        }

        /// <summary>Reads the document's settings again (after Undo or Redo swapped them) and drops its cached routes.</summary>
        public static void Reload(GH_Document doc)
        {
            var state = Get(doc);
            state.Status = SettingsJson.Parse(doc.ValueTable.GetValue(SettingsJson.Key, null), out var file);
            state.File = file;
            state.Routes = null;
            Marker.Refresh(doc, state.Effective);
        }

        /// <summary>
        /// Moves hand-drawn points with their components (see Anchoring.Settle). Written silently:
        /// the move that caused it already marked the file modified and has its own undo step.
        /// </summary>
        public static void Settle(GH_Document doc)
        {
            var state = Get(doc);
            if (state.Status != SettingsStatus.Ok || state.File.Points.Count == 0) return;
            var anchors = SceneReader.Anchors(doc);
            var next = Anchoring.Settle(state.File, id => anchors.TryGetValue(id, out var a) ? a : ((Pt, Pt)?)null);
            if (ReferenceEquals(next, state.File)) return;
            doc.ValueTable.SetValue(SettingsJson.Key, SettingsJson.Serialize(next));
            state.File = next;
        }

        /// <summary>
        /// Stores settings in the document (dropping overrides for wires that are gone) and marks it
        /// modified. Returns false, changing nothing, when the file belongs to a newer plugin.
        /// </summary>
        public static bool Write(GH_Document doc, FileSettings next, string undoName)
        {
            var state = Get(doc);
            if (!state.CanWrite)
            {
                Log.Info(doc.DisplayName + " was saved with a newer Straight Wire. Update the plugin to change its wire styles.");
                return false;
            }
            var pruned = next.Pruned(SceneReader.LiveWireIds(doc));
            string before = doc.ValueTable.GetValue(SettingsJson.Key, null), after = SettingsJson.Serialize(pruned);
            doc.ValueTable.SetValue(SettingsJson.Key, after);
            if (undoName != null && before != after) doc.UndoServer.PushUndoRecord(undoName, new SettingsUndo(before, after));
            Marker.Ensure(doc, pruned);
            state.Status = SettingsStatus.Ok;
            state.File = pruned;
            state.Routes = null;
            doc.Modified();
            return true;
        }
    }
}
