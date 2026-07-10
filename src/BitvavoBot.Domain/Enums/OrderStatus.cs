namespace BitvavoBot.Domain.Enums;

public enum OrderStatus
{
    New = 0,
    Open = 1,
    PartiallyFilled = 2,
    Filled = 3,
    Cancelled = 4,
    Rejected = 5,
    Expired = 6
}
