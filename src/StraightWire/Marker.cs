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
