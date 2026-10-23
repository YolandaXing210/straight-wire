namespace StraightWire.Core
{
    /// <summary>One of the three runs of a backwards wire between its stubs.</summary>
    public enum BackRun { None, Out, In, Cross }

    /// <summary>
    /// Where the user put a backwards wire's runs; a part that is null is automatic. Out and In
    /// are the verticals' distances from the output and input grips; Cross is how far the run
    /// back to the left sits below where routing would put it (negative is above).
    /// </summary>
    public sealed class BackRuns
    {
        public BackRuns(double? @out = null, double? @in = null, double? cross = null)
        {
            Out = @out;
            In = @in;
            Cross = cross;
        }

        public double? Out { get; }
        public double? In { get; }
        public double? Cross { get; }

        public bool IsEmpty => Out == null && In == null && Cross == null;

        public double? this[BackRun run] =>
            run == BackRun.Out ? Out : run == BackRun.In ? In : run == BackRun.Cross ? Cross : null;

        public BackRuns With(BackRun run, double? value) =>
            new BackRuns(run == BackRun.Out ? value : Out, run == BackRun.In ? value : In, run == BackRun.Cross ? value : Cross);
    }
}
