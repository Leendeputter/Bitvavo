using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace Procurement.UI.Support
{
    /// <summary>
    /// Single place for the DataGridView conventions this app wants consistent everywhere: quantity
    /// fields at 4 decimals, cost/price fields at 4 decimals with a € prefix, other plain decimals
    /// (e.g. a conversion factor) at a caller-chosen precision, dates as d-MM-yyyy, timestamps as
    /// d-MM-yyyy HH:mm:ss (unlike a pure date, an audit/created timestamp's time-of-day is actually
    /// meaningful, so that one keeps it instead of collapsing to date-only) — all right-aligned. Used
    /// by every Form with a DataGridView instead of duplicating format strings per screen.
    /// </summary>
    internal static class GridFormatting
    {
        private const string DateFormat = "d-MM-yyyy";
        private const string DateTimeFormat = "d-MM-yyyy HH:mm:ss";
        private const string QuantityFormat = "N4";
        private const string CurrencyFormat = "€ #,##0.0000";

        /// <summary>
        /// Pale-blue "screen" background matching MAX's own MRP screens (user-supplied screenshot,
        /// 20 okt 2026). Replaces an earlier attempt that set BackColor = SystemColors.Control
        /// explicitly — which made no visible difference at all, since that's already the implicit
        /// WinForms default, and the user correctly flagged it ("De achtergrond van de schermen is
        /// niet gewijzigd... dus lichtblauwachtig"). A shade lighter than
        /// ApplyClassicHeaderStyle's grid-header blue below, so headers still read as a distinct band
        /// rather than blending into the form itself.
        /// </summary>
        public static readonly Color ClassicScreenBackColor = Color.FromArgb(230, 240, 250);

        public static void ApplyQuantityColumns(DataGridView grid, params string[] columnNames)
        {
            foreach (var name in columnNames) ApplyFormat(grid, name, QuantityFormat);
        }

        public static void ApplyCurrencyColumns(DataGridView grid, params string[] columnNames)
        {
            foreach (var name in columnNames) ApplyFormat(grid, name, CurrencyFormat);
        }

        public static void ApplyDecimalColumns(DataGridView grid, int decimals, params string[] columnNames)
        {
            foreach (var name in columnNames) ApplyFormat(grid, name, "N" + decimals);
        }

        /// <summary>Pure calendar dates (due date, delivery date, ...) — time-of-day is never meaningful for these, so it's dropped rather than shown as a misleading 00:00.</summary>
        public static void ApplyDateColumns(DataGridView grid, params string[] columnNames)
        {
            foreach (var name in columnNames) ApplyFormat(grid, name, DateFormat);
        }

        /// <summary>Actual timestamps (CreatedAt, audit-log Timestamp, ...) where time-of-day matters.</summary>
        public static void ApplyDateTimeColumns(DataGridView grid, params string[] columnNames)
        {
            foreach (var name in columnNames) ApplyFormat(grid, name, DateTimeFormat);
        }

        private static void ApplyFormat(DataGridView grid, string columnName, string format)
        {
            var column = grid.Columns[columnName];
            if (column == null) return;
            column.DefaultCellStyle.Format = format;
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        public static void SetHeaderText(DataGridView grid, string columnName, string headerText)
        {
            if (grid.Columns[columnName] != null)
                grid.Columns[columnName].HeaderText = headerText;
        }

        public static void SetFixedColumnWidth(DataGridView grid, string columnName, int width)
        {
            var column = grid.Columns[columnName];
            if (column == null) return;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.Width = width;
        }

        /// <summary>
        /// A floor under a Fill-mode column so it can never be squeezed toward unreadable once the
        /// grid's fixed-width columns alone add up to more than the viewport — without this,
        /// AutoSizeColumnsMode.Fill just keeps shrinking the remaining Fill columns (down toward a
        /// handful of pixels) instead of ever giving up and showing a horizontal scrollbar for the
        /// overflow, which is what actually happened to Desc1/Reference as more fixed columns were
        /// added over time.
        /// </summary>
        /// <summary>
        /// DataGridViewColumn.MinimumWidth's setter can throw a NullReferenceException from deep
        /// inside WinForms itself (DataGridViewBand.set_Thickness, via set_MinimumThickness) — a
        /// known framework quirk, confirmed hit here (20 okt 2026) opening SettingsForm: a column
        /// still in Fill auto-size mode, on a grid sitting on a TabPage that hasn't been selected yet
        /// (so its handle isn't created), triggers an internal Fill-column recalculation that
        /// dereferences something not yet initialized. Temporarily dropping the column out of Fill
        /// mode for the assignment sidesteps that exact code path; restoring the original mode right
        /// after keeps the usual "Fill, but never below this width" behavior intact once the tab
        /// actually becomes visible.
        /// </summary>
        public static void SetMinimumWidth(DataGridView grid, string columnName, int minimumWidth)
        {
            var column = grid.Columns[columnName];
            if (column == null) return;

            var originalMode = column.AutoSizeMode;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.MinimumWidth = minimumWidth;
            column.AutoSizeMode = originalMode;
        }

        /// <summary>Sizes to N 'n'-characters instead of a guessed pixel width — DataGridView doesn't drop overflow, it ellipsizes ("..."), so a too-narrow guess reads as a trimming bug that isn't one.</summary>
        public static void SetCharacterBasedColumnWidth(DataGridView grid, string columnName, int characterCount)
        {
            var column = grid.Columns[columnName];
            if (column == null) return;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            column.Width = TextRenderer.MeasureText(new string('n', characterCount), grid.Font).Width + 12;
        }

        /// <summary>
        /// DataGridView.DoubleBuffered is protected — it draws cells itself instead of going through
        /// the normal Paint pipeline, so without this every scroll/scrollbar-drag repaints row by
        /// row instead of as one buffered frame. Reflection is the standard way around this (no
        /// supported public API exists); safe to no-op if a future .NET Framework version ever
        /// renames/removes the property.
        /// </summary>
        public static void EnableDoubleBuffering(DataGridView grid)
        {
            var property = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            property?.SetValue(grid, true, null);
        }

        /// <summary>
        /// DataGridView's row header is blank by default (just a selection arrow) — no built-in
        /// property shows a row number there, so this draws the 1-based row number into it on every
        /// repaint (RowPostPaint fires per visible row, same event WinForms itself uses for this
        /// well-known workaround). Subscribed once per grid instance (call this where the grid is
        /// constructed, alongside EnableDoubleBuffering) — the event lives on the DataGridView
        /// control itself, not on its rows, so it survives every later DataSource reassignment
        /// without needing to be re-wired on each refresh.
        /// </summary>
        public static void EnableRowNumberHeaders(DataGridView grid)
        {
            grid.RowPostPaint += (s, e) =>
            {
                var rowNumber = (e.RowIndex + 1).ToString();
                var headerBounds = new Rectangle(e.RowBounds.Left, e.RowBounds.Top, grid.RowHeadersWidth, e.RowBounds.Height);
                TextRenderer.DrawText(e.Graphics, rowNumber, grid.Font, headerBounds, SystemColors.ControlText,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            };
        }

        /// <summary>
        /// Pale-blue column headers matching MAX's own grids (user-supplied screenshot of its "MRP
        /// Detail" form, 20 okt 2026 — "Blauwe grid-headers" was one of the two styling choices
        /// explicitly kept out of the three offered; the form's yellow input-field highlighting was
        /// declined). EnableHeadersVisualStyles must be turned off first — left on, Windows visual
        /// styles paint the header chrome itself and ignore ColumnHeadersDefaultCellStyle entirely.
        /// </summary>
        public static void ApplyClassicHeaderStyle(DataGridView grid)
        {
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(205, 221, 240);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(205, 221, 240);
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.Black;
        }

        /// <summary>Same blue family as ClassicScreenBackColor/ApplyClassicHeaderStyle/ShellForm's menu — the one color this "classic MAX" styling keeps reusing for borders and emphasis.</summary>
        private static readonly Color ClassicBorderColor = Color.FromArgb(120, 160, 210);

        /// <summary>
        /// Flat, thin-bordered button matching MAX's own screens (user-supplied screenshot, 20 okt
        /// 2026 — "de opmaak van de knoppen aanpassen naar de layout op het voorbeeld"). Without this,
        /// Application.EnableVisualStyles() (Program.cs) renders every button with the modern Windows
        /// theme's raised/gradient look, nothing like that screenshot's flat, thin-bordered buttons —
        /// FlatStyle.Flat is what opts a button out of that theme rendering.
        /// </summary>
        public static void ApplyClassicButtonStyle(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = ClassicBorderColor;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(222, 234, 246);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(193, 218, 244);
            button.BackColor = Color.White;
            button.Padding = new Padding(6, 2, 6, 2);
        }

        /// <summary>
        /// GroupBox with the same thin blue border as the rest of this "classic MAX" styling (user
        /// request, 20 okt 2026 — "de rand van groupboxen"). The stock GroupBox border is drawn by
        /// whatever Windows visual style is active and completely ignores ForeColor/BackColor for it,
        /// so matching the color here means drawing the border by hand instead of setting any
        /// property — the standard WinForms technique for a recolored GroupBox.
        /// </summary>
        public sealed class ClassicGroupBox : GroupBox
        {
            protected override void OnPaint(PaintEventArgs e)
            {
                var textSize = e.Graphics.MeasureString(Text, Font);
                var top = (int)(textSize.Height / 2);
                var titleGap = string.IsNullOrEmpty(Text) ? 0 : (int)textSize.Width + 6;

                using (var borderPen = new Pen(ClassicBorderColor))
                {
                    e.Graphics.DrawLine(borderPen, 0, top, titleGap > 0 ? 8 : Width - 1, top);
                    if (titleGap > 0)
                        e.Graphics.DrawLine(borderPen, 8 + titleGap, top, Width - 1, top);
                    e.Graphics.DrawLine(borderPen, Width - 1, top, Width - 1, Height - 1);
                    e.Graphics.DrawLine(borderPen, Width - 1, Height - 1, 0, Height - 1);
                    e.Graphics.DrawLine(borderPen, 0, Height - 1, 0, top);
                }

                if (!string.IsNullOrEmpty(Text))
                    using (var textBrush = new SolidBrush(ForeColor))
                        e.Graphics.DrawString(Text, Font, textBrush, 8, 0);
            }
        }

        /// <summary>
        /// GroupBox.AutoSize with a manually-positioned (non-Dock) child turned out unreliable, so
        /// this reads the child's own (properly computed) PreferredSize and sizes the GroupBox
        /// explicitly around it instead. Dock=Fill for the child (tried in an earlier round) turned
        /// out worse, not better: GroupBox reserves its own built-in space for the border/label on
        /// top of whatever Padding is set, so a Dock=Fill child ends up squeezed into less room than
        /// this method actually sized the box for. A manually-placed (non-Dock) child at an explicit
        /// Location doesn't hit that double reservation, so that's what this goes back to — sized
        /// generously on the right/bottom since content clipping is a much worse failure mode than
        /// a little unused space in the box. Originally MainForm-only; moved here (20 okt 2026) once
        /// OrdersForm needed the same grouped/titled-box look for its new "Include" filter.
        /// </summary>
        public static GroupBox WrapInGroupBox(string title, Control content)
        {
            const int left = 12, top = 20, right = 24, bottom = 20;

            // ClassicGroupBox (user request, 20 okt 2026 — "de rand van groupboxen"), not a plain
            // GroupBox, for the same thin blue border as the rest of this app's classic MAX styling.
            var group = new ClassicGroupBox { Text = title, Margin = new Padding(4) };
            content.Location = new Point(left, top);
            group.Controls.Add(content);

            var contentSize = content.PreferredSize;
            var titleWidth = TextRenderer.MeasureText(title, group.Font).Width + 24;
            group.Size = new Size(Math.Max(contentSize.Width + left + right, titleWidth), contentSize.Height + top + bottom);

            return group;
        }
    }
}
