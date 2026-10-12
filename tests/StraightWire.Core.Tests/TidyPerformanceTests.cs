using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using StraightWire.Core;
using Xunit;
using Xunit.Abstractions;

namespace StraightWire.Core.Tests
{
    [Collection("Performance")]
    public class TidyPerformanceTests
    {
        const double BudgetMs = 100;

        readonly ITestOutputHelper _out;

        public TidyPerformanceTests(ITestOutputHelper output)
        {
            _out = output;
        }

        [Fact]
        public void Five_hundred_blocks_lay_out_within_budget()
        {
            var random = new Random(42);
            var blocks = Enumerable.Range(0, 500)
                .Select(i => new TidyBlock("b" + i.ToString("000"), Rect.FromXYWH(random.Next(0, 5000), random.Next(0, 5000), 100, 40)))
                .ToArray();
            var links = new List<TidyLink>();
            for (int k = 0; links.Count < 800; k++)
            {
                int i = random.Next(0, 500), j = random.Next(0, 500);
                if (i == j) continue;
                var from = blocks[Math.Min(i, j)];
                var to = blocks[Math.Max(i, j)];
                links.Add(new TidyLink(from.Id, from.Bounds.Y0 + 20, to.Id, to.Bounds.Y0 + 10 + 10 * (k % 3)));
            }

            IReadOnlyDictionary<string, Pt> result = null;
            for (int i = 0; i < 2; i++) result = Tidy.Layout(blocks, links, 10);
            var times = new List<double>();
            for (int i = 0; i < 5; i++)
            {
                var watch = Stopwatch.StartNew();
                result = Tidy.Layout(blocks, links, 10);
                times.Add(watch.Elapsed.TotalMilliseconds);
            }
            times.Sort();
            _out.WriteLine($"median {times[2]:0.0} ms, {result.Count} blocks moved");
            Assert.NotEmpty(result);
            Assert.True(times[2] < BudgetMs, $"median {times[2]:0.0} ms over {BudgetMs} ms");
        }
    }
}
