namespace Hotdesks.Booking.Api.Contracts;

public sealed record ReservationResponse(
    Guid Id,
    Guid VersionId,
    int Version,
    bool IsLastVersion,
    Guid UserId,
    Guid HotdeskId,
    DateTimeOffset From,
    DateTimeOffset To,
    bool IsActive);
