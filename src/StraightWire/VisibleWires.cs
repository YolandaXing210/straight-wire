using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using StraightWire.Core;

namespace StraightWire
{
    /// <summary>A completed paint's paths and the individual connections behind them.</summary>
    internal sealed class VisibleWires
    {
        internal sealed class Entry
        {
            public WireDraw Wire;
            public Pt[] Path;
            public bool ReplacedCurve;
            public Pt[] NativePath;
        }

        internal sealed class Hit
        {
            public Entry Entry;
            public int Segment;
            public double Distance;
        }

        public readonly GH_Document Document;
        readonly long _fingerprint;
        readonly string _settings;
        readonly float _zoom;
        public readonly List<Entry> Entries = new List<Entry>();

        public VisibleWires(GH_Canvas canvas, SceneRead read)
        {
            Document = canvas.Document;
            _fingerprint = read.Scene.Fingerprint();
            _settings = Document.ValueTable.GetValue(SettingsJson.Key, null);
            _zoom = canvas.Viewport.Zoom;
        }

        public void Add(WireDraw wire, Pt[] path) =>
            Entries.Add(new Entry { Wire = wire, Path = path.ToArray(), ReplacedCurve = true, NativePath = Curve(wire, 0.25f / _zoom) });

        public void AddCurve(WireDraw wire)
        {
            if (wire.Display == GH_ParamWireDisplay.hidden && !wire.SourceSelected && !wire.TargetSelected) return;
            Entries.Add(new Entry { Wire = wire, Path = Curve(wire, 0.25f / _zoom) });
        }

        // Same argument order and directions as DrawConnection. Reverse to source -> target.
        static Pt[] Curve(WireDraw wire, float flatness)
        {
            using (var path = GH_Painter.ConnectionPath(wire.End, wire.Start, GH_WireDirection.left, GH_WireDirection.right))
            {
                path.Flatten(null, flatness);
                return path.PathPoints.AsEnumerable().Reverse().Select(p => new Pt(p.X, p.Y)).ToArray();
            }
        }

        public bool IsCurrent(GH_Canvas canvas)
        {
            if (canvas.Document != Document || canvas.Viewport.Zoom != _zoom ||
                Document.ValueTable.GetValue(SettingsJson.Key, null) != _settings) return false;
            var read = SceneReader.Read(Document, DocStore.Get(Document).Effective);
            if (read.Scene.Fingerprint() != _fingerprint) return false;
            // Selection can change the visibility of hidden wires without changing routing.
            return Entries.All(e => e.Wire.Target.Sources.Contains(e.Wire.Source) &&
                e.Wire.Source.Attributes.OutputGrip == e.Wire.Start && e.Wire.Target.Attributes.InputGrip == e.Wire.End &&
                e.Wire.Source.Attributes.GetTopLevel.Selected == e.Wire.SourceSelected &&
                e.Wire.Target.Attributes.GetTopLevel.Selected == e.Wire.TargetSelected && e.Wire.Target.WireDisplay == e.Wire.Display);
        }

        public List<Hit> Hits(PointF point, double tolerance)
        {
            var p = new Pt(point.X, point.Y);
            var hits = new List<Hit>();
            foreach (var entry in Entries)
            {
                var hit = WireEdit.HitWire(new[] { new KeyValuePair<string, Pt[]>(entry.Wire.Id, entry.Path) }, p, tolerance);
                if (hit.HasValue) hits.Add(new Hit { Entry = entry, Segment = hit.Value.Segment, Distance = hit.Value.Distance });
            }
            return hits.OrderBy(h => h.Distance).ThenBy(h => h.Entry.Wire.Id, StringComparer.Ordinal).ToList();
        }

        public bool OnReplacedCurve(PointF point, double tolerance)
        {
            var p = new Pt(point.X, point.Y);
            foreach (var entry in Entries.Where(e => e.ReplacedCurve))
            {
                var path = entry.NativePath;
                if (WireEdit.HitWire(new[] { new KeyValuePair<string, Pt[]>(entry.Wire.Id, path) }, p, tolerance).HasValue) return true;
            }
            return false;
        }
    }
}
