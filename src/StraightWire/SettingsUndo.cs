using Grasshopper.Kernel;
using Grasshopper.Kernel.Undo;
using StraightWire.Core;

namespace StraightWire
{
    /// <summary>
    /// One step in Grasshopper's Undo for a change to the file's wire settings: it holds the
    /// settings JSON from before and after (null meaning the file had none) and swaps them back.
    /// </summary>
    public sealed class SettingsUndo : GH_UndoAction
    {
        readonly string _before, _after;

        public SettingsUndo(string before, string after)
        {
            _before = before;
            _after = after;
        }

        public override bool ExpiresSolution => false;

        protected override void Internal_Undo(GH_Document doc) => Apply(doc, _before);

        protected override void Internal_Redo(GH_Document doc) => Apply(doc, _after);

        /// <summary>Puts the settings from before back without going through the undo server (a failed tidy).</summary>
        internal void Revert(GH_Document doc) => Apply(doc, _before);

        static void Apply(GH_Document doc, string json)
        {
            Log.Guard("undo", () =>
            {
                if (json == null) doc.ValueTable.DeleteValue(SettingsJson.Key);
                else doc.ValueTable.SetValue(SettingsJson.Key, json);
                DocStore.Reload(doc);
                CanvasHook.ResetFailures();
                CanvasHook.RefreshAll();
            });
        }
    }
}
