using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace Procurement.UI.Support
{
    // Letterlijke kopie van UniPro2026's UI/Support/ClassicStyle.cs (16 nov 2026, user request:
    // Procurement-schermen via de Designer opbouwen zoals UniPro, zodat ze later zonder verschil
    // samengevoegd kunnen worden). Schermen die al naar Designer.cs zijn omgezet roepen
    // ClassicStyle.ApplyClassicStyle(this) aan direct na InitializeComponent(), precies zoals in
    // UniPro. De nog niet omgezette, in code opgebouwde schermen gebruiken voorlopig nog
    // GridFormatting's eigen helpers. Bij wijzigingen: in beide apps gelijk houden.
    public static class ClassicStyle
    {
        public static readonly Color ClassicScreenBackColor = Color.FromArgb(230, 240, 250);

        private static readonly Color ClassicBorderColor = Color.FromArgb(120, 160, 210);

        private const int ButtonCornerRadius = 6;

        public static void ApplyClassicButtonStyle(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            // BorderSize blijft 0 - de stock FlatAppearance-rand tekent een vierkant, en de
            // Region-clip hieronder (voor de afgeronde hoeken) sneed daar de hoeken vanaf: een
            // rand die in elke hoek onderbroken was (user-report 23 okt 2026: "De rand is niet
            // gesloten, in alle hoeken is hij onderbroken"). De rand wordt in plaats daarvan zelf
            // getekend (OnButtonPaint hieronder), als één doorlopend pad dat exact overeenkomt
            // met de afgeronde clip-regio, dus zonder die mismatch.
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(222, 234, 246);
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(193, 218, 244);
            button.BackColor = Color.White;

            // Geen Padding hier (eerdere versie zette 6,2,6,2) - deze knoppen hebben in de
            // Designer een vaste, krappe Size die precies op de tekst is afgestemd, dus padding
            // eet in op de beschikbare tekstruimte i.p.v. 'm te geven (user-report 23 okt 2026:
            // "valt wat tekst weg op een heleboel plaatsen").

            // Ronde hoeken (user-report 23 okt 2026) via een Region-clip i.p.v. custom-painten
            // zoals ClassicGroupBox - Button is hier geen eigen subclass (anders dan GroupBox
            // hoefde het veldtype in Designer.cs niet aangepast te worden), dus de region en de
            // rand worden hier, runtime, op de bestaande Button-instantie gezet. SizeChanged
            // erbij zodat een Anchor/Dock-herschikking de afronding niet verpest.
            ApplyRoundedRegion(button);
            button.SizeChanged -= OnButtonSizeChanged;
            button.SizeChanged += OnButtonSizeChanged;
            button.Paint -= OnButtonPaint;
            button.Paint += OnButtonPaint;
        }

        private static void OnButtonSizeChanged(object sender, EventArgs e)
        {
            ApplyRoundedRegion((Control)sender);
        }

        private static void OnButtonPaint(object sender, PaintEventArgs e)
        {
            var button = (Control)sender;
            if (button.Width <= 0 || button.Height <= 0)
                return;

            // 1px naar binnen t.o.v. de Region (die (0,0,Width,Height) gebruikt) zodat de
            // pen-stroke (die zelf ook ~1px breed is, half aan weerszijden van het pad) volledig
            // binnen de clip-regio valt - anders wordt de buitenste helft van de pen alsnog
            // weggesneden en oogt de rand half doorzichtig/kartelig.
            var bounds = new Rectangle(0, 0, button.Width - 1, button.Height - 1);
            int r = Math.Min(ButtonCornerRadius, Math.Min(bounds.Width, bounds.Height) / 2);

            var oldMode = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            using (var path = RoundedRectPath(bounds, r))
            using (var pen = new Pen(ClassicBorderColor))
            {
                e.Graphics.DrawPath(pen, path);
            }

            e.Graphics.SmoothingMode = oldMode;
        }

        private static void ApplyRoundedRegion(Control control)
        {
            if (control.Width <= 0 || control.Height <= 0)
                return;

            var rect = new Rectangle(0, 0, control.Width, control.Height);
            int r = Math.Min(ButtonCornerRadius, Math.Min(rect.Width, rect.Height) / 2);

            using (var path = RoundedRectPath(rect, r))
            {
                var oldRegion = control.Region;
                control.Region = new Region(path);
                oldRegion?.Dispose();
            }
        }

        private static GraphicsPath RoundedRectPath(Rectangle rect, int r)
        {
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, r * 2, r * 2, 180, 90);
            path.AddArc(rect.Right - r * 2, rect.Y, r * 2, r * 2, 270, 90);
            path.AddArc(rect.Right - r * 2, rect.Bottom - r * 2, r * 2, r * 2, 0, 90);
            path.AddArc(rect.X, rect.Bottom - r * 2, r * 2, r * 2, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void ApplyClassicHeaderStyle(DataGridView grid)
        {
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(205, 221, 240);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(205, 221, 240);
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.Black;

            // Rijkoppen (links, meestal de kolom met de ">" rij-aanwijzer) bleven op de standaard
            // grijze systeemkleur staan terwijl de kolomkoppen al blauw waren (user-report 3 nov
            // 2026). EnableHeadersVisualStyles=false hierboven geldt voor beide headertypes, dus
            // RowHeadersDefaultCellStyle hoeft alleen nog expliciet dezelfde kleuren te krijgen.
            grid.RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(205, 221, 240);
            grid.RowHeadersDefaultCellStyle.ForeColor = Color.Black;
            grid.RowHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(205, 221, 240);
            grid.RowHeadersDefaultCellStyle.SelectionForeColor = Color.Black;
        }

        // DataGridView.DoubleBuffered is protected - reflection is the standard workaround, veilig
        // als no-op als een toekomstige .NET Framework-versie de property ooit zou hernoemen/verwijderen.
        public static void EnableDoubleBuffering(DataGridView grid)
        {
            var property = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            property?.SetValue(grid, true, null);
        }

        // Loopt recursief door de hele control-boom (Panel/GroupBox/TabControl/SplitContainer/...)
        // en past de classic-stijl toe op elke Button/DataGridView die ze tegenkomt - zo hoeft niet
        // elk Designer.cs-bestand met de hand per control aangepast te worden.
        public static void ApplyClassicStyle(Control root)
        {
            if (root == null)
                return;

            foreach (Control control in root.Controls)
            {
                if (control == null || control.IsDisposed)
                    continue;

                if (control is Button button)
                {
                    ApplyClassicButtonStyle(button);
                }
                else if (control is DataGridView grid)
                {
                    ApplyClassicHeaderStyle(grid);
                    EnableDoubleBuffering(grid);
                }

                ApplyClassicStyle(control);
            }
        }

        // Stock GroupBox-rand wordt getekend door de actieve Windows visual style en negeert
        // ForeColor/BackColor volledig - dus de rand hier met de hand tekenen i.p.v. een property
        // zetten, zelfde techniek als Procurement.
        public sealed class ClassicGroupBox : GroupBox
        {
            private const int CornerRadius = 6;

            protected override void OnPaint(PaintEventArgs e)
            {
                var textSize = TextRenderer.MeasureText(Text, Font);
                var top = textSize.Height / 2;
                var titleGap = string.IsNullOrEmpty(Text) ? 0 : textSize.Width + 6;
                var resumeX = Math.Min(8 + titleGap, Width - 1);

                var left = 0;
                var right = Width - 1;
                var bottom = Height - 1;
                var r = CornerRadius;

                using (var borderPen = new Pen(ClassicBorderColor))
                {
                    e.Graphics.SmoothingMode = SmoothingMode.Default;
                    e.Graphics.DrawLine(borderPen, left + r, top, titleGap > 0 ? 8 : right - r, top);
                    if (titleGap > 0)
                        e.Graphics.DrawLine(borderPen, resumeX, top, right - r, top);
                    e.Graphics.DrawLine(borderPen, right, top + r, right, bottom - r);
                    e.Graphics.DrawLine(borderPen, right - r, bottom, left + r, bottom);
                    e.Graphics.DrawLine(borderPen, left, bottom - r, left, top + r);

                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.DrawArc(borderPen, left, top, r * 2, r * 2, 180, 90);
                    e.Graphics.DrawArc(borderPen, right - r * 2, top, r * 2, r * 2, 270, 90);
                    e.Graphics.DrawArc(borderPen, right - r * 2, bottom - r * 2, r * 2, r * 2, 0, 90);
                    e.Graphics.DrawArc(borderPen, left, bottom - r * 2, r * 2, r * 2, 90, 90);
                }

                if (!string.IsNullOrEmpty(Text))
                    using (var textBrush = new SolidBrush(ForeColor))
                        e.Graphics.DrawString(Text, Font, textBrush, 8, 0);
            }
        }

        // Lichtblauwe menubalk zoals MAX's eigen MRP-menu - alleen de kleuren die dat scherm
        // daadwerkelijk toont worden overschreven, de rest valt terug op ProfessionalColorTable's
        // eigen defaults.
        public sealed class ClassicBlueColorTable : ProfessionalColorTable
        {
            public override Color MenuStripGradientBegin => Color.FromArgb(222, 234, 246);
            public override Color MenuStripGradientEnd => Color.FromArgb(185, 211, 238);
            public override Color MenuBorder => Color.FromArgb(120, 160, 210);
            public override Color MenuItemBorder => Color.FromArgb(120, 160, 210);
            public override Color MenuItemSelected => Color.FromArgb(193, 218, 244);
            public override Color MenuItemSelectedGradientBegin => Color.FromArgb(193, 218, 244);
            public override Color MenuItemSelectedGradientEnd => Color.FromArgb(193, 218, 244);
            public override Color MenuItemPressedGradientBegin => Color.FromArgb(155, 190, 230);
            public override Color MenuItemPressedGradientEnd => Color.FromArgb(155, 190, 230);
            public override Color ImageMarginGradientBegin => Color.White;
            public override Color ImageMarginGradientMiddle => Color.White;
            public override Color ImageMarginGradientEnd => Color.White;
        }
    }
}
