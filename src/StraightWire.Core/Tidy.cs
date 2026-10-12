using System;
using System.Collections.Generic;
using System.Linq;

namespace StraightWire.Core
{
    /// <summary>Something tidy moves as one rectangle: a component, a parameter, or a whole group.</summary>
    public sealed class TidyBlock
    {
        public TidyBlock(string id, Rect bounds)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Bounds = bounds;
        }

        public string Id { get; }
        public Rect Bounds { get; }
    }

    /// <summary>A wire between two blocks, with the absolute y of its output and input grips.</summary>
    public sealed class TidyLink
    {
        public TidyLink(string source, double sourceY, string target, double targetY)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            SourceY = sourceY;
            Target = target ?? throw new ArgumentNullException(nameof(target));
            TargetY = targetY;
        }

        public string Source { get; }
        public double SourceY { get; }
        public string Target { get; }
        public double TargetY { get; }
    }

    /// <summary>
    /// The prototype's tidy: blocks in columns that follow the data flow left to right, each input
    /// lined up with the output feeding it, columns stacked without gaps, and the old top-to-bottom
    /// order kept wherever nothing forces a change.
    /// </summary>
    public static class Tidy
    {
        public const double Gap = 12;

        sealed class Node
        {
            public TidyBlock Block;
            public int Order;
            public readonly List<Edge> In = new List<Edge>();
            public readonly List<Edge> Out = new List<Edge>();
            public int Depth;
            public bool Source;
            public double X, Y;

            public string Id => Block.Id;
            public double Top => Block.Bounds.Y0;
            public double W => Block.Bounds.X1 - Block.Bounds.X0;
            public double H => Block.Bounds.Y1 - Block.Bounds.Y0;
        }

        sealed class Edge
        {
            public Node From, To;
            public double OutOff, InOff;
            public bool Dropped;
        }

        /// <summary>
        /// The new top-left of every block that moves. Blocks without links never move, and blocks
        /// already in place are left out.
        /// </summary>
        public static IReadOnlyDictionary<string, Pt> Layout(IReadOnlyList<TidyBlock> blocks, IReadOnlyList<TidyLink> links, double spacing)
        {
            var result = new Dictionary<string, Pt>(StringComparer.Ordinal);
            if (blocks == null || links == null) return result;

            var byId = new Dictionary<string, TidyBlock>(StringComparer.Ordinal);
            foreach (var b in blocks)
                if (b != null && !byId.ContainsKey(b.Id)) byId[b.Id] = b;

            var nodes = new Dictionary<string, Node>(StringComparer.Ordinal);
            Node NodeOf(TidyBlock b)
            {
                if (!nodes.TryGetValue(b.Id, out var n)) nodes[b.Id] = n = new Node { Block = b };
                return n;
            }

            var edges = new List<Edge>();
            foreach (var l in links)
            {
                if (l == null || string.Equals(l.Source, l.Target, StringComparison.Ordinal)) continue;
                if (!byId.TryGetValue(l.Source, out var sb) || !byId.TryGetValue(l.Target, out var tb)) continue;
                edges.Add(new Edge { From = NodeOf(sb), To = NodeOf(tb), OutOff = l.SourceY - sb.Bounds.Y0, InOff = l.TargetY - tb.Bounds.Y0 });
            }
            if (edges.Count == 0) return result;

            // Visit order: left to right, then top to bottom. Everything below iterates in it, so the
            // result never depends on the order blocks and links were given in.
            var order = nodes.Values.OrderBy(n => n.Block.Bounds.X0).ThenBy(n => n.Block.Bounds.Y0)
                             .ThenBy(n => n.Id, StringComparer.Ordinal).ToList();
            for (int i = 0; i < order.Count; i++) order[i].Order = i;
            edges.Sort((a, b) =>
            {
                int c = a.From.Order.CompareTo(b.From.Order);
                if (c == 0) c = a.To.Order.CompareTo(b.To.Order);
                if (c == 0) c = a.OutOff.CompareTo(b.OutOff);
                if (c == 0) c = a.InOff.CompareTo(b.InOff);
                return c;
            });
            foreach (var e in edges) e.From.Out.Add(e);

            DropCycles(order);
            foreach (var e in edges)
                if (!e.Dropped) e.To.In.Add(e);
            foreach (var n in order) n.Out.RemoveAll(e => e.Dropped);

            Depths(order);
            var columns = order.GroupBy(n => n.Depth).OrderBy(g => g.Key).Select(g => g.ToList()).ToList();
            PlaceColumns(columns, spacing);
            StackSources(columns);
            AlignInputs(columns);
            AlignSources(columns);
            AlignInputs(columns);

            // The layout keeps the old top-left corner. Grasshopper floors positions to whole units,
            // so every block moves by a whole number of units: a fraction would be undone at once,
            // making the next tidy "move" it again, and would let a group's members drift apart.
            double ox = order.Min(n => n.Block.Bounds.X0) - order.Min(n => n.X);
            double oy = order.Min(n => n.Block.Bounds.Y0) - order.Min(n => n.Y);
            foreach (var n in order)
            {
                double dx = Math.Round(n.X + ox - n.Block.Bounds.X0), dy = Math.Round(n.Y + oy - n.Block.Bounds.Y0);
                if (dx != 0 || dy != 0) result[n.Id] = new Pt(n.Block.Bounds.X0 + dx, n.Block.Bounds.Y0 + dy);
            }
            return result;
        }

        /// <summary>
        /// Grasshopper's data flow has no cycles, but blocks can: group A feeds B, which feeds back
        /// into A. A depth-first walk in visit order drops each link that closes a cycle, which is
        /// the one running backwards in the current layout.
        /// </summary>
        static void DropCycles(List<Node> order)
        {
            var state = new Dictionary<Node, int>();
            var stack = new Stack<(Node Node, int Next)>();
            foreach (var root in order)
            {
                if (state.ContainsKey(root)) continue;
                state[root] = 1;
                stack.Push((root, 0));
                while (stack.Count > 0)
                {
                    var (node, next) = stack.Pop();
                    if (next >= node.Out.Count)
                    {
                        state[node] = 2;
                        continue;
                    }
                    stack.Push((node, next + 1));
                    var e = node.Out[next];
                    if (!state.TryGetValue(e.To, out int s))
                    {
                        state[e.To] = 1;
                        stack.Push((e.To, 0));
                    }
                    else if (s == 1) e.Dropped = true;
                }
            }
        }

        /// <summary>Longest path from a source; sources then move to just before what they feed.</summary>
        static void Depths(List<Node> order)
        {
            var remaining = order.ToDictionary(n => n, n => n.In.Count);
            var queue = new Queue<Node>(order.Where(n => n.In.Count == 0));
            while (queue.Count > 0)
            {
                var n = queue.Dequeue();
                foreach (var e in n.Out)
                {
                    e.To.Depth = Math.Max(e.To.Depth, n.Depth + 1);
                    if (--remaining[e.To] == 0) queue.Enqueue(e.To);
                }
            }
            foreach (var n in order)
            {
                n.Source = n.In.Count == 0;
                if (n.Source && n.Out.Count > 0) n.Depth = Math.Max(0, n.Out.Min(e => e.To.Depth) - 1);
            }
        }

        static void PlaceColumns(List<List<Node>> columns, double spacing)
        {
            double x = 0;
            foreach (var column in columns)
            {
                double width = column.Max(n => n.W);
                var grips = new HashSet<(Node, double)>();
                foreach (var n in column)
                {
                    n.X = n.Source ? x + width - n.W : x;
                    foreach (var e in n.Out) grips.Add((n, e.OutOff));
                }
                x += width + Math.Max(64, 40 + grips.Count * spacing * 1.2);
            }
        }

        /// <summary>Each column's sources, tightly stacked from the topmost one, in their old order.</summary>
        static void StackSources(List<List<Node>> columns)
        {
            foreach (var column in columns)
            {
                foreach (var n in column) n.Y = n.Top;
                var sources = column.Where(n => n.Source).OrderBy(n => n.Top).ThenBy(n => n.Id, StringComparer.Ordinal).ToList();
                if (sources.Count == 0) continue;
                double y = sources[0].Top;
                foreach (var n in sources)
                {
                    n.Y = y;
                    y += n.H + Gap;
                }
            }
        }

        /// <summary>
        /// Each block lines its first input up with the output feeding it: the input with the
        /// smallest offset among those fed by a non-source, or the smallest overall.
        /// </summary>
        static void AlignInputs(List<List<Node>> columns)
        {
            foreach (var column in columns)
            {
                var want = new Dictionary<Node, double>();
                foreach (var n in column)
                {
                    if (n.Source)
                    {
                        want[n] = n.Y;
                        continue;
                    }
                    var pick = First(n.In.Where(e => !e.From.Source)) ?? First(n.In);
                    want[n] = pick.From.Y + pick.OutOff - pick.InOff;
                }
                Resolve(column, want);
            }

            static Edge First(IEnumerable<Edge> candidates) =>
                candidates.OrderBy(e => e.InOff).ThenBy(e => e.From.Id, StringComparer.Ordinal).ThenBy(e => e.OutOff).FirstOrDefault();
        }

        /// <summary>Each source lines its first output up with the topmost input it feeds.</summary>
        static void AlignSources(List<List<Node>> columns)
        {
            foreach (var column in columns)
            {
                var want = new Dictionary<Node, double>();
                foreach (var n in column)
                {
                    if (!n.Source || n.Out.Count == 0)
                    {
                        want[n] = n.Y;
                        continue;
                    }
                    double first = n.Out.Min(e => e.OutOff);
                    var pick = n.Out.Where(e => e.OutOff == first).OrderBy(e => e.To.Y)
                                    .ThenBy(e => e.To.Id, StringComparer.Ordinal).ThenBy(e => e.InOff).First();
                    want[n] = pick.To.Y + pick.InOff - pick.OutOff;
                }
                Resolve(column, want);
            }
        }

        /// <summary>Everyone gets the y it wants unless that would overlap the block above.</summary>
        static void Resolve(List<Node> column, Dictionary<Node, double> want)
        {
            double bottom = double.NegativeInfinity;
            foreach (var n in column.OrderBy(n => want[n]).ThenBy(n => n.Y).ThenBy(n => n.Id, StringComparer.Ordinal).ToList())
            {
                n.Y = Math.Max(want[n], bottom + Gap);
                bottom = n.Y + n.H;
            }
        }
    }
}
