using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace StraightWire.Core
{
    /// <summary>A hand-drawn wire's interior points, and where its two components were when they were last settled.</summary>
    public sealed class ManualPoints
    {
        public ManualPoints(IReadOnlyList<Pt> points, Pt sourceAt, Pt targetAt)
        {
            Points = points.ToArray();
            SourceAt = sourceAt;
            TargetAt = targetAt;
        }

        public IReadOnlyList<Pt> Points { get; }

        /// <summary>Top-left corner of the source component when the points were last settled.</summary>
        public Pt SourceAt { get; }

        /// <summary>Top-left corner of the target component when the points were last settled.</summary>
        public Pt TargetAt { get; }

        public ManualPoints Shifted(double dx, double dy) =>
            new ManualPoints(Points.Select(p => new Pt(p.X + dx, p.Y + dy)).ToArray(),
                             new Pt(SourceAt.X + dx, SourceAt.Y + dy), new Pt(TargetAt.X + dx, TargetAt.Y + dy));

        public ManualPoints WithAnchors(Pt sourceAt, Pt targetAt) => new ManualPoints(Points, sourceAt, targetAt);
    }

    /// <summary>
    /// A file's wire settings: its global style, its spacing, per-wire style overrides, and the
    /// user's edits (lane positions, hand-drawn points and backwards wires' runs), all keyed by
    /// wire id. Immutable.
    /// </summary>
    public sealed class FileSettings
    {
        public FileSettings(WireStyle mode, double spacing, IReadOnlyDictionary<string, WireStyle> overrides = null,
                            IReadOnlyDictionary<string, double> lanes = null, IReadOnlyDictionary<string, ManualPoints> points = null,
                            IReadOnlyDictionary<string, BackRuns> backs = null)
        {
            Mode = mode;
            Spacing = SettingsJson.ClampSpacing(spacing);
            Overrides = Copy(overrides);
            Lanes = Copy(lanes);
            Points = Copy(points);
            Backs = Copy(backs);
        }

        public WireStyle Mode { get; }
        public double Spacing { get; }
        public IReadOnlyDictionary<string, WireStyle> Overrides { get; }

        /// <summary>Where the user put each automatic wire's lane, as a fraction of its free range.</summary>
        public IReadOnlyDictionary<string, double> Lanes { get; }

        /// <summary>Hand-drawn wires. A wire listed here ignores its lane and back entries.</summary>
        public IReadOnlyDictionary<string, ManualPoints> Points { get; }

        /// <summary>Where the user put each automatic backwards wire's runs.</summary>
        public IReadOnlyDictionary<string, BackRuns> Backs { get; }

        public WireStyle StyleOf(string wireId) => Overrides.TryGetValue(wireId, out var s) ? s : Mode;

        public FileSettings WithMode(WireStyle mode) => new FileSettings(mode, Spacing, Overrides, Lanes, Points, Backs);

        public FileSettings WithSpacing(double spacing) => new FileSettings(Mode, spacing, Overrides, Lanes, Points, Backs);

        public FileSettings WithOverride(IEnumerable<string> wireIds, WireStyle style)
        {
            var d = Copy(Overrides);
            foreach (string id in wireIds) d[id] = style;
            return new FileSettings(Mode, Spacing, d, Lanes, Points, Backs);
        }

        public FileSettings WithoutOverride(IEnumerable<string> wireIds)
        {
            var d = Copy(Overrides);
            foreach (string id in wireIds) d.Remove(id);
            return new FileSettings(Mode, Spacing, d, Lanes, Points, Backs);
        }

        public FileSettings WithLane(string wireId, double fraction)
        {
            var d = Copy(Lanes);
            d[wireId] = Geo.Clamp(fraction, 0, 1);
            return new FileSettings(Mode, Spacing, Overrides, d, Points, Backs);
        }

        /// <summary>Makes the wire hand-drawn through these points; an empty list makes it automatic again.</summary>
        public FileSettings WithPoints(string wireId, ManualPoints points)
        {
            var d = Copy(Points);
            if (points == null || points.Points.Count == 0) d.Remove(wireId);
            else d[wireId] = points;
            return new FileSettings(Mode, Spacing, Overrides, Lanes, d, Backs);
        }

        /// <summary>Places a backwards wire's runs; null or no runs makes them automatic again.</summary>
        public FileSettings WithBack(string wireId, BackRuns runs)
        {
            var d = Copy(Backs);
            if (runs == null || runs.IsEmpty) d.Remove(wireId);
            else d[wireId] = runs;
            return new FileSettings(Mode, Spacing, Overrides, Lanes, Points, d);
        }

        /// <summary>Back to automatic: forgets the wires' lane positions, points and back runs, keeps their style.</summary>
        public FileSettings WithoutEdits(IEnumerable<string> wireIds)
        {
            var lanes = Copy(Lanes);
            var points = Copy(Points);
            var backs = Copy(Backs);
            foreach (string id in wireIds)
            {
                lanes.Remove(id);
                points.Remove(id);
                backs.Remove(id);
            }
            return new FileSettings(Mode, Spacing, Overrides, lanes, points, backs);
        }

        /// <summary>
        /// Back to automatic, and for every back wire among them, the out run it shares with the
        /// other back wires from its output too. <paramref name="backGroups"/> gives the output of
        /// each wire that runs backwards.
        /// </summary>
        public FileSettings WithoutEdits(IEnumerable<string> wireIds, IReadOnlyDictionary<string, string> backGroups)
        {
            var ids = wireIds.ToList();
            var outputs = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
                if (backGroups.TryGetValue(id, out string output)) outputs.Add(output);
            var backs = Copy(Backs);
            foreach (var kv in Backs)
                if (backGroups.TryGetValue(kv.Key, out string output) && outputs.Contains(output))
                {
                    var runs = kv.Value.With(BackRun.Out, null);
                    if (runs.IsEmpty) backs.Remove(kv.Key);
                    else backs[kv.Key] = runs;
                }
            return new FileSettings(Mode, Spacing, Overrides, Lanes, Points, backs).WithoutEdits(ids);
        }

        /// <summary>Drops everything kept for wires that no longer exist, so files do not collect dead ids.</summary>
        public FileSettings Pruned(ICollection<string> liveWireIds)
        {
            Dictionary<string, T> Keep<T>(IReadOnlyDictionary<string, T> source)
            {
                var d = Copy(source);
                foreach (string id in source.Keys)
                    if (!liveWireIds.Contains(id)) d.Remove(id);
                return d;
            }
            return new FileSettings(Mode, Spacing, Keep(Overrides), Keep(Lanes), Keep(Points), Keep(Backs));
        }

        static Dictionary<string, T> Copy<T>(IReadOnlyDictionary<string, T> source) =>
            source == null ? new Dictionary<string, T>(StringComparer.Ordinal) : new Dictionary<string, T>(source, StringComparer.Ordinal);
    }

    public enum SettingsStatus { Missing, Ok, Broken, TooNew }

    /// <summary>The settings as stored in GH_Document.ValueTable under <see cref="Key"/>.</summary>
    public static class SettingsJson
    {
        public const string Key = "StraightWire.v1";
        public const int Version = 1;

        public static double ClampSpacing(double s) =>
            double.IsNaN(s) ? Routing.DefaultSpacing
            : s < Routing.MinSpacing ? Routing.MinSpacing
            : s > Routing.MaxSpacing ? Routing.MaxSpacing
            : s;

        public static string Serialize(FileSettings s)
        {
            using (var stream = new MemoryStream())
            {
                // Relaxed escaping keeps wire ids readable: "a>b", not "a>b".
                using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
                {
                    w.WriteStartObject();
                    w.WriteNumber("v", Version);
                    w.WriteString("mode", StyleNames.ToToken(s.Mode));
                    w.WriteNumber("spacing", s.Spacing);
                    w.WriteStartObject("wires");
                    foreach (var kv in s.Overrides.OrderBy(k => k.Key, StringComparer.Ordinal))
                        w.WriteString(kv.Key, StyleNames.ToToken(kv.Value));
                    w.WriteEndObject();
                    if (s.Lanes.Count > 0)
                    {
                        w.WriteStartObject("lanes");
                        foreach (var kv in s.Lanes.OrderBy(k => k.Key, StringComparer.Ordinal)) w.WriteNumber(kv.Key, kv.Value);
                        w.WriteEndObject();
                    }
                    if (s.Points.Count > 0)
                    {
                        w.WriteStartObject("points");
                        foreach (var kv in s.Points.OrderBy(k => k.Key, StringComparer.Ordinal))
                        {
                            w.WriteStartObject(kv.Key);
                            w.WriteStartArray("at");
                            w.WriteNumberValue(kv.Value.SourceAt.X);
                            w.WriteNumberValue(kv.Value.SourceAt.Y);
                            w.WriteNumberValue(kv.Value.TargetAt.X);
                            w.WriteNumberValue(kv.Value.TargetAt.Y);
                            w.WriteEndArray();
                            w.WriteStartArray("p");
                            foreach (var p in kv.Value.Points)
                            {
                                w.WriteNumberValue(p.X);
                                w.WriteNumberValue(p.Y);
                            }
                            w.WriteEndArray();
                            w.WriteEndObject();
                        }
                        w.WriteEndObject();
                    }
                    if (s.Backs.Count > 0)
                    {
                        w.WriteStartObject("backs");
                        foreach (var kv in s.Backs.OrderBy(k => k.Key, StringComparer.Ordinal))
                        {
                            w.WriteStartObject(kv.Key);
                            if (kv.Value.Out is double o) w.WriteNumber("out", o);
                            if (kv.Value.In is double i) w.WriteNumber("in", i);
                            if (kv.Value.Cross is double c) w.WriteNumber("cross", c);
                            w.WriteEndObject();
                        }
                        w.WriteEndObject();
                    }
                    w.WriteEndObject();
                }
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        /// <summary>
        /// Reads stored settings. Missing: nothing stored. Broken: not readable as version-1
        /// settings. TooNew: written by a newer plugin, which must not be overwritten. Unknown
        /// style names inside "wires" are skipped rather than failing the whole file.
        /// </summary>
        /// <summary>A "points" entry: {"at":[sx,sy,tx,ty],"p":[x1,y1,…]}; null if it is malformed or has no points.</summary>
        static ManualPoints ReadPoints(JsonElement e)
        {
            if (e.ValueKind != JsonValueKind.Object) return null;
            if (!e.TryGetProperty("at", out var atElement) || !e.TryGetProperty("p", out var pElement)) return null;
            var at = JsonNumbers.Of(atElement);
            var p = JsonNumbers.Of(pElement);
            if (at == null || at.Count != 4 || p == null || p.Count == 0 || p.Count % 2 != 0) return null;
            var pts = new List<Pt>(p.Count / 2);
            for (int i = 0; i < p.Count; i += 2) pts.Add(new Pt(p[i], p[i + 1]));
            return new ManualPoints(pts, new Pt(at[0], at[1]), new Pt(at[2], at[3]));
        }

        /// <summary>A "backs" entry: {"out":…,"in":…,"cross":…}, any part missing; null if no part is a number.</summary>
        static BackRuns ReadBack(JsonElement e)
        {
            if (e.ValueKind != JsonValueKind.Object) return null;
            double? Part(string name) =>
                e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : (double?)null;
            var runs = new BackRuns(Part("out"), Part("in"), Part("cross"));
            return runs.IsEmpty ? null : runs;
        }

        public static SettingsStatus Parse(string json, out FileSettings settings)
        {
            settings = null;
            if (string.IsNullOrWhiteSpace(json)) return SettingsStatus.Missing;
            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object) return SettingsStatus.Broken;
                    if (!root.TryGetProperty("v", out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out int version))
                        return SettingsStatus.Broken;
                    if (version > Version) return SettingsStatus.TooNew;
                    if (version < 1) return SettingsStatus.Broken;
                    if (!root.TryGetProperty("mode", out var m) || m.ValueKind != JsonValueKind.String ||
                        !StyleNames.TryParseToken(m.GetString(), out var mode))
                        return SettingsStatus.Broken;
                    double spacing = Routing.DefaultSpacing;
                    if (root.TryGetProperty("spacing", out var sp))
                    {
                        if (sp.ValueKind != JsonValueKind.Number) return SettingsStatus.Broken;
                        spacing = sp.GetDouble();
                    }
                    var overrides = new Dictionary<string, WireStyle>(StringComparer.Ordinal);
                    if (root.TryGetProperty("wires", out var wires) && wires.ValueKind == JsonValueKind.Object)
                        foreach (var p in wires.EnumerateObject())
                            if (p.Value.ValueKind == JsonValueKind.String && StyleNames.TryParseToken(p.Value.GetString(), out var style))
                                overrides[p.Name] = style;
                    var lanes = new Dictionary<string, double>(StringComparer.Ordinal);
                    if (root.TryGetProperty("lanes", out var laneObj) && laneObj.ValueKind == JsonValueKind.Object)
                        foreach (var p in laneObj.EnumerateObject())
                            if (p.Value.ValueKind == JsonValueKind.Number) lanes[p.Name] = Geo.Clamp(p.Value.GetDouble(), 0, 1);
                    var points = new Dictionary<string, ManualPoints>(StringComparer.Ordinal);
                    if (root.TryGetProperty("points", out var pointObj) && pointObj.ValueKind == JsonValueKind.Object)
                        foreach (var p in pointObj.EnumerateObject())
                            if (ReadPoints(p.Value) is ManualPoints manual) points[p.Name] = manual;
                    var backs = new Dictionary<string, BackRuns>(StringComparer.Ordinal);
                    if (root.TryGetProperty("backs", out var backObj) && backObj.ValueKind == JsonValueKind.Object)
                        foreach (var p in backObj.EnumerateObject())
                            if (ReadBack(p.Value) is BackRuns runs) backs[p.Name] = runs;
                    settings = new FileSettings(mode, spacing, overrides, lanes, points, backs);
                    return SettingsStatus.Ok;
                }
            }
            catch (JsonException)
            {
                return SettingsStatus.Broken;
            }
        }
    }

    static class JsonNumbers
    {
        /// <summary>The numbers of a JSON array, or null if it is not an array of numbers only.</summary>
        public static List<double> Of(JsonElement e)
        {
            if (e.ValueKind != JsonValueKind.Array) return null;
            var list = new List<double>();
            foreach (var item in e.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Number) return null;
                list.Add(item.GetDouble());
            }
            return list;
        }
    }

    public static class StyleNames
    {
        public static string ToToken(WireStyle s) =>
            s == WireStyle.Curve ? "bezier" : s == WireStyle.Orthogonal ? "ortho" : "diag";

        public static bool TryParseToken(string token, out WireStyle style)
        {
            switch (token)
            {
                case "bezier": style = WireStyle.Curve; return true;
                case "ortho": style = WireStyle.Orthogonal; return true;
                case "diag": style = WireStyle.Diagonal; return true;
                default: style = WireStyle.Diagonal; return false;
            }
        }

        public static string Label(WireStyle s) =>
            s == WireStyle.Curve ? "Curve" : s == WireStyle.Orthogonal ? "Orthogonal" : "45°";

        /// <summary>The canvas widget's narrower button text.</summary>
        public static string ShortLabel(WireStyle s) => s == WireStyle.Orthogonal ? "Ortho" : Label(s);

        /// <summary>The marker component's message, e.g. "45° · 10".</summary>
        public static string Summary(FileSettings s) =>
            Label(s.Mode) + " · " + s.Spacing.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
