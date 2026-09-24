namespace Hotdesks.Booking.Api.Models;

public sealed class Reservation
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid HotdeskId { get; set; }

    public DateTimeOffset From { get; set; }

    public DateTimeOffset To { get; set; }

    public bool IsActive { get; set; } = true;

    public User User { get; set; } = null!;

    public Hotdesk Hotdesk { get; set; } = null!;
}
