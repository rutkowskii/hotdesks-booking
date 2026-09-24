using System.Net;
using System.Net.Http.Json;
using Hotdesks.Booking.Api.Contracts;
using Hotdesks.Booking.Api.Data;
using Hotdesks.Booking.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Hotdesks.Booking.Api.Tests;

public sealed partial class HotdesksEndpointTests
{
    [Fact]
    public async Task Adding_an_overlapping_reservation_returns_conflict()
    {
        using var client = factory.CreateClient();
        var userId = await CreateUserAsync();
        var hotdeskId = await CreateHotdeskAsync(client);
        var reservationId = Guid.Empty;
        var from = DateTimeOffset.UtcNow.AddHours(1);

        try
        {
            var firstResponse = await client.PostAsJsonAsync(
                "/reservation/add",
                new AddReservationRequest
                {
                    UserId = userId,
                    HotdeskId = hotdeskId,
                    From = from,
                    To = from.AddHours(1)
                });
            Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

            var firstReservation = await firstResponse.Content.ReadFromJsonAsync<ReservationResponse>();
            Assert.NotNull(firstReservation);
            reservationId = firstReservation.Id;

            var overlapResponse = await client.PostAsJsonAsync(
                "/reservation/add",
                new AddReservationRequest
                {
                    UserId = userId,
                    HotdeskId = hotdeskId,
                    From = from.AddMinutes(30),
                    To = from.AddHours(2)
                });

            Assert.Equal(HttpStatusCode.Conflict, overlapResponse.StatusCode);
        }
        finally
        {
            await RemoveReservationAsync(reservationId);
            await RemoveHotdeskAsync(hotdeskId);
            await RemoveUserAsync(userId);
        }
    }

    [Fact]
    public async Task Database_rejects_an_overlapping_reservation()
    {
        using var client = factory.CreateClient();
        var userId = await CreateUserAsync();
        var hotdeskId = await CreateHotdeskAsync(client);
        var reservationId = Guid.Empty;
        var from = DateTimeOffset.UtcNow.AddHours(1);

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<HotdesksBookingDbContext>();

            var firstReservation = new Reservation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                HotdeskId = hotdeskId,
                From = from,
                To = from.AddHours(1)
            };
            dbContext.Reservations.Add(firstReservation);
            await dbContext.SaveChangesAsync();
            reservationId = firstReservation.Id;

            dbContext.Reservations.Add(new Reservation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                HotdeskId = hotdeskId,
                From = from.AddMinutes(30),
                To = from.AddHours(2)
            });

            var exception = await Assert.ThrowsAsync<DbUpdateException>(
                () => dbContext.SaveChangesAsync());
            var postgresException = FindPostgresException(exception);

            Assert.NotNull(postgresException);
            Assert.Equal(PostgresErrorCodes.ExclusionViolation, postgresException.SqlState);
            Assert.Equal(
                "EX_Reservations_HotdeskId_TimeRange_NoOverlap",
                postgresException.ConstraintName);
        }
        finally
        {
            await RemoveReservationAsync(reservationId);
            await RemoveHotdeskAsync(hotdeskId);
            await RemoveUserAsync(userId);
        }
    }

    [Fact]
    public async Task Cancelling_a_reservation_allows_an_overlapping_reservation()
    {
        using var client = factory.CreateClient();
        var userId = await CreateUserAsync();
        var hotdeskId = await CreateHotdeskAsync(client);
        var reservationIds = new List<Guid>();
        var from = DateTimeOffset.UtcNow.AddHours(1);

        try
        {
            var firstResponse = await client.PostAsJsonAsync(
                "/reservation/add",
                new AddReservationRequest
                {
                    UserId = userId,
                    HotdeskId = hotdeskId,
                    From = from,
                    To = from.AddHours(1)
                });
            Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

            var firstReservation = await firstResponse.Content.ReadFromJsonAsync<ReservationResponse>();
            Assert.NotNull(firstReservation);
            reservationIds.Add(firstReservation.Id);

            var cancelResponse = await client.PostAsync($"/reservation/{firstReservation.Id}/cancel", null);
            Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

            var historyResponse = await client.GetAsync($"/reservation/{firstReservation.VersionId}/history");
            Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

            var reservations = await historyResponse.Content.ReadFromJsonAsync<List<ReservationResponse>>();
            Assert.NotNull(reservations);
            Assert.Contains(
                reservations,
                reservation => reservation.Id == firstReservation.Id
                    && reservation.IsActive
                    && !reservation.IsLastVersion);
            var cancelledReservation = Assert.Single(
                reservations,
                reservation => reservation.IsLastVersion && !reservation.IsActive);
            reservationIds.Add(cancelledReservation.Id);

            var overlappingResponse = await client.PostAsJsonAsync(
                "/reservation/add",
                new AddReservationRequest
                {
                    UserId = userId,
                    HotdeskId = hotdeskId,
                    From = from.AddMinutes(30),
                    To = from.AddHours(2)
                });
            Assert.Equal(HttpStatusCode.Created, overlappingResponse.StatusCode);

            var overlappingReservation = await overlappingResponse.Content.ReadFromJsonAsync<ReservationResponse>();
            Assert.NotNull(overlappingReservation);
            reservationIds.Add(overlappingReservation.Id);
        }
        finally
        {
            foreach (var reservationId in reservationIds)
            {
                await RemoveReservationAsync(reservationId);
            }

            await RemoveHotdeskAsync(hotdeskId);
            await RemoveUserAsync(userId);
        }
    }

    [Fact]
    public async Task Database_allows_an_overlap_with_an_inactive_reservation()
    {
        using var client = factory.CreateClient();
        var userId = await CreateUserAsync();
        var hotdeskId = await CreateHotdeskAsync(client);
        var reservationIds = new List<Guid>();
        var from = DateTimeOffset.UtcNow.AddHours(1);

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<HotdesksBookingDbContext>();

            var inactiveReservation = new Reservation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                HotdeskId = hotdeskId,
                From = from,
                To = from.AddHours(1),
                IsActive = false
            };
            dbContext.Reservations.Add(inactiveReservation);
            await dbContext.SaveChangesAsync();
            reservationIds.Add(inactiveReservation.Id);

            var activeOverlappingReservation = new Reservation
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                HotdeskId = hotdeskId,
                From = from.AddMinutes(30),
                To = from.AddHours(2)
            };
            dbContext.Reservations.Add(activeOverlappingReservation);
            await dbContext.SaveChangesAsync();
            reservationIds.Add(activeOverlappingReservation.Id);
        }
        finally
        {
            foreach (var reservationId in reservationIds)
            {
                await RemoveReservationAsync(reservationId);
            }

            await RemoveHotdeskAsync(hotdeskId);
            await RemoveUserAsync(userId);
        }
    }

}
