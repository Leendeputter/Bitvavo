using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace Procurement.UI.Support
{
    /// <summary>
    /// Remembers DataGridView column widths per Windows user (user request, 13 nov 2026: "grid-breedtes
    /// onthouden zoals in UniPro"). Same storage and key format as UniPro2026's
    /// Infrastructure/Settings/GridColumnSettingsHelper.cs: one user-scoped setting
    /// "GridColumnWidths" (user.config, local per Windows user, not in the database) holding
    /// "{FormName}.{GridName}.{ColumnName}=width;..." — so the two merge without a format change.
    ///
    /// Differences from UniPro, because Procurement builds its screens in code rather than the Designer:
    ///  - One call per form: <see cref="Attach"/> at the end of the constructor wires everything
    ///    (restore now, restore again after every DataSource binding, save on FormClosing). Most
    ///    Procurement grids regenerate their columns on each refresh, so restoring only once (as
    ///    UniPro's RestoreAllColumnWidths does for Designer columns) wouldn't stick.
    ///  - Only columns the user actually resized are stored (dragged or double-clicked divider), plus
    ///    any already stored. Many Procurement columns are Fill-mode on purpose; storing every column's
    ///    current width would freeze those too and leave the grid not filling a wider window.
    ///  - Grids need a Name (code-built grids have none by default); each form sets one.
    /// </summary>
    public static class GridColumnSettingsHelper
    {
        private sealed class GridState
        {
            public string FormName;
            public readonly HashSet<string> UserResizedColumns = new HashSet<string>(StringComparer.Ordinal);
            public bool Restoring;
        }

        private static readonly ConditionalWeakTable<DataGridView, GridState> States =
            new ConditionalWeakTable<DataGridView, GridState>();

        /// <summary>Call at the end of a form's constructor, after its grids exist.</summary>
        public static void Attach(Form form)
        {
            var formName = form.GetType().Name;
            var settings = Load();

            foreach (var grid in FindGrids(form))
            {
                var state = new GridState { FormName = formName };
                States.Add(grid, state);

                grid.ColumnWidthChanged += (s, e) =>
                {
                    // Only a drag by the user counts — not our own restore, not the form's own
                    // SetCharacterBasedColumnWidth calls, not Fill-mode reflow on a window resize.
                    if (!state.Restoring && Control.MouseButtons == MouseButtons.Left && IsDraggingDividerOf(grid, e.Column))
                        state.UserResizedColumns.Add(e.Column.Name);
                };
                grid.ColumnDividerDoubleClick += (s, e) =>
                {
                    if (e.ColumnIndex >= 0)
                        state.UserResizedColumns.Add(grid.Columns[e.ColumnIndex].Name);
                };

                // Posted rather than run inline: forms set DataSource and then apply their own
                // default widths right after; the restore has to come last to win.
                grid.DataBindingComplete += (s, e) =>
                {
                    if (grid.IsHandleCreated)
                        grid.BeginInvoke((Action)(() => Restore(grid, state, Load())));
                    else
                        Restore(grid, state, Load());
                };

                Restore(grid, state, settings);
            }

            form.FormClosing += (s, e) => SaveAll(form);
        }

        /// <summary>
        /// True when the mouse is in the column header row, at this column's right edge — i.e. this is
        /// the column whose divider is being dragged. Without this, the Fill-mode neighbours that
        /// shrink/grow along with it (or every column while the window border is dragged) would also
        /// count as "resized by the user" and get frozen at their current width.
        /// </summary>
        private static bool IsDraggingDividerOf(DataGridView grid, DataGridViewColumn column)
        {
            if (!column.Visible || column.Index < 0) return false;

            var mouse = grid.PointToClient(Control.MousePosition);
            if (mouse.Y < 0 || mouse.Y > grid.ColumnHeadersHeight) return false;

            var rect = grid.GetColumnDisplayRectangle(column.Index, false);
            if (rect.Width == 0) return false;

            var rightEdge = grid.RightToLeft == RightToLeft.Yes ? rect.Left : rect.Right;
            return Math.Abs(mouse.X - rightEdge) <= 12;
        }

        private static void Restore(DataGridView grid, GridState state, Dictionary<string, string> settings)
        {
            if (grid.IsDisposed) return;

            state.Restoring = true;
            try
            {
                foreach (DataGridViewColumn column in grid.Columns)
                {
                    if (settings.TryGetValue(Key(state.FormName, grid, column), out var value)
                        && int.TryParse(value, out var width) && width > 0)
                    {
                        column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                        column.Width = Math.Max(width, column.MinimumWidth);
                    }
                }
            }
            finally
            {
                state.Restoring = false;
            }
        }

        private static void SaveAll(Form form)
        {
            var settings = Load();
            var changed = false;

            foreach (var grid in FindGrids(form))
            {
                if (!States.TryGetValue(grid, out var state)) continue;

                foreach (DataGridViewColumn column in grid.Columns)
                {
                    var key = Key(state.FormName, grid, column);
                    if (state.UserResizedColumns.Contains(column.Name) || settings.ContainsKey(key))
                    {
                        settings[key] = column.Width.ToString();
                        changed = true;
                    }
                }
            }

            if (changed)
                Save(settings);
        }

        private static string Key(string formName, DataGridView grid, DataGridViewColumn column) =>
            $"{formName}.{grid.Name}.{column.Name}";

        private static IEnumerable<DataGridView> FindGrids(Control root)
        {
            foreach (Control child in root.Controls)
            {
                if (child is DataGridView grid)
                    yield return grid;

                foreach (var nested in FindGrids(child))
                    yield return nested;
            }
        }

        private static Dictionary<string, string> Load()
        {
            var dict = new Dictionary<string, string>();
            var data = UserLayoutSettings.Default.GridColumnWidths;
            if (string.IsNullOrEmpty(data))
                return dict;

            foreach (var entry in data.Split(';'))
            {
                var parts = entry.Split('=');
                if (parts.Length == 2)
                    dict[parts[0]] = parts[1];
            }

            return dict;
        }

        private static void Save(Dictionary<string, string> dict)
        {
            try
            {
                UserLayoutSettings.Default.GridColumnWidths = string.Join(";", dict.Select(x => $"{x.Key}={x.Value}"));
                UserLayoutSettings.Default.Save();
            }
            catch (ConfigurationException)
            {
                // A corrupt/locked user.config must never block closing a screen; widths are a convenience.
            }
        }
    }

    /// <summary>
    /// User-scoped settings (user.config under %LOCALAPPDATA%), the code equivalent of UniPro's
    /// Properties.Settings.Default.GridColumnWidths — Procurement.UI has no Settings.settings designer file.
    /// </summary>
    internal sealed class UserLayoutSettings : ApplicationSettingsBase
    {
        public static UserLayoutSettings Default { get; } = (UserLayoutSettings)Synchronized(new UserLayoutSettings());

        [UserScopedSetting]
        [DefaultSettingValue("")]
        public string GridColumnWidths
        {
            get => (string)this[nameof(GridColumnWidths)];
            set => this[nameof(GridColumnWidths)] = value;
        }
    }
}
