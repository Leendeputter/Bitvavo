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
}
