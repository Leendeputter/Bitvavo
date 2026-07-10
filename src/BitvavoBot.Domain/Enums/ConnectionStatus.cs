namespace BitvavoBot.Domain.Enums;

public enum ConnectionStatus
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Reconnecting = 3,
    FallbackPolling = 4
}
