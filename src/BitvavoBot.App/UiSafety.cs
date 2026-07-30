namespace BitvavoBot.App;

/// <summary>
/// Background work (the WebSocket client, the bot orchestrator) can raise its events at any time,
/// including before a Form/UserControl's window handle exists yet — e.g. MainForm kicks off the
/// WebSocket connection from its own constructor, before Application.Run has shown the window.
/// Plain Control.BeginInvoke throws in that case. For most of these events that's more than a
/// cosmetic bug: the exception surfaces on whatever background thread raised the event, and for
/// the WebSocket reconnect loop specifically, an uncaught exception there permanently kills the
/// loop — so the connection never gets another chance and the status label freezes forever.
/// Route every cross-thread UI update through this instead of a bare BeginInvoke.
/// </summary>
internal static class UiSafety
{
    public static void SafeBeginInvoke(this Control control, Action action)
    {
        if (control.IsDisposed || !control.IsHandleCreated) return;

        try
        {
            control.BeginInvoke(new MethodInvoker(() => action()));
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
