using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using StraightWire.Core;
using Xunit;
using Xunit.Abstractions;

namespace StraightWire.Core.Tests
{
    /// <summary>Timing tests run alone: sharing the CPU with other test classes skews them.</summary>
    [CollectionDefinition("Performance", DisableParallelization = true)]
    public class PerformanceCollection { }

    [Collection("Performance")]
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
            // Warm up to steady state: .NET first runs a method unoptimised and recompiles it
            // after ~30 calls, in the background. A drag reaches that within half a second, and
            // steady state is what the budget is about.
            for (int i = 0; i < 60; i++) action();
            System.Threading.Thread.Sleep(500);
            for (int i = 0; i < 10; i++) action();
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
