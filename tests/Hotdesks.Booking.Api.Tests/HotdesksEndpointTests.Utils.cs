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

public sealed partial class HotdesksEndpointTests : IClassFixture<HotdesksApiFactory>
{
    private readonly HotdesksApiFactory factory;

    public HotdesksEndpointTests(HotdesksApiFactory factory) => this.factory = factory;

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

    private static EditReservationRequest CreateEditRequest(Guid hotdeskId, DateTimeOffset from) =>
        new()
        {
            HotdeskId = hotdeskId,
            From = from.AddHours(2),
            To = from.AddHours(3)
        };

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
