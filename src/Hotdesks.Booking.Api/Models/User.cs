namespace Hotdesks.Booking.Api.Models;

public sealed class User
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public ICollection<Reservation> Reservations { get; } = new List<Reservation>();
}
