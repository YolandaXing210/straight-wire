using System.Collections.Generic;
using StraightWire.Core;
using Xunit;

namespace StraightWire.Core.Tests
{
    public class SpatialGridTests
    {
        [Fact]
        public void Query_returns_only_intersecting_ids_sorted_and_once()
        {
            var g = new SpatialGrid(64);
            g.Add(3, new Rect(0, 0, 300, 300));   // spans many cells
            g.Add(1, new Rect(10, 10, 20, 20));
            g.Add(2, new Rect(500, 500, 510, 510));
            var hits = new List<int>();
            g.Query(new Rect(0, 0, 100, 100), hits);
            Assert.Equal(new[] { 1, 3 }, hits);
        }

        [Fact]
        public void Remove_and_re_add_move_an_item()
        {
            var g = new SpatialGrid(64);
            g.Add(1, new Rect(0, 0, 10, 10));
            g.Add(1, new Rect(1000, 1000, 1010, 1010));   // replaces
            var hits = new List<int>();
            g.Query(new Rect(0, 0, 10, 10), hits);
            Assert.Empty(hits);
            g.Query(new Rect(1000, 1000, 1001, 1001), hits);
            Assert.Equal(new[] { 1 }, hits);
            g.Remove(1);
            g.Query(new Rect(1000, 1000, 1001, 1001), hits);
            Assert.Empty(hits);
        }

        [Fact]
        public void Negative_coordinates_work()
        {
            var g = new SpatialGrid(64);
            g.Add(7, new Rect(-200, -200, -150, -150));
            var hits = new List<int>();
            g.Query(new Rect(-160, -160, -100, -100), hits);
            Assert.Equal(new[] { 7 }, hits);
        }
    }
}
