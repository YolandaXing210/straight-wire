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
