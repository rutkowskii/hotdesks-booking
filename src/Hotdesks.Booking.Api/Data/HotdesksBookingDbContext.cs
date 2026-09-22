using Microsoft.EntityFrameworkCore;
using Hotdesks.Booking.Api.Models;

namespace Hotdesks.Booking.Api.Data;

public sealed class HotdesksBookingDbContext(
    DbContextOptions<HotdesksBookingDbContext> options) : DbContext(options)
{
    public DbSet<Hotdesk> Hotdesks => Set<Hotdesk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Hotdesk>(entity =>
        {
            entity.HasKey(hotdesk => hotdesk.Id);

            entity.Property(hotdesk => hotdesk.Name)
                .HasMaxLength(64)
                .IsRequired();

            entity.Property(hotdesk => hotdesk.IsAvailable247)
                .IsRequired();

            entity.Property(hotdesk => hotdesk.IsEnabled)
                .HasDefaultValue(true)
                .IsRequired();
        });
    }
}
