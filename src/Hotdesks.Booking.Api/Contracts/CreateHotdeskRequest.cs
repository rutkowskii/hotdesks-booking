using System.ComponentModel.DataAnnotations;

namespace Hotdesks.Booking.Api.Contracts;

public sealed class CreateHotdeskRequest
{
    [Required]
    [StringLength(64)]
    public required string Name { get; init; }

    public bool IsAvailable247 { get; init; }
}
