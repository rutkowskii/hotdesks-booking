namespace Hotdesks.Booking.Api.Contracts;

public sealed class AddReservationRequest
{
    public Guid UserId { get; init; }

    public Guid HotdeskId { get; init; }

    public DateTimeOffset From { get; init; }

    public DateTimeOffset To { get; init; }
}
