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
    }
}
