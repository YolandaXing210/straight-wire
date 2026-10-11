using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;
using Grasshopper;
using Rhino;
using StraightWire.Core;

namespace StraightWire.UI
{
    /// <summary>
    /// A Straight Wire menu in Grasshopper's menu bar. The editor window does not exist yet when
    /// the plugin loads, so the menu waits on Rhino's idle ticks until it does. Item text and
    /// checks are refreshed each time the menu opens, so they always reflect the selection.
    /// </summary>
    internal static class StyleMenu
    {
        static readonly WireStyle[] Styles = { WireStyle.Curve, WireStyle.Orthogonal, WireStyle.Diagonal };
        static ToolStripMenuItem _menu;

        public static void AddWhenReady() => RhinoApp.Idle += OnIdle;

        public static void Remove()
        {
            RhinoApp.Idle -= OnIdle;
            if (_menu == null) return;
            var menu = _menu;
            _menu = null;
            Log.Guard("menu-remove", () => menu.Owner?.Items.Remove(menu));
        }

        static void OnIdle(object sender, EventArgs e)
        {
            var editor = Instances.DocumentEditor;
            if (editor?.MainMenuStrip == null) return;
            RhinoApp.Idle -= OnIdle;
            if (_menu != null) return;
            Log.Guard("menu-add", () =>
            {
                _menu = Build();
                editor.MainMenuStrip.Items.Add(_menu);
            });
        }

        static ToolStripMenuItem Build()
        {
            var menu = new ToolStripMenuItem("Straight Wire");

            var styleItems = new List<(WireStyle Style, ToolStripMenuItem Item)>();
            foreach (var style in Styles)
            {
                var s = style;
                var item = new ToolStripMenuItem(StyleNames.Label(s))
                {
                    ToolTipText = "Draw wires as " + StyleNames.Label(s) + ". With components selected, only their wires change.",
                };
                item.Click += (o, e) => Commands.SetStyle(s);
                menu.DropDownItems.Add(item);
                styleItems.Add((s, item));
            }
            menu.DropDownItems.Add(new ToolStripSeparator());

            var spacing = new ToolStripMenuItem("Wire Spacing");
            foreach (double option in Routing.SpacingOptions)
            {
                double v = option;
                var item = new ToolStripMenuItem(v.ToString(CultureInfo.InvariantCulture));
                item.Click += (o, e) => Commands.SetSpacing(v);
                spacing.DropDownItems.Add(item);
            }
            menu.DropDownItems.Add(spacing);

            var follow = new ToolStripMenuItem("Follow Global") { ToolTipText = "The selected components' wires go back to the file's style." };
            follow.Click += (o, e) => Commands.FollowGlobal();
            menu.DropDownItems.Add(follow);
            menu.DropDownItems.Add(new ToolStripSeparator());

            var save = new ToolStripMenuItem("Save to This File")
            {
                ToolTipText = "Store the wire styles in this file, so anyone with Straight Wire sees the same wires.",
            };
            save.Click += (o, e) => Commands.SaveToFile();
            menu.DropDownItems.Add(save);

            var mine = new ToolStripMenuItem("Make This My Default")
            {
                ToolTipText = "Use this file's style and spacing for files that have none of their own.",
            };
            mine.Click += (o, e) => Commands.MakeMyDefault();
            menu.DropDownItems.Add(mine);

            var reset = new ToolStripMenuItem("Reset Selected Wires")
            {
                ToolTipText = "Selected wires (and wires on selected components) lose their hand edits and route automatically again.",
            };
            reset.Click += (o, e) => Commands.ResetSelected();
            menu.DropDownItems.Insert(menu.DropDownItems.IndexOf(follow) + 1, reset);

            menu.DropDownOpening += (o, e) => Log.Guard("menu-open", () =>
            {
                var doc = Commands.ActiveDocument;
                var scope = Commands.ScopeOf(doc);
                var effective = doc == null ? null : DocStore.Get(doc).Effective;
                string suffix = scope.SelectedWires == 0 ? ""
                              : " (" + scope.SelectedWires + " selected wire" + (scope.SelectedWires == 1 ? ")" : "s)");
                foreach (var (s, item) in styleItems)
                {
                    item.Text = StyleNames.Label(s) + suffix;
                    item.Checked = scope.Active == s;
                    item.Enabled = scope.CanWrite;
                }
                foreach (ToolStripMenuItem item in spacing.DropDownItems)
                    item.Checked = effective != null && item.Text == effective.Spacing.ToString(CultureInfo.InvariantCulture);
                spacing.Enabled = scope.CanWrite;
                follow.Enabled = scope.CanWrite && scope.SelectedWires > 0;
                save.Enabled = scope.CanWrite;
                mine.Enabled = doc != null;
                reset.Enabled = scope.CanWrite && scope.SelectedWires > 0;
            });

            return menu;
        }
    }
}
