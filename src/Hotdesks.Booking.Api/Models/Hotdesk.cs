namespace Hotdesks.Booking.Api.Models;

public sealed class Hotdesk
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public bool IsAvailable247 { get; set; }

    public bool IsEnabled { get; set; } = true;
}
