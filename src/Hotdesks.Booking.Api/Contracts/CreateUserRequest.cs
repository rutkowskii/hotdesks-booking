using System.ComponentModel.DataAnnotations;

namespace Hotdesks.Booking.Api.Contracts;

public sealed class CreateUserRequest
{
    [Required]
    public required string Name { get; init; }
}
