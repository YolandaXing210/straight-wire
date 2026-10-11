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
    /// <summary>A file's wire settings: its global style, its spacing, and per-wire overrides by wire id. Immutable.</summary>
    public sealed class FileSettings
    {
        static readonly IReadOnlyDictionary<string, WireStyle> None = new Dictionary<string, WireStyle>(StringComparer.Ordinal);

        public FileSettings(WireStyle mode, double spacing, IReadOnlyDictionary<string, WireStyle> overrides = null)
        {
            Mode = mode;
            Spacing = SettingsJson.ClampSpacing(spacing);
            Overrides = overrides == null || overrides.Count == 0
                ? None
                : new Dictionary<string, WireStyle>(overrides, StringComparer.Ordinal);
        }

        public WireStyle Mode { get; }
        public double Spacing { get; }
        public IReadOnlyDictionary<string, WireStyle> Overrides { get; }

        public WireStyle StyleOf(string wireId) => Overrides.TryGetValue(wireId, out var s) ? s : Mode;

        public FileSettings WithMode(WireStyle mode) => new FileSettings(mode, Spacing, Overrides);

        public FileSettings WithSpacing(double spacing) => new FileSettings(Mode, spacing, Overrides);

        public FileSettings WithOverride(IEnumerable<string> wireIds, WireStyle style)
        {
            var d = Copy();
            foreach (string id in wireIds) d[id] = style;
            return new FileSettings(Mode, Spacing, d);
        }

        public FileSettings WithoutOverride(IEnumerable<string> wireIds)
        {
            var d = Copy();
            foreach (string id in wireIds) d.Remove(id);
            return new FileSettings(Mode, Spacing, d);
        }

        /// <summary>Drops overrides whose wire no longer exists, so files do not collect dead ids.</summary>
        public FileSettings Pruned(ICollection<string> liveWireIds)
        {
            var d = Copy();
            foreach (string id in Overrides.Keys)
                if (!liveWireIds.Contains(id)) d.Remove(id);
            return new FileSettings(Mode, Spacing, d);
        }

        Dictionary<string, WireStyle> Copy() => new Dictionary<string, WireStyle>(Overrides, StringComparer.Ordinal);
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
                    settings = new FileSettings(mode, spacing, overrides);
                    return SettingsStatus.Ok;
                }
            }
            catch (JsonException)
            {
                return SettingsStatus.Broken;
            }
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
