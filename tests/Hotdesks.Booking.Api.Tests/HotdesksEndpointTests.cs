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

public sealed class HotdesksEndpointTests(HotdesksApiFactory factory) : IClassFixture<HotdesksApiFactory>
{
    [Fact]
    public async Task Get_hotdesks_returns_an_empty_list()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/hotdesks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var hotdesks = await response.Content.ReadFromJsonAsync<List<Hotdesk>>();

        Assert.NotNull(hotdesks);
        Assert.Empty(hotdesks);
    }

    [Fact]
    public async Task Hotdesks_can_be_created_and_soft_deleted()
    {
        using var client = factory.CreateClient();
        var runIdentifier = Guid.NewGuid().ToString("N");
        var requests = Enumerable.Range(1, 4)
            .Select(index => new CreateHotdeskRequest
            {
                Name = $"Integration desk {runIdentifier}-{index}",
                IsAvailable247 = index % 2 == 0
            })
            .ToList();

        var createdHotdesks = new List<Hotdesk>();
        foreach (var request in requests)
        {
            var response = await client.PostAsJsonAsync("/api/hotdesks", request);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var hotdesk = await response.Content.ReadFromJsonAsync<Hotdesk>();
            Assert.NotNull(hotdesk);
            createdHotdesks.Add(hotdesk);
        }

        var getAfterCreateResponse = await client.GetAsync("/api/hotdesks");
        Assert.Equal(HttpStatusCode.OK, getAfterCreateResponse.StatusCode);

        var hotdesksAfterCreate = await getAfterCreateResponse.Content.ReadFromJsonAsync<List<Hotdesk>>();
        Assert.NotNull(hotdesksAfterCreate);
        Assert.Equal(
            createdHotdesks.Select(hotdesk => hotdesk.Id).OrderBy(id => id),
            hotdesksAfterCreate.Select(hotdesk => hotdesk.Id).OrderBy(id => id));

        foreach (var hotdesk in createdHotdesks)
        {
            var response = await client.DeleteAsync($"/api/hotdesks/{hotdesk.Id}");

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var getAfterDeleteResponse = await client.GetAsync("/api/hotdesks");
        Assert.Equal(HttpStatusCode.OK, getAfterDeleteResponse.StatusCode);

        var hotdesksAfterDelete = await getAfterDeleteResponse.Content.ReadFromJsonAsync<List<Hotdesk>>();
        Assert.NotNull(hotdesksAfterDelete);
        Assert.Empty(hotdesksAfterDelete);
    }

    [Fact]
    public async Task Adding_a_reservation_for_a_nonexistent_user_fails()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/reservation/add",
            CreateReservationRequest(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Adding_a_reservation_for_a_nonexistent_hotdesk_fails()
    {
        using var client = factory.CreateClient();
        var userId = await CreateUserAsync();

        try
        {
            var response = await client.PostAsJsonAsync(
                "/reservation/add",
                CreateReservationRequest(userId, Guid.NewGuid()));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            await RemoveUserAsync(userId);
        }
    }

    [Fact]
    public async Task Adding_a_reservation_with_a_nonpositive_duration_fails()
    {
        using var client = factory.CreateClient();
        var now = DateTimeOffset.UtcNow;

        var response = await client.PostAsJsonAsync(
            "/reservation/add",
            new AddReservationRequest
            {
                UserId = Guid.NewGuid(),
                HotdeskId = Guid.NewGuid(),
                From = now,
                To = now
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_reservation_can_be_added_and_retrieved()
    {
        using var client = factory.CreateClient();
        var userId = await CreateUserAsync();
        var hotdeskId = Guid.Empty;
        var reservationId = Guid.Empty;

        try
        {
            var createHotdeskResponse = await client.PostAsJsonAsync(
                "/api/hotdesks",
                new CreateHotdeskRequest
                {
                    Name = $"Reservation desk {Guid.NewGuid():N}",
                    IsAvailable247 = true
                });
            Assert.Equal(HttpStatusCode.Created, createHotdeskResponse.StatusCode);

            var hotdesk = await createHotdeskResponse.Content.ReadFromJsonAsync<Hotdesk>();
            Assert.NotNull(hotdesk);
            hotdeskId = hotdesk.Id;

            var addReservationResponse = await client.PostAsJsonAsync(
                "/reservation/add",
                CreateReservationRequest(userId, hotdeskId));
            Assert.Equal(HttpStatusCode.Created, addReservationResponse.StatusCode);

            var reservation = await addReservationResponse.Content.ReadFromJsonAsync<ReservationResponse>();
            Assert.NotNull(reservation);
            reservationId = reservation.Id;
            Assert.Equal(userId, reservation.UserId);
            Assert.Equal(hotdeskId, reservation.HotdeskId);

            var getReservationsResponse = await client.GetAsync("/reservation");
            Assert.Equal(HttpStatusCode.OK, getReservationsResponse.StatusCode);

            var reservations = await getReservationsResponse.Content.ReadFromJsonAsync<List<ReservationResponse>>();
            Assert.NotNull(reservations);
            Assert.Contains(reservations, existing => existing.Id == reservationId);
        }
        finally
        {
            await RemoveReservationAsync(reservationId);
            await RemoveHotdeskAsync(hotdeskId);
            await RemoveUserAsync(userId);
        }
    }

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

    private static AddReservationRequest CreateReservationRequest(Guid userId, Guid hotdeskId)
    {
        var from = DateTimeOffset.UtcNow.AddHours(1);

        return new AddReservationRequest
        {
            UserId = userId,
            HotdeskId = hotdeskId,
            From = from,
            To = from.AddHours(1)
        };
    }

    private async Task<Guid> CreateUserAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HotdesksBookingDbContext>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = $"Integration user {Guid.NewGuid():N}"
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return user.Id;
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (var currentException = exception; currentException is not null; currentException = currentException.InnerException)
        {
            if (currentException is PostgresException postgresException)
            {
                return postgresException;
            }
        }

        return null;
    }

    private async Task<Guid> CreateHotdeskAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/hotdesks",
            new CreateHotdeskRequest
            {
                Name = $"Reservation desk {Guid.NewGuid():N}",
                IsAvailable247 = true
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var hotdesk = await response.Content.ReadFromJsonAsync<Hotdesk>();
        Assert.NotNull(hotdesk);

        return hotdesk.Id;
    }

    private async Task RemoveReservationAsync(Guid reservationId)
    {
        if (reservationId == Guid.Empty)
        {
            return;
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HotdesksBookingDbContext>();
        var reservation = await dbContext.Reservations.FindAsync(reservationId);
        if (reservation is not null)
        {
            dbContext.Reservations.Remove(reservation);
            await dbContext.SaveChangesAsync();
        }
    }

    private async Task RemoveHotdeskAsync(Guid hotdeskId)
    {
        if (hotdeskId == Guid.Empty)
        {
            return;
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HotdesksBookingDbContext>();
        var hotdesk = await dbContext.Hotdesks.FindAsync(hotdeskId);
        if (hotdesk is not null)
        {
            dbContext.Hotdesks.Remove(hotdesk);
            await dbContext.SaveChangesAsync();
        }
    }

    private async Task RemoveUserAsync(Guid userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HotdesksBookingDbContext>();
        var user = await dbContext.Users.FindAsync(userId);
        if (user is not null)
        {
            dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync();
        }
    }
}

public sealed class HotdesksApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            configurationBuilder.AddJsonFile(
                Path.Combine(AppContext.BaseDirectory, "appsettings.Test.json"),
                optional: false,
                reloadOnChange: false));
    }
}
