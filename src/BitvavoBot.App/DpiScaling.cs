namespace BitvavoBot.App;

/// <summary>
/// This UI is built entirely in code (no Designer-generated InitializeComponent), so none of the
/// Forms/UserControls ever got the AutoScaleMode/AutoScaleDimensions baseline the Designer
/// normally emits automatically. Without it, every hardcoded pixel Width/Height stays literal on
/// screen regardless of the system's DPI scaling, which is why controls looked tiny on a
/// high-DPI display. Call this as the first line of every Form/UserControl constructor.
/// </summary>
internal static class DpiScaling
{
    public static void ApplyStandardAutoScale(this ContainerControl control)
    {
        control.AutoScaleMode = AutoScaleMode.Dpi;
        control.AutoScaleDimensions = new SizeF(96F, 96F);
    }

    /// <summary>
    /// Button, CheckBox and RadioButton (all <see cref="ButtonBase"/>) were never given AutoSize
    /// or padding, so they sat at WinForms' old ~23px default height no matter the DPI scale
    /// factor. Call this once, after the control tree has been built (i.e. after BuildLayout()),
    /// to recursively grow every button-like control to fit its text with real breathing room.
    /// </summary>
    public static void ApplyReadableButtonSizing(this Control root)
    {
        foreach (Control child in root.Controls)
        {
            // AutoSizeMode (GrowOnly/GrowAndShrink) only exists on the concrete Button class, not
            // on the shared ButtonBase (CheckBox/RadioButton don't expose it), so it needs its own check.
            if (child is Button button)
            {
                button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            }

            if (child is ButtonBase buttonBase)
            {
                buttonBase.AutoSize = true;
                buttonBase.Padding = new Padding(8, 5, 8, 5);
            }

            if (child.HasChildren)
            {
                child.ApplyReadableButtonSizing();
            }
        }
    }
}
