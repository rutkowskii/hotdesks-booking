using Microsoft.EntityFrameworkCore;
using Hotdesks.Booking.Api.Models;

namespace Hotdesks.Booking.Api.Data;

public sealed class HotdesksBookingDbContext(
    DbContextOptions<HotdesksBookingDbContext> options) : DbContext(options)
{
    public DbSet<Hotdesk> Hotdesks => Set<Hotdesk>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

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

            entity.HasMany(hotdesk => hotdesk.Reservations)
                .WithOne(reservation => reservation.Hotdesk)
                .HasForeignKey(reservation => reservation.HotdeskId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(user => user.Id);

            entity.Property(user => user.Name)
                .IsRequired();

            entity.HasMany(user => user.Reservations)
                .WithOne(reservation => reservation.User)
                .HasForeignKey(reservation => reservation.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(reservation => reservation.Id);

            entity.Property(reservation => reservation.From)
                .IsRequired();

            entity.Property(reservation => reservation.To)
                .IsRequired();

            entity.ToTable(table => table.HasCheckConstraint(
                "CK_Reservations_FromBeforeTo",
                "\"From\" < \"To\""));
        });
    }
}
