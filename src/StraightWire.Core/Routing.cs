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
