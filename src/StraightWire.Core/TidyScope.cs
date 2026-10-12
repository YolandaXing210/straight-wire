using System;
using System.Collections.Generic;
using System.Linq;

namespace StraightWire.Core
{
    /// <summary>Active objects (components, parameters) can be blocks; annotations move only inside a group; the marker never moves.</summary>
    public enum TidyKind { Active, Annotation, Marker }

    /// <summary>A top-level object on the canvas.</summary>
    public sealed class TidyObject
    {
        public TidyObject(string id, Rect bounds, TidyKind kind)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Bounds = bounds;
            Kind = kind;
        }

        public string Id { get; }
        public Rect Bounds { get; }
        public TidyKind Kind { get; }
    }

    /// <summary>A group. Members lists every object and group inside it, recursively.</summary>
    public sealed class TidyGroup
    {
        public TidyGroup(string id, Rect bounds, IReadOnlyList<string> members, bool selected)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Bounds = bounds;
            Members = members ?? Array.Empty<string>();
            Selected = selected;
        }

        public string Id { get; }
        public Rect Bounds { get; }
        public IReadOnlyList<string> Members { get; }
        public bool Selected { get; }
    }

    /// <summary>A wire between two top-level objects, with the y of its output and input grips.</summary>
    public sealed class TidyWire
    {
        public TidyWire(string id, string source, double sourceY, string target, double targetY)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Source = source ?? "";
            SourceY = sourceY;
            Target = target ?? "";
            TargetY = targetY;
        }

        public string Id { get; }
        public string Source { get; }
        public double SourceY { get; }
        public string Target { get; }
        public double TargetY { get; }
    }

    /// <summary>What a tidy works on: its blocks, the links between them, and what moves with each block.</summary>
    public sealed class TidyPlan
    {
        internal TidyPlan(bool selection, IReadOnlyList<TidyBlock> blocks, IReadOnlyList<TidyLink> links,
                          IReadOnlyDictionary<string, IReadOnlyList<string>> members, IReadOnlyDictionary<string, string> blockOf)
        {
            Selection = selection;
            Blocks = blocks;
            Links = links;
            Members = members;
            BlockOf = blockOf;
        }

        /// <summary>True for Tidy Selected, false for Tidy All.</summary>
        public bool Selection { get; }

        /// <summary>Sorted by id.</summary>
        public IReadOnlyList<TidyBlock> Blocks { get; }

        public IReadOnlyList<TidyLink> Links { get; }

        /// <summary>Block id to the object and group ids that move with it, sorted.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<string>> Members { get; }

        /// <summary>Object or group id to the id of the block it moves with.</summary>
        public IReadOnlyDictionary<string, string> BlockOf { get; }
    }

    /// <summary>
    /// Turns a canvas into blocks and links. A block is a top-level group with everything in it
    /// (groups sharing an object merge), or an active object outside those groups. With two or
    /// more selected blocks only they are tidied; otherwise everything is.
    /// </summary>
    public static class TidyScope
    {
        public static TidyPlan Build(IReadOnlyList<TidyObject> objects, IReadOnlyList<TidyGroup> groups, IReadOnlyList<TidyWire> wires,
                                     IReadOnlyCollection<string> selectedObjects, IReadOnlyCollection<string> selectedWires)
        {
            var objectsById = new Dictionary<string, TidyObject>(StringComparer.Ordinal);
            foreach (var o in objects ?? Array.Empty<TidyObject>())
                if (o != null && !objectsById.ContainsKey(o.Id)) objectsById[o.Id] = o;
            var groupsById = new SortedDictionary<string, TidyGroup>(StringComparer.Ordinal);
            foreach (var g in groups ?? Array.Empty<TidyGroup>())
                if (g != null && !groupsById.ContainsKey(g.Id)) groupsById[g.Id] = g;
            var wireList = (wires ?? Array.Empty<TidyWire>()).Where(w => w != null).OrderBy(w => w.Id, StringComparer.Ordinal).ToList();

            bool IsActive(string id) => objectsById.TryGetValue(id, out var o) && o.Kind == TidyKind.Active;

            var selected = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string id in selectedObjects ?? Array.Empty<string>())
                if (id != null && IsActive(id)) selected.Add(id);
            var picked = new HashSet<string>(selectedWires ?? Array.Empty<string>(), StringComparer.Ordinal);
            foreach (var w in wireList.Where(w => picked.Contains(w.Id)))
            {
                if (IsActive(w.Source)) selected.Add(w.Source);
                if (IsActive(w.Target)) selected.Add(w.Target);
            }

            var selectedGroups = groupsById.Values.Where(g => g.Selected).ToList();
            var insideSelected = new HashSet<string>(selectedGroups.SelectMany(g => g.Members), StringComparer.Ordinal);
            var plan = Make(selectedGroups.Where(g => !insideSelected.Contains(g.Id)).ToList(), selected, true, objectsById, groupsById, wireList);
            if (plan.Blocks.Count >= 2) return plan;

            var nested = new HashSet<string>(groupsById.Values.SelectMany(g => g.Members), StringComparer.Ordinal);
            var loose = new SortedSet<string>(objectsById.Keys.Where(id => IsActive(id) && !nested.Contains(id)), StringComparer.Ordinal);
            return Make(groupsById.Values.Where(g => !nested.Contains(g.Id)).ToList(), loose, false, objectsById, groupsById, wireList);
        }

        /// <summary>The wires whose ends are in two different moved blocks: their lanes and points no longer mean anything.</summary>
        public static List<string> WiresToReset(TidyPlan plan, IEnumerable<string> movedBlocks, IReadOnlyList<TidyWire> wires)
        {
            var moved = new HashSet<string>(movedBlocks ?? Array.Empty<string>(), StringComparer.Ordinal);
            var reset = new List<string>();
            foreach (var w in (wires ?? Array.Empty<TidyWire>()).Where(w => w != null).OrderBy(w => w.Id, StringComparer.Ordinal))
            {
                if (!plan.BlockOf.TryGetValue(w.Source, out string a) || !plan.BlockOf.TryGetValue(w.Target, out string b)) continue;
                if (a != b && moved.Contains(a) && moved.Contains(b)) reset.Add(w.Id);
            }
            return reset;
        }

        static TidyPlan Make(List<TidyGroup> groups, SortedSet<string> objectIds, bool selection,
                             Dictionary<string, TidyObject> objectsById, SortedDictionary<string, TidyGroup> groupsById, List<TidyWire> wires)
        {
            // Everything that moves with a group: the group, the groups and objects in it; never the marker.
            var contents = groups.Select(g =>
            {
                var set = new SortedSet<string>(StringComparer.Ordinal) { g.Id };
                foreach (string m in g.Members)
                    if (groupsById.ContainsKey(m) || (objectsById.TryGetValue(m, out var o) && o.Kind != TidyKind.Marker)) set.Add(m);
                return set;
            }).ToList();

            // Groups that share anything are one block.
            var parent = Enumerable.Range(0, groups.Count).ToArray();
            int Find(int i) => parent[i] == i ? i : parent[i] = Find(parent[i]);
            var owner = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < groups.Count; i++)
                foreach (string m in contents[i])
                {
                    if (owner.TryGetValue(m, out int j)) parent[Find(i)] = Find(j);
                    else owner[m] = i;
                }

            var blocks = new List<TidyBlock>();
            var members = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            var blockOf = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var set in Enumerable.Range(0, groups.Count).GroupBy(Find))
            {
                var all = new SortedSet<string>(set.SelectMany(i => contents[i]), StringComparer.Ordinal);
                string id = set.Select(i => groups[i].Id).Min(StringComparer.Ordinal);
                var boxes = set.Select(i => groups[i].Bounds).ToList();
                blocks.Add(new TidyBlock(id, new Rect(boxes.Min(r => r.X0), boxes.Min(r => r.Y0), boxes.Max(r => r.X1), boxes.Max(r => r.Y1))));
                members[id] = all.ToArray();
                foreach (string m in all) blockOf[m] = id;
            }
            foreach (string id in objectIds)
            {
                if (blockOf.ContainsKey(id)) continue;
                blocks.Add(new TidyBlock(id, objectsById[id].Bounds));
                members[id] = new[] { id };
                blockOf[id] = id;
            }
            blocks.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            var links = new List<TidyLink>();
            foreach (var w in wires)
            {
                if (!blockOf.TryGetValue(w.Source, out string a) || !blockOf.TryGetValue(w.Target, out string b) || a == b) continue;
                links.Add(new TidyLink(a, w.SourceY, b, w.TargetY));
            }
            return new TidyPlan(selection, blocks, links, members, blockOf);
        }
    }
}
