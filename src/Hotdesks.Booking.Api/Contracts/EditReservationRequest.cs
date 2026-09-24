namespace Hotdesks.Booking.Api.Contracts;

public sealed class EditReservationRequest
{
    public Guid HotdeskId { get; init; }

    public DateTimeOffset From { get; init; }

    public DateTimeOffset To { get; init; }
}
