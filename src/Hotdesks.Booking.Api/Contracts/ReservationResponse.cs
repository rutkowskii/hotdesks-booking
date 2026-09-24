namespace Hotdesks.Booking.Api.Contracts;

public sealed record ReservationResponse(
    Guid Id,
    Guid UserId,
    Guid HotdeskId,
    DateTimeOffset From,
    DateTimeOffset To,
    bool IsActive);
