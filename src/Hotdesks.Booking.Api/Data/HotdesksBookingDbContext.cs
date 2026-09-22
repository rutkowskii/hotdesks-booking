using Microsoft.EntityFrameworkCore;

namespace Hotdesks.Booking.Api.Data;

public sealed class HotdesksBookingDbContext(
    DbContextOptions<HotdesksBookingDbContext> options) : DbContext(options);
